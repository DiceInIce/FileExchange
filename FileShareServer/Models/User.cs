namespace FileShareServer.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsOnline { get; set; }
        public string? ConnectionId { get; set; }

        public ICollection<Friendship> InitiatedFriendships { get; set; } = new List<Friendship>();
        public ICollection<Friendship> ReceivedFriendships { get; set; } = new List<Friendship>();
        public ICollection<ChatMessage> SentMessages { get; set; } = new List<ChatMessage>();
        public ICollection<ChatMessage> ReceivedMessages { get; set; } = new List<ChatMessage>();
    }

    public class Friendship
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int FriendId { get; set; }
        public User? Friend { get; set; }
        public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum FriendshipStatus
    {
        Pending,
        Accepted,
        Blocked,
        Rejected
    }

    public class ChatMessage
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public User? Sender { get; set; }
        public int ReceiverId { get; set; }
        public User? Receiver { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }
        public MessageType Type { get; set; } = MessageType.Text;
    }

    public enum MessageType
    {
        Text,
        File,
        System
    }

    public class FileTransfer
    {
        public int Id { get; set; }
        public int InitiatorId { get; set; }
        public User? Initiator { get; set; }
        public int RecipientId { get; set; }
        public User? Recipient { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public TransferStatus Status { get; set; } = TransferStatus.Pending;
        public string? WebRtcConnectionId { get; set; }
    }

    public enum TransferStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }
}
