using FileShareClient.Models;
using FileShareClient.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
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
    private readonly object _p2pInboundLock = new();
    private readonly Dictionary<string, MemoryStream> _p2pInboundStreams = new();
    private string MessageInput = "";
    private string SearchQuery = "";
    private string FileTransferStatus = "";
    private string FileSendMode = "auto";
    /// <summary>Лимит размера только для загрузки файла на сервер (режим SERVER).</summary>
    private const long ServerUploadMaxBytes = 100L * 1024 * 1024;
    private string MessageSearchQuery = "";
    private string MessageDeliveryStatus = "";
    private bool IsSendingMessage;
    private bool IsDraggingFile;
    private DotNetObjectReference<Chat>? _dropRef;
    private DotNetObjectReference<Chat>? _peerRef;
    private bool _dropZoneInitialized;
    private bool _scrollObserverInitialized;
    private bool _scrollToBottomRequested;
    private bool _isNearBottom = true;
    private bool ShowScrollToBottomButton;
    private bool IsInitialLoading = true;
    private List<ToastItem> Toasts = new();
    private HubConnectionState _hubConnectionState = HubConnectionState.Disconnected;

    // Download progress tracking
    private DownloadProgress? CurrentDownloadProgress;
    private bool IsDownloading;
    private bool DownloadIndicatorIsP2pLocal;
    private string CurrentDownloadFileName = "";

    // P2P data channel: отправка / приём файла (прогресс из peerTransfer.js)
    private bool ShowP2pChannelProgress;
    private bool P2pChannelProgressIsOutgoing;
    private string P2pChannelProgressPhase = "";
    private string P2pChannelProgressFileName = "";
    private DownloadProgress? P2pChannelProgress;
    private IEnumerable<User> FilteredFriends =>
        FriendFilter switch
        {
            "online" => Friends.Where(f => f.IsOnline),
            "unread" => Friends.Where(f => UnreadCounts.TryGetValue(f.Id, out var unread) && unread > 0),
            _ => Friends
        };
    private IEnumerable<ChatMessage> VisibleMessages =>
        string.IsNullOrWhiteSpace(MessageSearchQuery)
            ? Messages
            : Messages.Where(m => m.Content?.Contains(MessageSearchQuery, StringComparison.OrdinalIgnoreCase) == true);
    private string FriendFilter = "all";

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
        ChatService.OnConnectionStateChanged += HandleConnectionStateChanged;

        if (!ChatService.IsConnected && ApiService.Token != null)
        {
            await ChatService.ConnectAsync(ApiService.ServerUrl, ApiService.Token);
        }

        _hubConnectionState = ChatService.GetConnectionState();
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

        if (SelectedFriend != null && !_scrollObserverInitialized)
        {
            await JS.InvokeVoidAsync("chatFileDrop.initScrollObserver", "chat-messages-scroll", _dropRef);
            _scrollObserverInitialized = true;
        }
        else if (SelectedFriend == null && _scrollObserverInitialized)
        {
            await JS.InvokeVoidAsync("chatFileDrop.disposeScrollObserver", "chat-messages-scroll");
            _scrollObserverInitialized = false;
        }
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
        ChatService.OnConnectionStateChanged -= HandleConnectionStateChanged;

        if (ChatService.IsConnected)
        {
            await ChatService.DisconnectAsync();
        }

        if (_dropRef != null)
        {
            await JS.InvokeVoidAsync("chatFileDrop.disposeDropZone", "chat-drop-zone");
            await JS.InvokeVoidAsync("chatFileDrop.disposeScrollObserver", "chat-messages-scroll");
            _dropRef.Dispose();
        }

        if (_peerRef != null)
        {
            lock (_p2pInboundLock)
            {
                foreach (var kv in _p2pInboundStreams)
                {
                    kv.Value.Dispose();
                }

                _p2pInboundStreams.Clear();
            }

            await JS.InvokeVoidAsync("peerTransfer.dispose");
            _peerRef.Dispose();
        }
    }
}
