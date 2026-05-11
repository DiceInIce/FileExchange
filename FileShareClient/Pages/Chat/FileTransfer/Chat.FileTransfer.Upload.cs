using System.Text.Json.Serialization;
using FileShareClient.Models;
using FileShareClient.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace FileShareClient.Pages;

public partial class Chat
{
    private long MaxFileReadBytesForCurrentSendMode() =>
        (FileSendMode ?? "auto").Equals("server", StringComparison.OrdinalIgnoreCase)
            ? ServerUploadMaxBytes
            : 1L << 40;

    private async Task OnFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        await using var stream = file.OpenReadStream(maxAllowedSize: MaxFileReadBytesForCurrentSendMode());
        await UploadFile(stream, file.Name);
    }

    [JSInvokable]
    public async Task HandleDroppedFile(string fileName, byte[] fileBytes, long size)
    {
        IsDraggingFile = false;
        var mode = (FileSendMode ?? "auto").ToLowerInvariant();
        if (mode == "server" && size > ServerUploadMaxBytes)
        {
            FileTransferStatus = "В режиме SERVER максимум 100 МБ. Для больших файлов выберите P2P или AUTO.";
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            await using var stream = new MemoryStream(fileBytes, writable: false); // read-only stream
            await UploadFile(stream, fileName);
            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            FileTransferStatus = "Не удалось обработать перетащенный файл.";
            await InvokeAsync(StateHasChanged);
        }
    }

    [JSInvokable]
    public Task SetDraggingState(bool isDragging)
    {
        IsDraggingFile = isDragging;
        return InvokeAsync(StateHasChanged);
    }

    private async Task UploadFile(Stream stream, string fileName)
    {
        if (SelectedFriend == null)
        {
            FileTransferStatus = "Сначала выберите друга.";
            return;
        }

        if (!ChatService.IsConnected)
        {
            FileTransferStatus = "Нет подключения к чату.";
            return;
        }

        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        var fileLength = memory.Length;
        var mode = (FileSendMode ?? "auto").ToLowerInvariant();

        if (mode == "server" && fileLength > ServerUploadMaxBytes)
        {
            FileTransferStatus = "В режиме SERVER максимальный размер файла — 100 МБ. Для больших файлов переключите режим на P2P или AUTO.";
            AddToast(FileTransferStatus, "error");
            return;
        }

        if (mode == "server")
        {
            var srvBytes = memory.ToArray();
            await using var serverOnlyStream = new MemoryStream(srvBytes);
            var (serverOnlySuccess, serverOnlyError) = await ApiService.UploadFileAsync(SelectedFriend.Id, serverOnlyStream, fileName);
            if (!serverOnlySuccess)
            {
                FileTransferStatus = serverOnlyError;
                AddToast("Ошибка отправки файла. Можно повторить.", "error");
                return;
            }

            FileTransferStatus = $"SERVER: файл '{fileName}' отправлен пользователю {SelectedFriend.DisplayName}.";
            AddToast(FileTransferStatus, "success");
            return;
        }

        try
        {
            ShowP2pChannelProgress = true;
            P2pChannelProgressIsOutgoing = true;
            P2pChannelProgressFileName = fileName;
            P2pChannelProgressPhase = "connecting";
            P2pChannelProgress = new DownloadProgress
            {
                BytesReceived = 0,
                TotalBytes = fileLength,
                SpeedBytesPerSecond = 0,
                ElapsedTime = TimeSpan.Zero,
                EstimatedTimeRemaining = TimeSpan.Zero
            };
            await InvokeAsync(StateHasChanged);

            try
            {
                memory.Position = 0;
                using var dotnetStreamRef = new DotNetStreamReference(memory, leaveOpen: true);
                var p2pResult = await JS.InvokeAsync<PeerSendFileResult>(
                    "peerTransfer.sendFileStream",
                    SelectedFriend.Id,
                    fileName,
                    fileLength,
                    dotnetStreamRef);
                if (p2pResult.Success)
                {
                    var sentBytes = memory.ToArray();
                    // Кэшируем файл для самого отправителя, чтобы он мог его скачать
                    P2pFileCache[NormalizeP2pToken(p2pResult.Token)] = (sentBytes, fileName);
                    await ApiService.StoreFileMessageAsync(SelectedFriend.Id, fileName, fileLength, "p2p", p2pResult.Token);
                    FileTransferStatus = $"P2P: файл '{fileName}' отправлен пользователю {SelectedFriend.DisplayName}.";
                    AddToast(FileTransferStatus, "success");
                    return;
                }
            }
            finally
            {
                ClearP2pChannelTransferUi();
            }

            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            ClearP2pChannelTransferUi();
            await InvokeAsync(StateHasChanged);
            if (mode == "p2p")
            {
                FileTransferStatus = "P2P-only режим: не удалось установить прямое соединение.";
                AddToast(FileTransferStatus, "error");
                return;
            }
        }

        if (mode == "p2p")
        {
            FileTransferStatus = "P2P-only режим: прямое соединение недоступно.";
            AddToast(FileTransferStatus, "error");
            return;
        }

        AddToast("P2P недоступен, отправляю через сервер...", "info");
        if (fileLength > ServerUploadMaxBytes)
        {
            FileTransferStatus = "P2P недоступен, а файл больше 100 МБ — через сервер его не отправить. Проверьте соединение для P2P или уменьшите файл.";
            AddToast(FileTransferStatus, "error");
            return;
        }

        var fallbackBytes = memory.ToArray();
        await using var apiStream = new MemoryStream(fallbackBytes);
        var (success, error) = await ApiService.UploadFileAsync(SelectedFriend.Id, apiStream, fileName);
        if (!success)
        {
            FileTransferStatus = error;
            AddToast("Ошибка отправки файла через сервер.", "error");
            return;
        }

        FileTransferStatus = $"SERVER: файл '{fileName}' отправлен пользователю {SelectedFriend.DisplayName}.";
        AddToast(FileTransferStatus, "success");
    }

    private sealed class PeerSendFileResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        [JsonPropertyName("token")]
        public string Token { get; set; } = "-";
    }
}
