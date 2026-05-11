using FileShareClient.Models;
using FileShareClient.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace FileShareClient.Pages;

public partial class Chat
{
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageInput) || SelectedFriend == null || !ChatService.IsConnected)
            return;

        if (!Friends.Any(f => f.Id == SelectedFriend.Id))
        {
            SelectedFriend = null;
            Messages.Clear();
            FileTransferStatus = "Нельзя отправить сообщение: пользователь удален из друзей.";
            StateHasChanged();
            return;
        }

        var content = MessageInput;
        MessageInput = "";
        IsSendingMessage = true;
        MessageDeliveryStatus = "Отправка...";

        var sentViaP2P = false;
        if (SelectedFriend.IsOnline)
        {
            try
            {
                sentViaP2P = await JS.InvokeAsync<bool>("peerTransfer.sendText", SelectedFriend.Id, content);
            }
            catch
            {
                sentViaP2P = false;
            }
        }

        if (sentViaP2P)
        {
            _ = ApiService.StoreMessageAsync(SelectedFriend.Id, content);
            MessageDeliveryStatus = "Отправлено по P2P";
        }
        else
        {
            await ChatService.SendMessageAsync(SelectedFriend.Id, content);
            MessageDeliveryStatus = SelectedFriend.IsOnline
                ? "Отправлено через сервер"
                : "Отправлено через сервер (получатель офлайн)";
        }

        Messages.Add(new ChatMessage
        {
            SenderId = CurrentUser?.Id ?? 0,
            SenderName = CurrentUser?.Username,
            Content = content,
            Timestamp = DateTime.Now,
            IsRead = true,
            Type = 0
        });

        if (_isNearBottom)
        {
            _scrollToBottomRequested = true;
            ShowScrollToBottomButton = false;
        }
        else
        {
            ShowScrollToBottomButton = true;
        }
        IsSendingMessage = false;
        StateHasChanged();
    }

    private async Task HandleMessageKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await SendMessage();
        }
    }

    private async Task RefreshUnreadCounts()
    {
        var unreadMessages = await ApiService.GetUnreadMessagesAsync();
        UnreadCounts = unreadMessages
            .GroupBy(m => m.SenderId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task MarkFriendMessagesAsRead(int friendId)
    {
        var unreadMessages = await ApiService.GetUnreadMessagesAsync();
        var toMark = unreadMessages.Where(m => m.SenderId == friendId && m.Id > 0).ToList();
        foreach (var msg in toMark)
        {
            await ApiService.MarkAsReadAsync(msg.Id);
        }

        UnreadCounts[friendId] = 0;
        await RefreshUnreadCounts();
        await InvokeAsync(StateHasChanged);
    }

    private async Task MarkSingleMessageAsRead(int messageId)
    {
        if (messageId <= 0)
        {
            return;
        }
        await ApiService.MarkAsReadAsync(messageId);
    }
}
