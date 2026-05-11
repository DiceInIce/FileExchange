using FileShareClient.Models;
using FileShareClient.Services;
using Microsoft.AspNetCore.Components;

namespace FileShareClient.Pages;

public partial class Chat
{
    private async Task DownloadFileFromMessage(ChatMessage message)
    {
        if (!ChatFileMessageParser.TryParse(message, out var fileMeta) || fileMeta is null)
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
}
