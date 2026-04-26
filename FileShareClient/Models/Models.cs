namespace FileShareClient.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public bool IsOnline { get; set; }
    }

    public class ChatMessage
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public string? SenderName { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public int Type { get; set; }
    }

    public class Friendship
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class WebRTCOffer
    {
        public int SenderId { get; set; }
        public string? SenderName { get; set; }
        public string? Offer { get; set; }
    }

    public class WebRTCAnswer
    {
        public int SenderId { get; set; }
        public string? Answer { get; set; }
    }

    public class IceCandidate
    {
        public int SenderId { get; set; }
        public string? Candidate { get; set; }
    }

    public class FileTransferRequest
    {
        public int SenderId { get; set; }
        public string? SenderName { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    public class FriendRequestNotification
    {
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
    }

    public class IncomingFile
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
    }

    public class FileTransferAvailableNotification
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }
}
