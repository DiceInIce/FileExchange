namespace FileShareServer.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public bool IsOnline { get; set; }
    }

    public class UserDetailDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public bool IsOnline { get; set; }
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public UserDetailDto User { get; set; } = new();
    }

    public class FriendRequestNotificationDto
    {
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
    }
}
