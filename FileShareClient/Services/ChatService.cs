using Microsoft.AspNetCore.SignalR.Client;
using FileShareClient.Models;

namespace FileShareClient.Services
{
    public class ChatService
    {
        private HubConnection? _connection;
        /// <summary>Состояние хаба для UI (подключение, переподключение, обрыв).</summary>
        public event Action<HubConnectionState>? OnConnectionStateChanged;
        public event Action<ChatMessage>? OnMessageReceived;
        public event Action<int, string, string>? OnUserOnline;
        public event Action<int>? OnUserOffline;
        public event Action<WebRTCOffer>? OnOfferReceived;
        public event Action<WebRTCAnswer>? OnAnswerReceived;
        public event Action<IceCandidate>? OnIceCandidateReceived;
        public event Action<FileTransferRequest>? OnFileTransferRequest;
        public event Action? OnSocialDataChanged;
        public event Action<FriendRequestNotification>? OnFriendRequestReceived;
        public event Action<FileTransferAvailableNotification>? OnFileTransferAvailable;
        public event Action? OnConnected;
        public event Action? OnDisconnected;

        public HubConnectionState GetConnectionState() =>
            _connection?.State ?? HubConnectionState.Disconnected;

        public async Task ConnectAsync(string serverUrl, string token)
        {
            if (_connection != null)
            {
                try
                {
                    await _connection.DisposeAsync();
                }
                catch
                {
                    // ignore
                }

                _connection = null;
            }

            _connection = new HubConnectionBuilder()
                .WithUrl($"{serverUrl}/chathub?access_token={token}", options =>
                {
                    options.HttpMessageHandlerFactory = _ =>
                    {
                        var handler = new HttpClientHandler();
                        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                        return handler;
                    };
                })
                .WithAutomaticReconnect()
                .Build();

            _connection.Reconnecting += error =>
            {
                OnConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting);
                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                OnConnectionStateChanged?.Invoke(HubConnectionState.Connected);
                OnConnected?.Invoke();
                return Task.CompletedTask;
            };

            _connection.On<ChatMessage>("ReceiveMessage", (message) =>
            {
                OnMessageReceived?.Invoke(message);
            });

            _connection.On<int, string, string>("UserOnline", (userId, username, displayName) =>
            {
                OnUserOnline?.Invoke(userId, username, displayName);
            });

            _connection.On<int>("UserOffline", (userId) =>
            {
                OnUserOffline?.Invoke(userId);
            });

            _connection.On<WebRTCOffer>("ReceiveOffer", (offer) =>
            {
                OnOfferReceived?.Invoke(offer);
            });

            _connection.On<WebRTCAnswer>("ReceiveAnswer", (answer) =>
            {
                OnAnswerReceived?.Invoke(answer);
            });

            _connection.On<IceCandidate>("ReceiveIceCandidate", (candidate) =>
            {
                OnIceCandidateReceived?.Invoke(candidate);
            });

            _connection.On<FileTransferRequest>("FileTransferRequest", (request) =>
            {
                OnFileTransferRequest?.Invoke(request);
            });

            _connection.On("SocialDataChanged", () =>
            {
                OnSocialDataChanged?.Invoke();
            });

            _connection.On<FriendRequestNotification>("FriendRequestReceived", (notification) =>
            {
                OnFriendRequestReceived?.Invoke(notification);
            });

            _connection.On<FileTransferAvailableNotification>("FileTransferAvailable", (notification) =>
            {
                OnFileTransferAvailable?.Invoke(notification);
            });

            _connection.Closed += async error =>
            {
                OnConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
                OnDisconnected?.Invoke();
                await Task.Delay(new Random().Next(0, 5) * 1000);
            };

            try
            {
                await _connection.StartAsync();
                OnConnectionStateChanged?.Invoke(HubConnectionState.Connected);
                OnConnected?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
                OnConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
                if (_connection != null)
                {
                    try
                    {
                        await _connection.DisposeAsync();
                    }
                    catch
                    {
                        // ignore
                    }

                    _connection = null;
                }
            }
        }

        public async Task DisconnectAsync()
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
                _connection = null;
            }
        }

        public async Task SendMessageAsync(int receiverId, string content)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("SendMessage", receiverId, content);
            }
        }

        public async Task SendOfferAsync(int receiverId, string offer)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("SendOffer", receiverId, offer);
            }
        }

        public async Task SendAnswerAsync(int receiverId, string answer)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("SendAnswer", receiverId, answer);
            }
        }

        public async Task SendIceCandidateAsync(int receiverId, string candidate)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("SendIceCandidate", receiverId, candidate);
            }
        }

        public async Task InitiateFileTransferAsync(int receiverId, string fileName, long fileSize)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("InitiateFileTransfer", receiverId, fileName, fileSize);
            }
        }

        public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    }
}
