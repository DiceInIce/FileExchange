namespace FileShareClient.Models;

public sealed class ToastItem
{
    public string Id { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string Kind { get; init; } = "info";
}
