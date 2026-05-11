using FileShareClient.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FileShareClient.Pages;

public partial class Chat
{
    [JSInvokable]
    public Task OnMessagesScrolled(bool nearBottom)
    {
        _isNearBottom = nearBottom;
        ShowScrollToBottomButton = !nearBottom && Messages.Count > 0;
        return InvokeAsync(StateHasChanged);
    }

    private async Task ScrollToBottomNow()
    {
        ShowScrollToBottomButton = false;
        _isNearBottom = true;
        await JS.InvokeVoidAsync("chatFileDrop.scrollToBottom", "chat-messages-scroll");
    }

    private void AddToast(string text, string kind = "info")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Toasts.Add(new ToastItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = text,
            Kind = kind
        });
        if (Toasts.Count > 4)
        {
            Toasts.RemoveAt(0);
        }

        var dismissId = Toasts[^1].Id;
        _ = InvokeAsync(async () =>
        {
            await Task.Delay(5000);
            var idx = Toasts.FindIndex(t => t.Id == dismissId);
            if (idx >= 0)
            {
                Toasts.RemoveAt(idx);
                StateHasChanged();
            }
        });
    }

    private void DismissToast(string id)
    {
        Toasts.RemoveAll(t => t.Id == id);
    }

    private void Logout()
    {
        ApiService.ClearSession();
        Navigation.NavigateTo("/");
    }
}
