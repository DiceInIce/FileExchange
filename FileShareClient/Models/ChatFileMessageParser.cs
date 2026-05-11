namespace FileShareClient.Models;

/// <summary>Содержимое сообщения типа «файл» (формат FILE|… в <see cref="ChatMessage.Content"/>).</summary>
public sealed class ChatParsedFileMessage
{
    public int TransferId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string Source { get; init; } = "server";
    public string Token { get; init; } = "-";
}

public static class ChatFileMessageParser
{
    public static string NormalizeP2pToken(string? token) =>
        string.IsNullOrWhiteSpace(token) ? "-" : token.Trim();

    public static bool TryParse(ChatMessage message, out ChatParsedFileMessage? meta)
    {
        meta = null;

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
            var tokenRaw = parts.Length >= 6 ? parts[5] : "-";
            meta = new ChatParsedFileMessage
            {
                TransferId = transferId,
                FileName = fileName,
                FileSize = fileSize,
                Source = source,
                Token = NormalizeP2pToken(tokenRaw)
            };
            return true;
        }
        catch
        {
            return false;
        }
    }
}
