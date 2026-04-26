namespace FileShareServer.DTOs
{
    public record RegisterRequest(string Username, string Email, string Password);
    public record LoginRequest(string Username, string Password);
    public record StoreMessageRequest(string Content);
    public record StoreFileMessageRequest(string FileName, long FileSize, string Source, string? Token);
}
