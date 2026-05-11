using FileShareClient.Models;
using FileShareClient.Services;
using Microsoft.AspNetCore.Components;

namespace FileShareClient.Pages;

public partial class Chat
{
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
                AddToast(FileTransferStatus, "error");
                return;
            }

            var total = cached.Data.LongLength;
            IsDownloading = true;
            DownloadIndicatorIsP2pLocal = true;
            CurrentDownloadFileName = cached.FileName;
            CurrentDownloadProgress = new DownloadProgress
            {
                BytesReceived = total,
                TotalBytes = total,
                SpeedBytesPerSecond = 0,
                ElapsedTime = TimeSpan.Zero,
                EstimatedTimeRemaining = TimeSpan.Zero
            };
            await InvokeAsync(StateHasChanged);

            try
            {
                var p2pSaved = FileSaveService.SaveBytes(cached.Data, cached.FileName);
                FileTransferStatus = p2pSaved
                    ? $"P2P: файл '{cached.FileName}' сохранен."
                    : "Сохранение файла отменено.";
                AddToast(FileTransferStatus, p2pSaved ? "success" : "info");
            }
            finally
            {
                IsDownloading = false;
                DownloadIndicatorIsP2pLocal = false;
                CurrentDownloadProgress = null;
                CurrentDownloadFileName = "";
                StateHasChanged();
            }

            return;
        }

        DownloadIndicatorIsP2pLocal = false;
        IsDownloading = true;
        CurrentDownloadFileName = fileMeta.FileName;
        CurrentDownloadProgress = new DownloadProgress { TotalBytes = fileMeta.FileSize };
        await InvokeAsync(StateHasChanged);

        try
        {
            var (success, data, fileName, error) = await ApiService.DownloadFileAsync(
                fileMeta.TransferId,
                progress: null,
                contentLengthFallback: fileMeta.FileSize,
                progressUiAsync: async p =>
                {
                    CurrentDownloadProgress = p;
                    await InvokeAsync(StateHasChanged);
                });

            if (!success || data == null)
            {
                FileTransferStatus = error;
                AddToast(FileTransferStatus, "error");
                return;
            }

            var saved = FileSaveService.SaveBytes(data, fileName);
            FileTransferStatus = saved
                ? $"SERVER: файл '{fileName}' сохранен."
                : "Сохранение файла отменено.";
            AddToast(FileTransferStatus, saved ? "success" : "info");
        }
        finally
        {
            IsDownloading = false;
            DownloadIndicatorIsP2pLocal = false;
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
            var tokenRaw = parts.Length >= 6 ? parts[5] : "-";
            fileMeta = new ParsedFileMessage
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

    private sealed class ParsedFileMessage
    {
        public int TransferId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Source { get; set; } = "server";
        public string Token { get; set; } = "-";
    }
}
