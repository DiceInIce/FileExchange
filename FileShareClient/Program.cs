using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.Logging;
using FileShareClient;
using FileShareClient.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;


namespace FileShareClient
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();
            //services.AddLogging(logging => logging.AddConsole());
            services.AddScoped<ApiService>();
            services.AddScoped<ChatService>();
            services.AddScoped<FileSaveService>();
            services.AddSingleton<WindowControlService>();

            var provider = services.BuildServiceProvider();

            var mainForm = new MainForm(provider);
            Application.Run(mainForm);
        }
    }

    public class MainForm : Form
    {
        private const int WmNclbuttondown = 0xA1;
        private const int HtCaption = 0x2;
        private BlazorWebView blazorWebView;
        private readonly WindowControlService _windowControlService;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public MainForm(IServiceProvider services)
        {
            Text = "FileShare Desktop Client";
            Size = new Size(1280, 820);
            MinimumSize = new Size(1024, 700);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;

            _windowControlService = services.GetRequiredService<WindowControlService>();
            _windowControlService.CommandRequested += HandleWindowCommandRequested;

            blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index.html",
                Services = services
            };

            blazorWebView.RootComponents.Add<App>("#app");

            Controls.Add(blazorWebView);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _windowControlService.CommandRequested -= HandleWindowCommandRequested;
                blazorWebView.Dispose();
            }

            base.Dispose(disposing);
        }

        private void HandleWindowCommandRequested(WindowCommand command)
        {
            if (!IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(() => HandleWindowCommandRequested(command));
                return;
            }

            switch (command)
            {
                case WindowCommand.Minimize:
                    WindowState = FormWindowState.Minimized;
                    break;
                case WindowCommand.ToggleMaximize:
                    WindowState = WindowState == FormWindowState.Maximized
                        ? FormWindowState.Normal
                        : FormWindowState.Maximized;
                    break;
                case WindowCommand.Close:
                    Close();
                    break;
                case WindowCommand.DragMove:
                    ReleaseCapture();
                    SendMessage(Handle, WmNclbuttondown, HtCaption, 0);
                    break;
            }
        }
    }
}

