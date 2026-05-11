using FileShareClient.Models;
using FileShareClient.Services;
using Microsoft.AspNetCore.Components;

namespace FileShareClient.Pages;

public partial class Chat
{
    private async Task LoadInitialData()
    {
        try
        {
            Friends = await ApiService.GetFriendsAsync();
            PendingRequests = await ApiService.GetPendingRequestsAsync();
            var sentRequests = await ApiService.GetSentRequestsAsync();
            SentRequestUserIds = sentRequests
                .Where(r => r.UserId > 0)
                .Select(r => r.UserId)
                .ToHashSet();
            await RefreshUnreadCounts();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading data: {ex.Message}");
            AddToast("Не удалось обновить список друзей.", "error");
        }
        finally
        {
            IsInitialLoading = false;
        }
    }

    private async Task SelectFriend(User friend)
    {
        SelectedFriend = friend;
        Messages = await ApiService.GetConversationAsync(friend.Id);
        await MarkFriendMessagesAsRead(friend.Id);
        MessageInput = "";
        MessageDeliveryStatus = "";
        _scrollToBottomRequested = true;
    }

    private async Task SendFriendRequest(int userId)
    {
        if (await ApiService.SendFriendRequestAsync(userId))
        {
            SentRequestUserIds.Add(userId);
            FileTransferStatus = "Заявка в друзья отправлена.";
            StateHasChanged();
        }
        else
        {
            FileTransferStatus = "Не удалось отправить заявку в друзья.";
            StateHasChanged();
        }
    }

    private Task HandleSearchItemClick(int userId, bool canSendRequest)
    {
        if (!canSendRequest)
        {
            return Task.CompletedTask;
        }

        return SendFriendRequest(userId);
    }

    private async Task AcceptFriend(int userId)
    {
        if (await ApiService.AcceptFriendRequestAsync(userId))
        {
            await LoadInitialData();
            StateHasChanged();
        }
    }

    private async Task RejectFriend(int userId)
    {
        if (await ApiService.RejectFriendRequestAsync(userId))
        {
            await LoadInitialData();
            StateHasChanged();
        }
    }

    private async Task RemoveFriend()
    {
        if (SelectedFriend != null && await ApiService.RemoveFriendAsync(SelectedFriend.Id))
        {
            Friends.Remove(SelectedFriend);
            SelectedFriend = null;
            StateHasChanged();
        }
    }

    private async Task PerformSearch()
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults = await ApiService.SearchUsersAsync(SearchQuery);
            StateHasChanged();
        }
    }

    private void SetFriendFilter(string filter)
    {
        FriendFilter = filter;
    }
}
