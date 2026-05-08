using FileShareClient.Models;
using FileShareClient.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace FileShareClient.Pages;

public partial class Chat : IAsyncDisposable
{
    [Inject] public NavigationManager Navigation { get; set; } = null!;
    [Inject] public ApiService ApiService { get; set; } = null!;
    [Inject] public ChatService ChatService { get; set; } = null!;
    [Inject] public FileSaveService FileSaveService { get; set; } = null!;
    [Inject] public IJSRuntime JS { get; set; } = null!;

    private User? CurrentUser;
    private User? SelectedFriend;
    private List<User> Friends = new();
    private List<Friendship> PendingRequests = new();
    private HashSet<int> SentRequestUserIds = new();
    private List<ChatMessage> Messages = new();
    private List<User> SearchResults = new();
    private Dictionary<int, int> UnreadCounts = new();
    private Dictionary<string, (byte[] Data, string FileName)> P2pFileCache = new();
    private string MessageInput = "";
    private string SearchQuery = "";
    private string FileTransferStatus = "";
    private string FileSendMode = "auto";
    private bool IsDraggingFile;
    private DotNetObjectReference<Chat>? _dropRef;
    private DotNetObjectReference<Chat>? _peerRef;
    private bool _dropZoneInitialized;
    private bool _scrollToBottomRequested;
    
    // Download progress tracking
    private DownloadProgress? CurrentDownloadProgress;
    private bool IsDownloading;
    private string CurrentDownloadFileName = "";

    protected override async Task OnInitializedAsync()
    {
        if (!ApiService.IsAuthenticated || ApiService.CurrentUser == null)
        {
            Navigation.NavigateTo("/");
            return;
        }

        CurrentUser = ApiService.CurrentUser;
        await LoadInitialData();

        ChatService.OnMessageReceived += HandleMessageReceived;
        ChatService.OnUserOnline += HandleUserOnline;
        ChatService.OnUserOffline += HandleUserOffline;
        ChatService.OnSocialDataChanged += HandleSocialDataChanged;
        ChatService.OnFileTransferRequest += HandleFileTransferRequest;
        ChatService.OnFriendRequestReceived += HandleFriendRequestReceived;
        ChatService.OnFileTransferAvailable += HandleFileTransferAvailable;
        ChatService.OnOfferReceived += HandleOfferReceived;
        ChatService.OnAnswerReceived += HandleAnswerReceived;
        ChatService.OnIceCandidateReceived += HandleIceCandidateReceived;
        ChatService.OnConnected += HandleRealtimeConnected;

        if (!ChatService.IsConnected && ApiService.Token != null)
        {
            await ChatService.ConnectAsync(ApiService.ServerUrl, ApiService.Token);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dropRef = DotNetObjectReference.Create(this);
            _peerRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("peerTransfer.init", _peerRef);
        }

        if (_dropRef == null)
        {
            return;
        }

        if (SelectedFriend != null && !_dropZoneInitialized)
        {
            await JS.InvokeVoidAsync("chatFileDrop.initDropZone", "chat-drop-zone", _dropRef);
            _dropZoneInitialized = true;
        }
        else if (SelectedFriend == null && _dropZoneInitialized)
        {
            await JS.InvokeVoidAsync("chatFileDrop.disposeDropZone", "chat-drop-zone");
            _dropZoneInitialized = false;
        }

        if (_scrollToBottomRequested)
        {
            _scrollToBottomRequested = false;
            await JS.InvokeVoidAsync("chatFileDrop.scrollToBottom", "chat-messages-scroll");
        }
    }

    private async Task LoadInitialData()
    {
        try
        {
            Friends = await ApiService.GetFriendsAsync();
            PendingRequests = await ApiService.GetPendingRequestsAsync();
            var sentRequests = await ApiService.GetSentRequestsAsync();
            SentRequestUserIds = sentRequests
                .Where(r => r.UserId > 0)
                .Select(r => r.UserId)
                .ToHashSet();
            await RefreshUnreadCounts();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading data: {ex.Message}");
        }
    }

    private async Task SelectFriend(User friend)
    {
        SelectedFriend = friend;
        Messages = await ApiService.GetConversationAsync(friend.Id);
        await MarkFriendMessagesAsRead(friend.Id);
        MessageInput = "";
        _scrollToBottomRequested = true;
    }

    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageInput) || SelectedFriend == null || !ChatService.IsConnected)
            return;

        if (!Friends.Any(f => f.Id == SelectedFriend.Id))
        {
            SelectedFriend = null;
            Messages.Clear();
            FileTransferStatus = "Нельзя отправить сообщение: пользователь удален из друзей.";
            StateHasChanged();
            return;
        }

        var content = MessageInput;
        MessageInput = "";

        var sentViaP2P = false;
        try
        {
            sentViaP2P = await JS.InvokeAsync<bool>("peerTransfer.sendText", SelectedFriend.Id, content);
        }
        catch
        {
            sentViaP2P = false;
        }

        if (sentViaP2P)
        {
            _ = ApiService.StoreMessageAsync(SelectedFriend.Id, content);
        }
        else
        {
            await ChatService.SendMessageAsync(SelectedFriend.Id, content);
        }
        
        Messages.Add(new ChatMessage
        {
            SenderId = CurrentUser?.Id ?? 0,
            SenderName = CurrentUser?.Username,
            Content = content,
            Timestamp = DateTime.Now,
            IsRead = true,
            Type = 0
        });

        _scrollToBottomRequested = true;
        StateHasChanged();
    }

    private void HandleMessageReceived(ChatMessage message)
    {
        _ = LoadInitialData();
        if (SelectedFriend?.Id == message.SenderId)
        {
            Messages.Add(message);
            _ = MarkSingleMessageAsRead(message.Id);
            _scrollToBottomRequested = true;
            _ = InvokeAsync(StateHasChanged);
        }
        else
        {
            if (!UnreadCounts.ContainsKey(message.SenderId))
            {
                UnreadCounts[message.SenderId] = 0;
            }
            UnreadCounts[message.SenderId]++;
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private void HandleSocialDataChanged()
    {
        _ = InvokeAsync(async () =>
        {
            await LoadInitialData();
            if (SelectedFriend != null)
            {
                var stillFriend = Friends.Any(f => f.Id == SelectedFriend.Id);
                if (!stillFriend)
                {
                    SelectedFriend = null;
                    Messages.Clear();
                    FileTransferStatus = "Пользователь больше не в друзьях. Чат закрыт.";
                }
                else
                {
                    Messages = await ApiService.GetConversationAsync(SelectedFriend.Id);
                    _scrollToBottomRequested = true;
                }
            }
            StateHasChanged();
        });
    }

    private void HandleFileTransferRequest(FileTransferRequest request)
    {
        FileTransferStatus = $"Входящий запрос на файл от {request.SenderName}: {request.FileName} ({request.FileSize} байт)";
        _ = InvokeAsync(StateHasChanged);
    }

    private void HandleFriendRequestReceived(FriendRequestNotification notification)
    {
        FileTransferStatus = $"Новая заявка в друзья от {notification.SenderName}";
        _ = InvokeAsync(StateHasChanged);
    }

    private void HandleFileTransferAvailable(FileTransferAvailableNotification notification)
    {
        FileTransferStatus = $"Получен файл от {notification.SenderName}: {notification.FileName}";
        _ = InvokeAsync(async () =>
        {
            if (SelectedFriend != null)
            {
                Messages = await ApiService.GetConversationAsync(SelectedFriend.Id);
                _scrollToBottomRequested = true;
            }
            StateHasChanged();
        });
    }

    private void HandleUserOnline(int userId, string username, string displayName)
    {
        var friend = Friends.FirstOrDefault(f => f.Id == userId);
        if (friend != null)
        {
            friend.IsOnline = true;
            _ = InvokeAsync(async () =>
            {
                await LoadInitialData();
                StateHasChanged();
            });
        }
    }

    private void HandleUserOffline(int userId)
    {
        var friend = Friends.FirstOrDefault(f => f.Id == userId);
        if (friend != null)
        {
            friend.IsOnline = false;
            _ = InvokeAsync(async () =>
            {
                await LoadInitialData();
                StateHasChanged();
            });
        }
    }

    private async Task SendFriendRequest(int userId)
    {
        if (await ApiService.SendFriendRequestAsync(userId))
        {
            SentRequestUserIds.Add(userId);
            FileTransferStatus = "Заявка в друзья отправлена.";
            StateHasChanged();
        }
        else
        {
            FileTransferStatus = "Не удалось отправить заявку в друзья.";
            StateHasChanged();
        }
    }

    private Task HandleSearchItemClick(int userId, bool canSendRequest)
    {
        if (!canSendRequest)
        {
            return Task.CompletedTask;
        }

        return SendFriendRequest(userId);
    }

    private async Task HandleMessageKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SendMessage();
        }
    }

    private async Task AcceptFriend(int userId)
    {
        if (await ApiService.AcceptFriendRequestAsync(userId))
        {
            await LoadInitialData();
            StateHasChanged();
        }
    }

    private async Task RejectFriend(int userId)
    {
        if (await ApiService.RejectFriendRequestAsync(userId))
        {
            await LoadInitialData();
            StateHasChanged();
        }
    }

    private async Task RemoveFriend()
    {
        if (SelectedFriend != null && await ApiService.RemoveFriendAsync(SelectedFriend.Id))
        {
            Friends.Remove(SelectedFriend);
            SelectedFriend = null;
            StateHasChanged();
        }
    }

    private async Task PerformSearch()
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults = await ApiService.SearchUsersAsync(SearchQuery);
            StateHasChanged();
        }
    }

    private async Task OnFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        await using var stream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024);
        await UploadFile(stream, file.Name);
    }

    [JSInvokable]
    public async Task HandleDroppedFile(string fileName, string base64, long size)
    {
        IsDraggingFile = false;
        // ✅ Увеличен лимит до 100 МБ в соответствии с серверным ограничением
        if (size > 100 * 1024 * 1024)
        {
            FileTransferStatus = "Файл слишком большой. Лимит 100 МБ.";
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            // ✅ Эффективная конвертация base64 с меньшим оверхедом памяти
            var bytes = Convert.FromBase64String(base64);
            await using var stream = new MemoryStream(bytes, writable: false); // read-only stream
            await UploadFile(stream, fileName);
            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            FileTransferStatus = "Не удалось обработать перетащенный файл.";
            await InvokeAsync(StateHasChanged);
        }
    }

    [JSInvokable]
    public Task SetDraggingState(bool isDragging)
    {
        IsDraggingFile = isDragging;
        return InvokeAsync(StateHasChanged);
    }

    private async Task UploadFile(Stream stream, string fileName)
    {
        if (SelectedFriend == null)
        {
            FileTransferStatus = "Сначала выберите друга.";
            return;
        }

        if (!ChatService.IsConnected)
        {
            FileTransferStatus = "Нет подключения к чату.";
            return;
        }

        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        var fileBytes = memory.ToArray();
        var base64 = Convert.ToBase64String(fileBytes);
        var mode = (FileSendMode ?? "auto").ToLowerInvariant();

        if (mode == "server")
        {
            await using var serverOnlyStream = new MemoryStream(fileBytes);
            var (serverOnlySuccess, serverOnlyError) = await ApiService.UploadFileAsync(SelectedFriend.Id, serverOnlyStream, fileName);
            if (!serverOnlySuccess)
            {
                FileTransferStatus = serverOnlyError;
                return;
            }

            FileTransferStatus = $"SERVER: файл '{fileName}' отправлен пользователю {SelectedFriend.DisplayName}.";
            return;
        }

        try
        {
            var p2pResult = await JS.InvokeAsync<PeerSendFileResult>("peerTransfer.sendFile", SelectedFriend.Id, fileName, base64, fileBytes.LongLength);
            if (p2pResult.Success)
            {
                // Кэшируем файл для самого отправителя, чтобы он мог его скачать
                P2pFileCache[p2pResult.Token] = (fileBytes, fileName);
                await ApiService.StoreFileMessageAsync(SelectedFriend.Id, fileName, fileBytes.LongLength, "p2p", p2pResult.Token);
                FileTransferStatus = $"P2P: файл '{fileName}' отправлен пользователю {SelectedFriend.DisplayName}.";
                return;
            }
        }
        catch
        {
            if (mode == "p2p")
            {
                FileTransferStatus = "P2P-only режим: не удалось установить прямое соединение.";
                return;
            }
        }

        if (mode == "p2p")
        {
            FileTransferStatus = "P2P-only режим: прямое соединение недоступно.";
            return;
        }

        await using var apiStream = new MemoryStream(fileBytes);
        var (success, error) = await ApiService.UploadFileAsync(SelectedFriend.Id, apiStream, fileName);
        if (!success)
        {
            FileTransferStatus = error;
            return;
        }

        FileTransferStatus = $"SERVER: файл '{fileName}' отправлен пользователю {SelectedFriend.DisplayName}.";
    }

    private void HandleOfferReceived(WebRTCOffer offer)
    {
        _ = InvokeAsync(async () =>
        {
            if (!string.IsNullOrWhiteSpace(offer.Offer))
            {
                await JS.InvokeVoidAsync("peerTransfer.onOffer", offer.SenderId, offer.Offer);
            }
        });
    }

    private void HandleAnswerReceived(WebRTCAnswer answer)
    {
        _ = InvokeAsync(async () =>
        {
            if (!string.IsNullOrWhiteSpace(answer.Answer))
            {
                await JS.InvokeVoidAsync("peerTransfer.onAnswer", answer.SenderId, answer.Answer);
            }
        });
    }

    private void HandleIceCandidateReceived(IceCandidate candidate)
    {
        _ = InvokeAsync(async () =>
        {
            if (!string.IsNullOrWhiteSpace(candidate.Candidate))
            {
                await JS.InvokeVoidAsync("peerTransfer.onIceCandidate", candidate.SenderId, candidate.Candidate);
            }
        });
    }

    [JSInvokable]
    public Task OnPeerTextMessage(int senderId, string content)
    {
        if (SelectedFriend?.Id != senderId)
        {
            if (!UnreadCounts.ContainsKey(senderId))
            {
                UnreadCounts[senderId] = 0;
            }
            UnreadCounts[senderId]++;
            return InvokeAsync(StateHasChanged);
        }

        Messages.Add(new ChatMessage
        {
            SenderId = senderId,
            SenderName = SelectedFriend.DisplayName,
            Content = content,
            Timestamp = DateTime.Now,
            IsRead = false,
            Type = 0
        });
        _scrollToBottomRequested = true;
        return InvokeAsync(StateHasChanged);
    }

    private async Task DownloadFileFromMessage(ChatMessage message)
    {
        if (!TryParseFileMessage(message, out var fileMeta))
        {
            FileTransferStatus = "Некорректные данные файла в сообщении.";
            return;
        }

        if (fileMeta.Source == "p2p")
        {
            if (!P2pFileCache.TryGetValue(fileMeta.Token, out var cached))
            {
                FileTransferStatus = "Этот P2P-файл недоступен локально (возможно, после перезапуска).";
                return;
            }

            var p2pSaved = FileSaveService.SaveBytes(cached.Data, cached.FileName);
            FileTransferStatus = p2pSaved
                ? $"P2P: файл '{cached.FileName}' сохранен."
                : "Сохранение файла отменено.";
            return;
        }

        IsDownloading = true;
        CurrentDownloadFileName = fileMeta.FileName;
        CurrentDownloadProgress = new DownloadProgress { TotalBytes = fileMeta.FileSize };
        StateHasChanged();

        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                CurrentDownloadProgress = p;
                _ = InvokeAsync(StateHasChanged);
            });

            var (success, data, fileName, error) = await ApiService.DownloadFileAsync(fileMeta.TransferId, progress);
            
            if (!success || data == null)
            {
                FileTransferStatus = error;
                return;
            }

            var saved = FileSaveService.SaveBytes(data, fileName);
            FileTransferStatus = saved
                ? $"SERVER: файл '{fileName}' сохранен."
                : "Сохранение файла отменено.";
        }
        finally
        {
            IsDownloading = false;
            CurrentDownloadProgress = null;
            CurrentDownloadFileName = "";
            StateHasChanged();
        }
    }

    private static bool TryParseFileMessage(ChatMessage message, out ParsedFileMessage fileMeta)
    {
        fileMeta = new ParsedFileMessage();

        if (message.Type != 1 || string.IsNullOrWhiteSpace(message.Content))
        {
            return false;
        }

        var parts = message.Content.Split('|');
        if (parts.Length < 4 || parts[0] != "FILE")
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var transferId))
        {
            return false;
        }

        if (!long.TryParse(parts[3], out var fileSize))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(parts[2]);
            var fileName = System.Text.Encoding.UTF8.GetString(bytes);
            var source = parts.Length >= 5 && !string.IsNullOrWhiteSpace(parts[4]) ? parts[4].ToLowerInvariant() : "server";
            var token = parts.Length >= 6 ? parts[5] : "-";
            fileMeta = new ParsedFileMessage
            {
                TransferId = transferId,
                FileName = fileName,
                FileSize = fileSize,
                Source = source,
                Token = string.IsNullOrWhiteSpace(token) ? "-" : token
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class ParsedFileMessage
    {
        public int TransferId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Source { get; set; } = "server";
        public string Token { get; set; } = "-";
    }

    private sealed class PeerSendFileResult
    {
        public bool Success { get; set; }
        public string Token { get; set; } = "-";
    }

    private void HandleRealtimeConnected()
    {
        _ = InvokeAsync(async () =>
        {
            await LoadInitialData();
            if (SelectedFriend != null)
            {
                Messages = await ApiService.GetConversationAsync(SelectedFriend.Id);
                _scrollToBottomRequested = true;
            }
            StateHasChanged();
        });
    }

    [JSInvokable]
    public async Task SendOfferToPeer(int receiverId, string offer)
    {
        await ChatService.SendOfferAsync(receiverId, offer);
    }

    [JSInvokable]
    public async Task SendAnswerToPeer(int receiverId, string answer)
    {
        await ChatService.SendAnswerAsync(receiverId, answer);
    }

    [JSInvokable]
    public async Task SendIceCandidateToPeer(int receiverId, string candidate)
    {
        await ChatService.SendIceCandidateAsync(receiverId, candidate);
    }

    [JSInvokable]
    public Task OnPeerTransferStatus(string status)
    {
        FileTransferStatus = status;
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task OnPeerFileReceived(int senderId, string fileName, string base64, long size, string token)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var normalizedToken = string.IsNullOrWhiteSpace(token) ? "-" : token;
            P2pFileCache[normalizedToken] = (bytes, fileName);
            FileTransferStatus = $"P2P: получен файл '{fileName}' ({size} байт).";
        }
        catch
        {
            FileTransferStatus = "P2P: не удалось обработать полученный файл.";
        }

        if (SelectedFriend?.Id == senderId)
        {
            _ = InvokeAsync(async () =>
            {
                Messages = await ApiService.GetConversationAsync(senderId);
                _scrollToBottomRequested = true;
                StateHasChanged();
            });
        }

        return InvokeAsync(StateHasChanged);
    }

    private void Logout()
    {
        ApiService.ClearSession();
        Navigation.NavigateTo("/");
    }

    private async Task RefreshUnreadCounts()
    {
        var unreadMessages = await ApiService.GetUnreadMessagesAsync();
        UnreadCounts = unreadMessages
            .GroupBy(m => m.SenderId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task MarkFriendMessagesAsRead(int friendId)
    {
        var unreadMessages = await ApiService.GetUnreadMessagesAsync();
        var toMark = unreadMessages.Where(m => m.SenderId == friendId && m.Id > 0).ToList();
        foreach (var msg in toMark)
        {
            await ApiService.MarkAsReadAsync(msg.Id);
        }

        UnreadCounts[friendId] = 0;
        await RefreshUnreadCounts();
        await InvokeAsync(StateHasChanged);
    }

    private async Task MarkSingleMessageAsRead(int messageId)
    {
        if (messageId <= 0)
        {
            return;
        }
        await ApiService.MarkAsReadAsync(messageId);
    }

    public async ValueTask DisposeAsync()
    {
        ChatService.OnMessageReceived -= HandleMessageReceived;
        ChatService.OnUserOnline -= HandleUserOnline;
        ChatService.OnUserOffline -= HandleUserOffline;
        ChatService.OnSocialDataChanged -= HandleSocialDataChanged;
        ChatService.OnFileTransferRequest -= HandleFileTransferRequest;
        ChatService.OnFriendRequestReceived -= HandleFriendRequestReceived;
        ChatService.OnFileTransferAvailable -= HandleFileTransferAvailable;
        ChatService.OnOfferReceived -= HandleOfferReceived;
        ChatService.OnAnswerReceived -= HandleAnswerReceived;
        ChatService.OnIceCandidateReceived -= HandleIceCandidateReceived;
        ChatService.OnConnected -= HandleRealtimeConnected;

        if (ChatService.IsConnected)
        {
            await ChatService.DisconnectAsync();
        }

        if (_dropRef != null)
        {
            await JS.InvokeVoidAsync("chatFileDrop.disposeDropZone", "chat-drop-zone");
            _dropRef.Dispose();
        }

        if (_peerRef != null)
        {
            await JS.InvokeVoidAsync("peerTransfer.dispose");
            _peerRef.Dispose();
        }
    }
}
