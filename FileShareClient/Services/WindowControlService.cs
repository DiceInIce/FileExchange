namespace FileShareClient.Services
{
    public enum WindowCommand
    {
        Minimize,
        ToggleMaximize,
        Close,
        DragMove
    }

    public class WindowControlService
    {
        public event Action<WindowCommand>? CommandRequested;

        public void Request(WindowCommand command)
        {
            CommandRequested?.Invoke(command);
        }
    }
}
