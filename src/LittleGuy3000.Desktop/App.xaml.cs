using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Path = System.IO.Path;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LittleGuy3000.Desktop;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private MainWindow? _window;
    private System.Threading.Mutex? _instance;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            RecordStartupFailure(e.Exception);
        };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        string instanceName = "Local\\LittleGuy3000.Desktop" + (Environment.GetCommandLineArgs().Contains("--self-test") ? ".SelfTest" : "");
        _instance = new System.Threading.Mutex(true, instanceName, out bool first);
        if (!first)
        {
            nint existing = FindWindowW(null, Platform.TrayService.ActivationTitle);
            if (existing != 0) PostMessageW(existing, 0x8002, Environment.GetCommandLineArgs().Contains("--settings") ? 2u : 1u, 0);
            Exit(); return;
        }
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex) { RecordStartupFailure(ex); Exit(); }
    }

    private static void RecordStartupFailure(Exception exception)
    {
        string directory = Environment.GetEnvironmentVariable("LITTLEGUY_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LittleGuy3000");
        Directory.CreateDirectory(directory);
        // Detailed exception content is permitted only for synthetic self-tests, never user conversations.
        string detail = Environment.GetCommandLineArgs().Contains("--self-test") ? exception.ToString() : $"{exception.GetType().Name} · 0x{exception.HResult:X8}";
        File.AppendAllText(Path.Combine(directory, "startup-diagnostic.txt"), DateTimeOffset.UtcNow + " " + detail + Environment.NewLine);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern nint FindWindowW(string? className, string title);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessageW(nint window, uint message, nuint wparam, nint lparam);
}
