using System.Text;
using FileShareServer.Constants;
using FileShareServer.Data;
using FileShareServer.Hubs;
using FileShareServer.Models;
using FileShareServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FileShareServer.Extensions
{
    public static class FileEndpointExtensions
    {
        public static WebApplication MapFileEndpoints(this WebApplication app, RouteGroupBuilder api, WebApplication webApp)
        {
            var filesApi = api.MapGroup(AppConstants.ApiRoutes.Files.Root)
                .WithName("Files")
                .RequireAuthorization();

            filesApi.MapPost(AppConstants.ApiRoutes.Files.Upload, async (
                int receiverId,
                HttpContext httpContext,
                IFormFile file,
                ApplicationDbContext db,
                ChatService chatService,
                FriendshipService friendshipService,
                UserService userService,
                IHubContext<ChatHub> hubContext) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                if (file == null || file.Length == 0) return Results.BadRequest(AppConstants.ErrorMessages.FileIsEmpty);

                // ✅ Ограничение размера файла на сервере (100 МБ)
                const long maxFileSize = 100 * 1024 * 1024; // 100 MB
                if (file.Length > maxFileSize)
                {
                    return Results.BadRequest("Файл слишком большой. Максимум 100 МБ.");
                }

                var areFriends = await friendshipService.AreFriendsAsync(userId, receiverId);
                if (!areFriends) return Results.BadRequest(AppConstants.ErrorMessages.FriendsOnly);

                var uploadsRoot = Path.Combine(webApp.Environment.ContentRootPath, "uploads");
                Directory.CreateDirectory(uploadsRoot);

                var originalName = Path.GetFileName(file.FileName);
                var storedName = $"{Guid.NewGuid():N}_{originalName}";
                var storedPath = Path.Combine(uploadsRoot, storedName);

                await using (var fs = new FileStream(storedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(fs);
                }

                var transfer = new FileTransfer
                {
                    InitiatorId = userId,
                    RecipientId = receiverId,
                    FileName = originalName,
                    FileSize = file.Length,
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    Status = TransferStatus.Completed,
                    WebRtcConnectionId = storedName
                };

                db.FileTransfers.Add(transfer);
                await db.SaveChangesAsync();

                var fileNameBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(originalName));
                var fileMessageContent = $"FILE{AppConstants.FileMessageFormat.Separator}{transfer.Id}{AppConstants.FileMessageFormat.Separator}{fileNameBase64}{AppConstants.FileMessageFormat.Separator}{file.Length}{AppConstants.FileMessageFormat.Separator}{AppConstants.FileMessageFormat.ServerSource}{AppConstants.FileMessageFormat.Separator}{AppConstants.FileMessageFormat.NoToken}";
                var chatMessage = await chatService.SendMessageAsync(userId, receiverId, fileMessageContent, MessageType.File);

                var sender = await userService.GetUserByIdAsync(userId);
                var receiver = await userService.GetUserByIdAsync(receiverId);
                if (receiver?.ConnectionId != null)
                {
                    await hubContext.Clients.Client(receiver.ConnectionId).SendAsync(AppConstants.HubMethods.FileTransferAvailable, new
                    {
                        transfer.Id,
                        SenderId = userId,
                        SenderName = sender?.DisplayName ?? sender?.Username ?? "Unknown",
                        transfer.FileName,
                        transfer.FileSize
                    });
                    await hubContext.Clients.Client(receiver.ConnectionId).SendAsync(AppConstants.HubMethods.ReceiveMessage, new
                    {
                        chatMessage.Id,
                        SenderId = userId,
                        SenderName = sender?.Username,
                        Content = chatMessage.Content,
                        chatMessage.Timestamp,
                        Type = (int)chatMessage.Type
                    });
                    await hubContext.Clients.Client(receiver.ConnectionId).SendAsync(AppConstants.HubMethods.SocialDataChanged);
                }
                if (sender?.ConnectionId != null)
                {
                    await hubContext.Clients.Client(sender.ConnectionId).SendAsync(AppConstants.HubMethods.SocialDataChanged);
                }

                return Results.Ok(new { transfer.Id, transfer.FileName, transfer.FileSize });
            })
            .DisableAntiforgery()
            .WithName("UploadFile");

            filesApi.MapGet(AppConstants.ApiRoutes.Files.Inbox, async (HttpContext httpContext, ApplicationDbContext db) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var files = await db.FileTransfers
                    .Where(f => f.RecipientId == userId && f.Status == TransferStatus.Completed)
                    .OrderByDescending(f => f.CompletedAt)
                    .Select(f => new
                    {
                        f.Id,
                        f.FileName,
                        f.FileSize,
                        f.CompletedAt,
                        SenderId = f.InitiatorId,
                        SenderName = f.Initiator != null ? (f.Initiator.DisplayName ?? f.Initiator.Username) : "Unknown"
                    })
                    .ToListAsync();

                return Results.Ok(files);
            })
            .WithName("GetInboxFiles");

            filesApi.MapGet(AppConstants.ApiRoutes.Files.Download, async (int id, HttpContext httpContext, ApplicationDbContext db) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var transfer = await db.FileTransfers.FirstOrDefaultAsync(f => f.Id == id);
                if (transfer == null) return Results.NotFound();

                if (transfer.RecipientId != userId && transfer.InitiatorId != userId) return Results.Forbid();

                if (string.IsNullOrWhiteSpace(transfer.WebRtcConnectionId))
                {
                    return Results.NotFound(AppConstants.ErrorMessages.FileNotFoundInStorage);
                }

                var uploadsRoot = Path.Combine(webApp.Environment.ContentRootPath, "uploads");
                var storedPath = Path.Combine(uploadsRoot, transfer.WebRtcConnectionId);
                if (!File.Exists(storedPath)) return Results.NotFound(AppConstants.ErrorMessages.FileNotFoundOnDisk);

                // ✅ Потоковое скачивание вместо загрузки в памяь
                var stream = File.OpenRead(storedPath);
                return Results.Stream(stream, "application/octet-stream", transfer.FileName);
            })
            .WithName("DownloadFile");

            return app;
        }
    }
}
