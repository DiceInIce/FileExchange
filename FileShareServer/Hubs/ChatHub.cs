using Microsoft.AspNetCore.SignalR;
using FileShareServer.Services;
using FileShareServer.Models;

namespace FileShareServer.Hubs
{
    public class ChatHub : Hub
    {
        private readonly UserService _userService;
        private readonly ChatService _chatService;
        private readonly FriendshipService _friendshipService;
        private readonly AuthService _authService;
        private readonly IUserConnectionManager _connectionManager;

        public ChatHub(UserService userService, ChatService chatService, FriendshipService friendshipService, AuthService authService, IUserConnectionManager connectionManager)
        {
            _userService = userService;
            _chatService = chatService;
            _friendshipService = friendshipService;
            _authService = authService;
            _connectionManager = connectionManager;
        }

        public override async Task OnConnectedAsync()
        {
            var token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
            if (token != null)
            {
                var userId = _authService.GetUserIdFromToken(token);
                if (userId.HasValue)
                {
                    var becameOnline = _connectionManager.AddConnection(userId.Value, Context.ConnectionId);
                    Context.Items["UserId"] = userId.Value;

                    var user = await _userService.GetUserByIdAsync(userId.Value);
                    if (becameOnline)
                    {
                        await _userService.SetUserOnlineAsync(userId.Value, Context.ConnectionId);
                    }

                    var friends = await _friendshipService.GetFriendsAsync(userId.Value);
                    foreach (var friend in friends)
                    {
                        if (_connectionManager.HasConnections(friend.Id))
                        {
                            await Clients.Caller.SendAsync("UserOnline", friend.Id, friend.Username, friend.DisplayName);
                        }

                        foreach (var friendConnectionId in _connectionManager.GetConnections(friend.Id))
                        {
                            await Clients.Client(friendConnectionId).SendAsync("UserOnline", userId.Value, user?.Username, user?.DisplayName);
                        }
                    }
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (Context.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int userId)
            {
                var becameOffline = _connectionManager.RemoveConnection(userId, Context.ConnectionId);

                if (becameOffline)
                {
                    await _userService.SetUserOfflineAsync(userId);
                }

                var friends = await _friendshipService.GetFriendsAsync(userId);
                foreach (var friend in friends)
                {
                    if (_connectionManager.HasConnections(friend.Id))
                    {
                        foreach (var friendConnectionId in _connectionManager.GetConnections(friend.Id))
                        {
                            if (becameOffline)
                            {
                                await Clients.Client(friendConnectionId).SendAsync("UserOffline", userId);
                            }
                        }
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(int receiverId, string content)
        {
            if (Context.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int senderId)
            {
                var areFriends = await _friendshipService.AreFriendsAsync(senderId, receiverId);
                if (!areFriends)
                {
                    return;
                }

                var message = await _chatService.SendMessageAsync(senderId, receiverId, content, MessageType.Text);
                var sender = await _userService.GetUserByIdAsync(senderId);
                var receiverConnectionIds = _connectionManager.GetConnections(receiverId);
                foreach (var connectionId in receiverConnectionIds)
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", new
                    {
                        message.Id,
                        SenderId = senderId,
                        SenderName = sender?.Username,
                        message.Content,
                        message.Timestamp,
                        Type = (int)message.Type
                    });
                }
            }
        }

        // WebRTC Signaling
        public async Task SendOffer(int receiverId, string offer)
        {
            if (Context.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int senderId)
            {
                var areFriends = await _friendshipService.AreFriendsAsync(senderId, receiverId);
                if (!areFriends)
                {
                    return;
                }

                var sender = await _userService.GetUserByIdAsync(senderId);
                var receiverConnectionIds = _connectionManager.GetConnections(receiverId);
                foreach (var connectionId in receiverConnectionIds)
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveOffer", new
                    {
                        SenderId = senderId,
                        SenderName = sender?.Username,
                        Offer = offer
                    });
                }
            }
        }

        public async Task SendAnswer(int receiverId, string answer)
        {
            if (Context.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int senderId)
            {
                var areFriends = await _friendshipService.AreFriendsAsync(senderId, receiverId);
                if (!areFriends)
                {
                    return;
                }

                var receiverConnectionIds = _connectionManager.GetConnections(receiverId);
                foreach (var connectionId in receiverConnectionIds)
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveAnswer", new
                    {
                        SenderId = senderId,
                        Answer = answer
                    });
                }
            }
        }

        public async Task SendIceCandidate(int receiverId, string candidate)
        {
            if (Context.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int senderId)
            {
                var areFriends = await _friendshipService.AreFriendsAsync(senderId, receiverId);
                if (!areFriends)
                {
                    return;
                }

                var receiverConnectionIds = _connectionManager.GetConnections(receiverId);
                foreach (var connectionId in receiverConnectionIds)
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveIceCandidate", new
                    {
                        SenderId = senderId,
                        Candidate = candidate
                    });
                }
            }
        }

        public async Task InitiateFileTransfer(int receiverId, string fileName, long fileSize)
        {
            if (Context.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int senderId)
            {
                var areFriends = await _friendshipService.AreFriendsAsync(senderId, receiverId);
                if (!areFriends)
                {
                    return;
                }

                var sender = await _userService.GetUserByIdAsync(senderId);
                var receiverConnectionIds = _connectionManager.GetConnections(receiverId);
                foreach (var connectionId in receiverConnectionIds)
                {
                    await Clients.Client(connectionId).SendAsync("FileTransferRequest", new
                    {
                        SenderId = senderId,
                        SenderName = sender?.Username,
                        FileName = fileName,
                        FileSize = fileSize
                    });
                }
            }
        }
    }
}
