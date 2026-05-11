using System.Text;
using FileShareServer.Constants;
using FileShareServer.DTOs;
using FileShareServer.Hubs;
using FileShareServer.Models;
using FileShareServer.Services;
using Microsoft.AspNetCore.SignalR;

namespace FileShareServer.Extensions
{
    public static class ChatEndpointExtensions
    {
        public static WebApplication MapChatEndpoints(this WebApplication app, RouteGroupBuilder api)
        {
            var chatApi = api.MapGroup(AppConstants.ApiRoutes.Chat.Root)
                .WithName("Chat")
                .RequireAuthorization();

            chatApi.MapGet(AppConstants.ApiRoutes.Chat.Conversation, async (int friendId, HttpContext httpContext, ChatService chatService, FriendshipService friendshipService) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var areFriends = await friendshipService.AreFriendsAsync(userId, friendId);
                if (!areFriends) return Results.Ok(new List<ChatMessageDto>());

                var messages = await chatService.GetConversationAsync(userId, friendId);
                return Results.Ok(messages.Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    Content = m.Content,
                    Timestamp = m.Timestamp,
                    IsRead = m.IsRead,
                    Type = (int)m.Type
                }));
            })
            .WithName("GetConversation");

            chatApi.MapGet(AppConstants.ApiRoutes.Chat.Unread, async (HttpContext httpContext, ChatService chatService) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var messages = await chatService.GetUnreadMessagesAsync(userId);
                return Results.Ok(messages.Select(m => new UnreadMessageDto
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    Content = m.Content,
                    Timestamp = m.Timestamp
                }));
            })
            .WithName("GetUnreadMessages");

            chatApi.MapPost(AppConstants.ApiRoutes.Chat.MarkRead, async (int messageId, ChatService chatService) =>
            {
                var result = await chatService.MarkAsReadAsync(messageId);
                if (!result) return Results.NotFound();
                return Results.Ok();
            })
            .WithName("MarkMessageAsRead");

            chatApi.MapPost(AppConstants.ApiRoutes.Chat.Store, async (int friendId, HttpContext httpContext, ChatService chatService, FriendshipService friendshipService, StoreMessageRequest request) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var areFriends = await friendshipService.AreFriendsAsync(userId, friendId);
                if (!areFriends) return Results.BadRequest(AppConstants.ErrorMessages.FriendsOnly);

                if (string.IsNullOrWhiteSpace(request.Content))
                {
                    return Results.BadRequest(AppConstants.ErrorMessages.MessageContentRequired);
                }

                var message = await chatService.SendMessageAsync(userId, friendId, request.Content, MessageType.Text);
                return Results.Ok(new { message.Id, message.SenderId, message.Content, message.Timestamp, Type = (int)message.Type });
            })
            .WithName("StoreMessage");

            chatApi.MapPost(AppConstants.ApiRoutes.Chat.StoreFile, async (
                int friendId,
                HttpContext httpContext,
                ChatService chatService,
                FriendshipService friendshipService,
                UserService userService,
                IHubContext<ChatHub> hubContext,
                IUserConnectionManager connectionManager,
                StoreFileMessageRequest request) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var areFriends = await friendshipService.AreFriendsAsync(userId, friendId);
                if (!areFriends) return Results.BadRequest(AppConstants.ErrorMessages.FriendsOnly);

                if (string.IsNullOrWhiteSpace(request.FileName) || request.FileSize <= 0)
                {
                    return Results.BadRequest(AppConstants.ErrorMessages.FileMetadataRequired);
                }

                var source = request.Source?.ToLowerInvariant() == AppConstants.FileMessageFormat.P2pSource
                    ? AppConstants.FileMessageFormat.P2pSource
                    : AppConstants.FileMessageFormat.ServerSource;
                var token = string.IsNullOrWhiteSpace(request.Token) ? AppConstants.FileMessageFormat.NoToken : request.Token;
                var fileNameBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.FileName));
                var content = $"FILE{AppConstants.FileMessageFormat.Separator}0{AppConstants.FileMessageFormat.Separator}{fileNameBase64}{AppConstants.FileMessageFormat.Separator}{request.FileSize}{AppConstants.FileMessageFormat.Separator}{source}{AppConstants.FileMessageFormat.Separator}{token}";

                var message = await chatService.SendMessageAsync(userId, friendId, content, MessageType.File);

                var sender = await userService.GetUserByIdAsync(userId);
                var receiver = await userService.GetUserByIdAsync(friendId);
                foreach (var connectionId in connectionManager.GetConnections(friendId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync(AppConstants.HubMethods.ReceiveMessage, new
                    {
                        message.Id,
                        SenderId = userId,
                        SenderName = sender?.Username,
                        message.Content,
                        message.Timestamp,
                        Type = (int)message.Type
                    });
                    await hubContext.Clients.Client(connectionId).SendAsync(AppConstants.HubMethods.SocialDataChanged);
                }
                foreach (var connectionId in connectionManager.GetConnections(userId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync(AppConstants.HubMethods.SocialDataChanged);
                }

                return Results.Ok(new { message.Id });
            })
            .WithName("StoreFileMessage");

            return app;
        }
    }
}
