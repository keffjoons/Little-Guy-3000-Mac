using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Platform;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace LittleGuy3000.Desktop.Companion;

// No HWND exists until drafts are ready. Dismissal destroys the surface rather than
// leaving an unused top-level WinUI window in the desktop compositor.
internal sealed class ReplyPadWindow : IDisposable
{
    private ReplyPadSurface? _surface;
    internal nint Handle => _surface?.Handle ?? 0;
    internal int DraftCount => _surface?.DraftCount ?? 0;
    internal bool Visible => _surface?.Visible ?? false;
    public void ShowDrafts(ReplyBatch batch, PixelPoint anchor, string status)
    {
        if (batch.Drafts.Count == 0) { Clear(); return; }
        nint foreground = Native.GetForegroundWindow();
        _surface ??= new ReplyPadSurface();
        _surface.Dismissed = Hide;
        _surface.ShowDrafts(batch, anchor, status);
        nint current = Native.GetForegroundWindow();
        if (current != foreground && Native.IsWindow(foreground)
            && Native.GetWindowThreadProcessId(current, out uint pid) != 0 && pid == Environment.ProcessId)
            Native.SetForegroundWindow(foreground);
    }
    public void Hide() { var surface = _surface; _surface = null; surface?.Dispose(); }
    public void Clear() => Hide();
    public void Dispose() => Hide();
}

internal sealed class ReplyPadSurface : IDisposable
{
    private readonly Window _window = new();
    private readonly StackPanel _cards = new() { Spacing = 14 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
    private bool _closing;
    internal nint Handle => WinRT.Interop.WindowNative.GetWindowHandle(_window);
    internal int DraftCount { get; private set; }
    internal bool Visible => _window.AppWindow.IsVisible;
    public Action? Dismissed { get; set; }
    public ReplyPadSurface()
    {
        _window.Title = Brand.Name + " · Reply drafts";
        var root = new Grid { RequestedTheme = ElementTheme.Dark, Padding = new Thickness(18), RowSpacing = 12,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 41, 31)) };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.Children.Add(new TextBlock { Text = "Little Guy · reply drafts", FontSize = 21, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Grid.SetRow(_status, 1); root.Children.Add(_status);
        var scroll = new ScrollViewer { Content = _cards }; Grid.SetRow(scroll, 2); root.Children.Add(scroll);
        var clear = new Button { Content = "Clear drafts & close" }; clear.Click += (_, _) => Dismissed?.Invoke(); Grid.SetRow(clear, 3); root.Children.Add(clear);
        root.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Escape) { e.Handled = true; Dismissed?.Invoke(); } };
        _window.Content = root;
        var presenter = (OverlappedPresenter)_window.AppWindow.Presenter;
        presenter.IsAlwaysOnTop = true; presenter.IsMaximizable = false; presenter.IsMinimizable = false;
        Native.SetWindowLongPtrW(Handle, -20, (Native.GetWindowLongPtrW(Handle, -20) | (nint)Native.ToolWindow) & ~(nint)0x40000);
        _window.AppWindow.Resize(new Windows.Graphics.SizeInt32(430, 680));
        _window.AppWindow.Hide();
        CaptureVisibility.Register(Handle);
        _window.AppWindow.Closing += (_, e) => { if (!_closing) { e.Cancel = true; _window.DispatcherQueue.TryEnqueue(() => Dismissed?.Invoke()); } };
    }
    public void ShowDrafts(ReplyBatch batch, PixelPoint anchor, string status)
    {
        _cards.Children.Clear(); DraftCount = batch.Drafts.Count;
        _status.Text = status + "\nEdit, copy and paste each reply. You send it yourself.";
        foreach (var draft in batch.Drafts)
        {
            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(new TextBlock { Text = draft.Recipient, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(new TextBlock { Text = draft.Comment, Opacity = .75, FontSize = 12, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            var text = new TextBox { Text = draft.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 64, MaxHeight = 240, MaxLength = 4000 };
            AutomationProperties.SetName(text, "Draft reply to " + draft.Recipient); stack.Children.Add(text);
            var copy = new Button { Content = "Copy reply" };
            AutomationProperties.SetName(copy, "Copy reply to " + draft.Recipient);
            copy.Click += (_, _) =>
            {
                try { var data = new DataPackage(); data.SetText(text.Text); Clipboard.SetContent(data); copy.Content = "Copied ✓"; }
                catch { _status.Text = "Clipboard is busy. Select the draft text and copy it, or try again."; }
            };
            text.TextChanged += (_, _) => copy.Content = "Copy reply";
            stack.Children.Add(copy);
            _cards.Children.Add(new Border { Child = stack, Padding = new Thickness(12), CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 47, 55, 42)) });
        }
        var point = new Native.Point((int)anchor.X, (int)anchor.Y);
        double scale = Math.Max(96, Native.GetDpiForWindow(Native.WindowFromPoint(point))) / 96.0;
        var work = Native.WorkArea(point);
        var rect = CompactReply.Place(anchor, work, Math.Min(430 * scale, work.Width), Math.Min(680 * scale, work.Height), scale);
        _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));
        _window.AppWindow.Show(false);
    }
    public void Hide() => _window.AppWindow.Hide();
    public void Clear() { Hide(); _cards.Children.Clear(); DraftCount = 0; _status.Text = ""; }
    public void Dispose() { _closing = true; Clear(); CaptureVisibility.Unregister(Handle); _window.Close(); }
}
