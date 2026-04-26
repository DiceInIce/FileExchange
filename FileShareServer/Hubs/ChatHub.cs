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

        public ChatHub(UserService userService, ChatService chatService, FriendshipService friendshipService, AuthService authService)
        {
            _userService = userService;
            _chatService = chatService;
            _friendshipService = friendshipService;
            _authService = authService;
        }

        public override async Task OnConnectedAsync()
        {
            var token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
            if (token != null)
            {
                var userId = _authService.GetUserIdFromToken(token);
                if (userId.HasValue)
                {
                    await _userService.SetUserOnlineAsync(userId.Value, Context.ConnectionId);
                    Context.Items["UserId"] = userId;

                    var user = await _userService.GetUserByIdAsync(userId.Value);
                    if (user != null)
                    {
                        var friends = await _friendshipService.GetFriendsAsync(userId.Value);
                        foreach (var friend in friends)
                        {
                            if (friend.IsOnline)
                            {
                                await Clients.Caller.SendAsync("UserOnline", friend.Id, friend.Username, friend.DisplayName);
                            }

                            if (friend.IsOnline && friend.ConnectionId != null)
                            {
                                await Clients.Client(friend.ConnectionId).SendAsync("UserOnline", userId, user.Username, user.DisplayName);
                            }
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
                await _userService.SetUserOfflineAsync(userId);

                var friends = await _friendshipService.GetFriendsAsync(userId);
                foreach (var friend in friends)
                {
                    if (friend.IsOnline && friend.ConnectionId != null)
                    {
                        await Clients.Client(friend.ConnectionId).SendAsync("UserOffline", userId);
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
                
                var receiver = await _userService.GetUserByIdAsync(receiverId);
                if (receiver?.ConnectionId != null)
                {
                    var sender = await _userService.GetUserByIdAsync(senderId);
                    await Clients.Client(receiver.ConnectionId).SendAsync("ReceiveMessage", new
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

                var receiver = await _userService.GetUserByIdAsync(receiverId);
                if (receiver?.ConnectionId != null)
                {
                    var sender = await _userService.GetUserByIdAsync(senderId);
                    await Clients.Client(receiver.ConnectionId).SendAsync("ReceiveOffer", new
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

                var receiver = await _userService.GetUserByIdAsync(receiverId);
                if (receiver?.ConnectionId != null)
                {
                    await Clients.Client(receiver.ConnectionId).SendAsync("ReceiveAnswer", new
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

                var receiver = await _userService.GetUserByIdAsync(receiverId);
                if (receiver?.ConnectionId != null)
                {
                    await Clients.Client(receiver.ConnectionId).SendAsync("ReceiveIceCandidate", new
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

                var receiver = await _userService.GetUserByIdAsync(receiverId);
                if (receiver?.ConnectionId != null)
                {
                    var sender = await _userService.GetUserByIdAsync(senderId);
                    await Clients.Client(receiver.ConnectionId).SendAsync("FileTransferRequest", new
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
