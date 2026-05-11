using FileShareClient.Models;
using FileShareClient.Services;
using Microsoft.AspNetCore.Components;

namespace FileShareClient.Pages;

public partial class Chat
{
    private void HandleMessageReceived(ChatMessage message)
    {
        _ = LoadInitialData();
        if (SelectedFriend?.Id == message.SenderId)
        {
            Messages.Add(message);
            _ = MarkSingleMessageAsRead(message.Id);
            if (_isNearBottom)
            {
                _scrollToBottomRequested = true;
                ShowScrollToBottomButton = false;
            }
            else
            {
                ShowScrollToBottomButton = true;
            }
            _ = InvokeAsync(StateHasChanged);
        }
        else
        {
            if (!UnreadCounts.ContainsKey(message.SenderId))
            {
                UnreadCounts[message.SenderId] = 0;
            }
            UnreadCounts[message.SenderId]++;
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private void HandleSocialDataChanged()
    {
        _ = InvokeAsync(async () =>
        {
            await LoadInitialData();
            if (SelectedFriend != null)
            {
                var stillFriend = Friends.Any(f => f.Id == SelectedFriend.Id);
                if (!stillFriend)
                {
                    SelectedFriend = null;
                    Messages.Clear();
                    FileTransferStatus = "Пользователь больше не в друзьях. Чат закрыт.";
                }
                else
                {
                    Messages = await ApiService.GetConversationAsync(SelectedFriend.Id);
                    _scrollToBottomRequested = true;
                }
            }
            StateHasChanged();
        });
    }

    private void HandleFileTransferRequest(FileTransferRequest request)
    {
        FileTransferStatus = $"Входящий запрос на файл от {request.SenderName}: {request.FileName} ({request.FileSize} байт)";
        _ = InvokeAsync(StateHasChanged);
    }

    private void HandleFriendRequestReceived(FriendRequestNotification notification)
    {
        FileTransferStatus = $"Новая заявка в друзья от {notification.SenderName}";
        _ = InvokeAsync(StateHasChanged);
    }

    private void HandleFileTransferAvailable(FileTransferAvailableNotification notification)
    {
        FileTransferStatus = $"Получен файл от {notification.SenderName}: {notification.FileName}";
        _ = InvokeAsync(async () =>
        {
            if (SelectedFriend != null)
            {
                Messages = await ApiService.GetConversationAsync(SelectedFriend.Id);
                _scrollToBottomRequested = true;
            }
            StateHasChanged();
        });
    }

    private void HandleUserOnline(int userId, string username, string displayName)
    {
        var friend = Friends.FirstOrDefault(f => f.Id == userId);
        if (friend != null)
        {
            friend.IsOnline = true;
            _ = InvokeAsync(async () =>
            {
                await LoadInitialData();
                StateHasChanged();
            });
        }
    }

    private void HandleUserOffline(int userId)
    {
        var friend = Friends.FirstOrDefault(f => f.Id == userId);
        if (friend != null)
        {
            friend.IsOnline = false;
            _ = InvokeAsync(async () =>
            {
                await LoadInitialData();
                StateHasChanged();
            });
        }
    }

    private void HandleRealtimeConnected()
    {
        _ = InvokeAsync(async () =>
        {
            await LoadInitialData();
            if (SelectedFriend != null)
            {
                Messages = await ApiService.GetConversationAsync(SelectedFriend.Id);
                _scrollToBottomRequested = true;
            }
            StateHasChanged();
        });
    }
}
