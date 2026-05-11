using FileShareClient.Models;
using FileShareClient.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FileShareClient.Pages;

public partial class Chat
{
    private void HandleOfferReceived(WebRTCOffer offer)
    {
        _ = InvokeAsync(async () =>
        {
            if (!string.IsNullOrWhiteSpace(offer.Offer))
            {
                await JS.InvokeVoidAsync("peerTransfer.onOffer", offer.SenderId, offer.Offer);
            }
        });
    }

    private void HandleAnswerReceived(WebRTCAnswer answer)
    {
        _ = InvokeAsync(async () =>
        {
            if (!string.IsNullOrWhiteSpace(answer.Answer))
            {
                await JS.InvokeVoidAsync("peerTransfer.onAnswer", answer.SenderId, answer.Answer);
            }
        });
    }

    private void HandleIceCandidateReceived(IceCandidate candidate)
    {
        _ = InvokeAsync(async () =>
        {
            if (!string.IsNullOrWhiteSpace(candidate.Candidate))
            {
                await JS.InvokeVoidAsync("peerTransfer.onIceCandidate", candidate.SenderId, candidate.Candidate);
            }
        });
    }

    [JSInvokable]
    public Task OnPeerTextMessage(int senderId, string content)
    {
        if (SelectedFriend?.Id != senderId)
        {
            if (!UnreadCounts.ContainsKey(senderId))
            {
                UnreadCounts[senderId] = 0;
            }
            UnreadCounts[senderId]++;
            return InvokeAsync(StateHasChanged);
        }

        Messages.Add(new ChatMessage
        {
            SenderId = senderId,
            SenderName = SelectedFriend.DisplayName,
            Content = content,
            Timestamp = DateTime.Now,
            IsRead = false,
            Type = 0
        });
        _scrollToBottomRequested = true;
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task SendOfferToPeer(int receiverId, string offer)
    {
        await ChatService.SendOfferAsync(receiverId, offer);
    }

    [JSInvokable]
    public async Task SendAnswerToPeer(int receiverId, string answer)
    {
        await ChatService.SendAnswerAsync(receiverId, answer);
    }

    [JSInvokable]
    public async Task SendIceCandidateToPeer(int receiverId, string candidate)
    {
        await ChatService.SendIceCandidateAsync(receiverId, candidate);
    }

    [JSInvokable]
    public Task OnPeerTransferStatus(string status)
    {
        FileTransferStatus = status;
        AddToast(status, status.Contains("не", StringComparison.OrdinalIgnoreCase) ? "error" : "info");
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task OnP2pSendProgress(int peerUserId, long sent, long total, string phase)
    {
        if (SelectedFriend?.Id != peerUserId || !ShowP2pChannelProgress || !P2pChannelProgressIsOutgoing)
        {
            return Task.CompletedTask;
        }

        P2pChannelProgressPhase = string.IsNullOrWhiteSpace(phase) ? "sending" : phase;
        var displayTotal = total > 0 ? total : Math.Max(sent, 1L);
        if (displayTotal < sent)
        {
            displayTotal = sent;
        }

        P2pChannelProgress = new DownloadProgress
        {
            BytesReceived = sent,
            TotalBytes = displayTotal,
            SpeedBytesPerSecond = 0,
            ElapsedTime = TimeSpan.Zero,
            EstimatedTimeRemaining = TimeSpan.Zero
        };
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task OnP2pReceiveProgress(int senderId, long received, long total, string fileName)
    {
        if (SelectedFriend?.Id != senderId)
        {
            return Task.CompletedTask;
        }

        ShowP2pChannelProgress = true;
        P2pChannelProgressIsOutgoing = false;
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            P2pChannelProgressFileName = fileName;
        }

        P2pChannelProgressPhase = received <= 0 ? "connecting" : "sending";
        var displayTotal = total > 0 ? total : Math.Max(received, 1L);
        if (displayTotal < received)
        {
            displayTotal = received;
        }

        P2pChannelProgress = new DownloadProgress
        {
            BytesReceived = received,
            TotalBytes = displayTotal,
            SpeedBytesPerSecond = 0,
            ElapsedTime = TimeSpan.Zero,
            EstimatedTimeRemaining = TimeSpan.Zero
        };
        return InvokeAsync(StateHasChanged);
    }

    private void ClearP2pChannelTransferUi()
    {
        ShowP2pChannelProgress = false;
        P2pChannelProgressIsOutgoing = false;
        P2pChannelProgress = null;
        P2pChannelProgressFileName = "";
        P2pChannelProgressPhase = "";
    }

    [JSInvokable]
    public Task OnP2pInboundStart(int senderId, string token, string fileName, long expectedSize)
    {
        var norm = NormalizeP2pToken(token);
        var key = P2pInboundStreamKey(senderId, norm);
        lock (_p2pInboundLock)
        {
            if (_p2pInboundStreams.Remove(key, out var existing))
            {
                existing.Dispose();
            }

            _p2pInboundStreams[key] = new MemoryStream();
        }

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnP2pInboundChunk(int senderId, string token, byte[] chunk)
    {
        if (chunk == null || chunk.Length == 0)
        {
            return Task.CompletedTask;
        }

        var norm = NormalizeP2pToken(token);
        var key = P2pInboundStreamKey(senderId, norm);
        lock (_p2pInboundLock)
        {
            if (!_p2pInboundStreams.TryGetValue(key, out var ms))
            {
                return Task.CompletedTask;
            }

            ms.Write(chunk, 0, chunk.Length);
        }

        return Task.CompletedTask;
    }

    [JSInvokable]
    public async Task OnP2pInboundComplete(int senderId, string token, string fileName, long declaredSize)
    {
        var norm = NormalizeP2pToken(token);
        var key = P2pInboundStreamKey(senderId, norm);
        MemoryStream? ms;
        lock (_p2pInboundLock)
        {
            _p2pInboundStreams.Remove(key, out ms);
        }

        if (SelectedFriend?.Id == senderId)
        {
            ClearP2pChannelTransferUi();
        }

        if (ms == null)
        {
            FileTransferStatus = "P2P: приём прерван (нет буфера).";
            AddToast(FileTransferStatus, "error");
            await InvokeAsync(StateHasChanged);
            return;
        }

        await using (ms)
        {
            if (ms.Length != declaredSize)
            {
                FileTransferStatus = $"P2P: несовпадение размера (ожидали {declaredSize} B, получено {ms.Length} B).";
                AddToast(FileTransferStatus, "error");
                return;
            }

            try
            {
                var bytes = ms.ToArray();
                P2pFileCache[norm] = (bytes, fileName);
                FileTransferStatus = $"P2P: получен файл '{fileName}' ({bytes.Length} байт).";
                AddToast(FileTransferStatus, "success");
            }
            catch
            {
                FileTransferStatus = "P2P: не удалось сохранить файл в память.";
                AddToast(FileTransferStatus, "error");
                return;
            }
        }

        if (SelectedFriend?.Id == senderId)
        {
            _ = InvokeAsync(async () =>
            {
                Messages = await ApiService.GetConversationAsync(senderId);
                if (_isNearBottom)
                {
                    _scrollToBottomRequested = true;
                }
                else
                {
                    ShowScrollToBottomButton = true;
                }

                StateHasChanged();
            });
        }

        await InvokeAsync(StateHasChanged);
    }

    private static string NormalizeP2pToken(string? token) =>
        string.IsNullOrWhiteSpace(token) ? "-" : token.Trim();

    private static string P2pInboundStreamKey(int senderId, string normalizedToken) =>
        $"{senderId}\u001f{normalizedToken}";
}
