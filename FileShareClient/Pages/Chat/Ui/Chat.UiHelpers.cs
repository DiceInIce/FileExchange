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

        Toasts.Add(new ToastMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = text,
            Kind = kind
        });
        if (Toasts.Count > 4)
        {
            Toasts.RemoveAt(0);
        }

        Task.Delay(5000).ContinueWith(_ =>
        {
            if (Toasts.Count > 0)
            {
                Toasts.RemoveAt(0);
            }
        });
    }

    private void DismissToast(string id)
    {
        Toasts.RemoveAll(t => t.Id == id);
    }

    private sealed class ToastMessage
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Kind { get; set; } = "info";
    }

    private void Logout()
    {
        ApiService.ClearSession();
        Navigation.NavigateTo("/");
    }
}
