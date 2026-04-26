namespace FileShareClient.Models
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public User User { get; set; } = new();
    }
}
