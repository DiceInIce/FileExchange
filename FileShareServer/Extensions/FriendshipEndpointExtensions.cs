using FileShareServer.Constants;
using FileShareServer.DTOs;
using FileShareServer.Hubs;
using FileShareServer.Services;
using Microsoft.AspNetCore.SignalR;

namespace FileShareServer.Extensions
{
    public static class FriendshipEndpointExtensions
    {
        public static WebApplication MapFriendshipEndpoints(this WebApplication app, RouteGroupBuilder api)
        {
            var friendApi = api.MapGroup(AppConstants.ApiRoutes.Friends.Root)
                .WithName("Friends")
                .RequireAuthorization();

            friendApi.MapPost(AppConstants.ApiRoutes.Friends.SendRequest, async (
                int friendId,
                HttpContext httpContext,
                FriendshipService friendshipService,
                UserService userService,
                IHubContext<ChatHub> hubContext,
                IUserConnectionManager connectionManager) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var result = await friendshipService.SendFriendRequestAsync(userId, friendId);
                if (result == null) return Results.BadRequest(AppConstants.ErrorMessages.CannotSendFriendRequest);

                var sender = await userService.GetUserByIdAsync(userId);
                var recipient = await userService.GetUserByIdAsync(friendId);
                foreach (var connectionId in connectionManager.GetConnections(friendId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync(AppConstants.HubMethods.SocialDataChanged);
                    await hubContext.Clients.Client(connectionId).SendAsync(AppConstants.HubMethods.FriendRequestReceived, new
                    {
                        SenderId = sender?.Id ?? userId,
                        SenderName = sender?.DisplayName ?? sender?.Username ?? "Unknown"
                    });
                }
                foreach (var connectionId in connectionManager.GetConnections(userId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync(AppConstants.HubMethods.SocialDataChanged);
                }

                return Results.Ok(result);
            })
            .WithName("SendFriendRequest");

            friendApi.MapPost(AppConstants.ApiRoutes.Friends.Accept, async (
                int friendId,
                HttpContext httpContext,
                FriendshipService friendshipService,
                UserService userService,
                IHubContext<ChatHub> hubContext,
                IUserConnectionManager connectionManager) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var result = await friendshipService.AcceptFriendRequestAsync(userId, friendId);
                if (!result) return Results.BadRequest(AppConstants.ErrorMessages.CannotAcceptFriendRequest);

                var accepter = await userService.GetUserByIdAsync(userId);
                var requester = await userService.GetUserByIdAsync(friendId);
                foreach (var connectionId in connectionManager.GetConnections(userId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync(AppConstants.HubMethods.SocialDataChanged);
                }
                foreach (var connectionId in connectionManager.GetConnections(friendId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync(AppConstants.HubMethods.SocialDataChanged);
                }

                return Results.Ok();
            })
            .WithName("AcceptFriendRequest");

            friendApi.MapPost(AppConstants.ApiRoutes.Friends.Reject, async (
                int friendId,
                HttpContext httpContext,
                FriendshipService friendshipService,
                UserService userService,
                IHubContext<ChatHub> hubContext,
                IUserConnectionManager connectionManager) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var result = await friendshipService.RejectFriendRequestAsync(userId, friendId);
                if (!result) return Results.BadRequest(AppConstants.ErrorMessages.CannotRejectFriendRequest);

                var rejector = await userService.GetUserByIdAsync(userId);
                var requester = await userService.GetUserByIdAsync(friendId);
                foreach (var connectionId in connectionManager.GetConnections(userId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync(AppConstants.HubMethods.SocialDataChanged);
                }
                foreach (var connectionId in connectionManager.GetConnections(friendId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync(AppConstants.HubMethods.SocialDataChanged);
                }

                return Results.Ok();
            })
            .WithName("RejectFriendRequest");

            friendApi.MapDelete(AppConstants.ApiRoutes.Friends.Remove, async (
                int friendId,
                HttpContext httpContext,
                FriendshipService friendshipService,
                UserService userService,
                IHubContext<ChatHub> hubContext,
                IUserConnectionManager connectionManager) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var result = await friendshipService.RemoveFriendAsync(userId, friendId);
                if (!result) return Results.BadRequest(AppConstants.ErrorMessages.CannotRemoveFriend);

                var initiator = await userService.GetUserByIdAsync(userId);
                var target = await userService.GetUserByIdAsync(friendId);
                foreach (var connectionId in connectionManager.GetConnections(userId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync(AppConstants.HubMethods.SocialDataChanged);
                }
                foreach (var connectionId in connectionManager.GetConnections(friendId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync(AppConstants.HubMethods.SocialDataChanged);
                }

                return Results.Ok();
            })
            .WithName("RemoveFriend");

            friendApi.MapGet(AppConstants.ApiRoutes.Friends.List, async (HttpContext httpContext, FriendshipService friendshipService, IUserConnectionManager connectionManager) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var friends = await friendshipService.GetFriendsAsync(userId);
                return Results.Ok(friends.Select(f => new UserDto
                {
                    Id = f.Id,
                    Username = f.Username,
                    DisplayName = f.DisplayName,
                    IsOnline = connectionManager.HasConnections(f.Id)
                }));
            })
            .WithName("GetFriends");

            friendApi.MapGet(AppConstants.ApiRoutes.Friends.Pending, async (HttpContext httpContext, FriendshipService friendshipService, IUserConnectionManager connectionManager) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var requests = await friendshipService.GetPendingRequestsAsync(userId);
                return Results.Ok(requests.Select(f => new FriendshipRequestDto
                {
                    Id = f.Id,
                    UserId = f.UserId,
                    User = f.User == null
                        ? null
                        : new UserDto
                        {
                            Id = f.User.Id,
                            Username = f.User.Username,
                            DisplayName = f.User.DisplayName,
                            IsOnline = connectionManager.HasConnections(f.User.Id)
                        }
                }));
            })
            .WithName("GetPendingRequests");

            friendApi.MapGet(AppConstants.ApiRoutes.Friends.Sent, async (HttpContext httpContext, FriendshipService friendshipService, IUserConnectionManager connectionManager) =>
            {
                var userId = httpContext.GetUserId();
                if (userId == 0) return Results.Unauthorized();

                var requests = await friendshipService.GetSentRequestsAsync(userId);
                return Results.Ok(requests.Select(f => new FriendshipRequestDto
                {
                    Id = f.Id,
                    UserId = f.FriendId,
                    User = f.Friend == null
                        ? null
                        : new UserDto
                        {
                            Id = f.Friend.Id,
                            Username = f.Friend.Username,
                            DisplayName = f.Friend.DisplayName,
                            IsOnline = connectionManager.HasConnections(f.Friend.Id)
                        }
                }));
            })
            .WithName("GetSentRequests");

            return app;
        }
    }
}
