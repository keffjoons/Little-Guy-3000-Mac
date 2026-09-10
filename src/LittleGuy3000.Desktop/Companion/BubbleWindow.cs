using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Platform;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;

namespace LittleGuy3000.Desktop.Companion;

/// <summary>A small stationary speech card. Showing it never activates the full application panel.</summary>
internal sealed class BubbleWindow : IDisposable
{
    private readonly LayeredWindow _tail = new();
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _animation;
    private bool _thinking;
    private PixelRect _placement;
    private PixelPoint _companionAnchor;
    public event Action<PixelPoint>? PlacementChanged;
    public event Action? StopSpeech;
    private readonly Button _stopSpeech = new() { Content = "Stop speech", Padding = new Thickness(8, 4, 8, 4) };
    private double _scale = 1, _phase;
    public bool ReducedMotion { get; set; }
    internal bool Thinking => _thinking;
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int SetWindowRgn(nint window, nint region, bool redraw);
    private readonly Window _window = new();
    private readonly TextBlock _heading = new() { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock _body = new() { FontSize = 15, Width = 332, TextWrapping = TextWrapping.Wrap, MaxLines = 7, TextTrimming = TextTrimming.None, IsTextSelectionEnabled = true };
    private readonly Button _done = new() { Content = "I've done this", Padding = new Thickness(10, 6, 10, 6) };
    private readonly TextBox _reply = new() { PlaceholderText = "Add a goal or reply…", MinHeight = 32 };
    private readonly TextBlock _hint = new() { Text = "Ctrl+Space · full explanation", FontSize = 11, Opacity = .7 };
    private readonly Grid _bodyHost = new() { Height = 44 };
    private readonly nint _handle;
    private bool _closing;
    private PixelPoint _anchor;
    private bool _walkthrough;
    private string? _inputHint;
    public bool Visible => _window.AppWindow.IsVisible;
    internal nint Handle => _handle;
    internal string Text => _body.Text;
    public event Action? OpenDetails, Confirm, Dismissed;
    public event Action<string>? Reply;

    public BubbleWindow()
    {
        _animation = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        _animation.Interval = TimeSpan.FromMilliseconds(180);
        _animation.Tick += (_, _) => { if (Visible && _thinking && !ReducedMotion) { _phase += .4; DrawTail(); } };
        _window.Title = Brand.Name;
        var ink = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 242, 248, 219));
        _heading.Foreground = _body.Foreground = _hint.Foreground = ink;
        var close = new Button { Content = "×", Padding = new Thickness(6, 0, 6, 0), Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0) };
        AutomationProperties.SetName(close, "Dismiss Little Guy's bubble");
        close.Click += (_, _) => Dismissed?.Invoke();
        var header = new Grid(); header.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Grid.SetColumn(close, 1); header.Children.Add(_heading); header.Children.Add(close);
        var more = new Button { Content = "Open full answer", Padding = new Thickness(10, 6, 10, 6) };
        more.Click += (_, _) => OpenDetails?.Invoke(); _done.Click += (_, _) => Confirm?.Invoke();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }; actions.Children.Add(more); actions.Children.Add(_done);
        _stopSpeech.Click += (_, _) => StopSpeech?.Invoke();
        _reply.PreviewKeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter && !string.IsNullOrWhiteSpace(_reply.Text)) { e.Handled = true; string text = _reply.Text.Trim(); _reply.Text = ""; Reply?.Invoke(text); } };
        _bodyHost.Children.Add(_body);
        var stack = new StackPanel { Spacing = 8 }; stack.Children.Add(header); stack.Children.Add(_bodyHost); stack.Children.Add(_reply); stack.Children.Add(actions); stack.Children.Add(_stopSpeech); stack.Children.Add(_hint);
        AutomationProperties.SetLiveSetting(_body, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        _window.Content = new Border { RequestedTheme = ElementTheme.Dark, Padding = new Thickness(22,18,22,18), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 41, 31)), BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 186, 210, 128)), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(36), Child = stack };
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        var presenter = (OverlappedPresenter)_window.AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false); presenter.IsAlwaysOnTop = true; presenter.IsResizable = false; presenter.IsMaximizable = false; presenter.IsMinimizable = false;
        Native.SetWindowLongPtrW(_handle, -20, (Native.GetWindowLongPtrW(_handle, -20) | (nint)Native.ToolWindow) & ~(nint)0x40000);
        _window.AppWindow.Resize(new Windows.Graphics.SizeInt32(380, 240));
        _window.AppWindow.Hide();
        CaptureVisibility.Register(_handle);
        _window.AppWindow.Closing += (_, e) => { if (!_closing) { e.Cancel = true; Dismissed?.Invoke(); } };
    }
    public void ShowAt(PixelPoint anchor, bool walkthrough)
    {
        _anchor = anchor; _walkthrough = walkthrough;
        _done.Visibility = walkthrough ? Visibility.Visible : Visibility.Collapsed;
        _reply.Visibility = walkthrough || _inputHint is not null ? Visibility.Visible : Visibility.Collapsed;
        _reply.PlaceholderText = _inputHint ?? "Add a goal or reply…";
        _hint.Text = walkthrough ? "Ctrl+Alt+Enter · check step" : "Ctrl+Space · full explanation";
        var point = new Native.Point((int)anchor.X, (int)anchor.Y);
        double scale = Math.Max(96, Native.GetDpiForWindow(Native.WindowFromPoint(point))) / 96.0;
        var layout = CompactReply.PlaceAbove(anchor, Native.WorkArea(point), 380 * scale, (196 + _bodyHost.Height + (walkthrough || _inputHint is not null ? 44 : 0)) * scale, scale);
        var rect = layout.Bubble; _companionAnchor = layout.CompanionAnchor;
        PlacementChanged?.Invoke(_companionAnchor);
        _placement = rect; _scale = scale;
        _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));
        var region = CreateRoundRectRgn(0,0,(int)rect.Width+1,(int)rect.Height+1,(int)(72*scale),(int)(72*scale));
        if (SetWindowRgn(_handle, region, true) == 0) Native.DeleteObject(region);
        DrawTail(); _animation.Start();
        // Use the XAML-aware window API so its content and compositor track visibility.
        _window.AppWindow.Show(false);
    }
    public void SetMessage(string heading, string text, bool busy)
    {
        _thinking = busy;
        _heading.Text = heading; _body.Text = CompactReply.Preview(text, "");
        double height = Math.Clamp(Math.Ceiling(_body.Text.Length / 35.0), 2, 7) * 22;
        if (height != _bodyHost.Height) { _bodyHost.Height = height; if (Visible) ShowAt(_anchor, _walkthrough); }
        _done.IsEnabled = _reply.IsEnabled = !busy;
        if (Visible) DrawTail();
    }
    public void FocusReply() { _window.Activate(); _reply.Focus(FocusState.Programmatic); }
    public void SetWalkthrough(bool active) { if (_walkthrough != active && Visible) ShowAt(_anchor, active); }
    public void SetInputHint(string? hint) { _inputHint = hint; if (Visible) ShowAt(_anchor,_walkthrough); }
    private void DrawTail()
    {
        using var image = BubbleTailRenderer.RenderAbove(_thinking, _scale, ReducedMotion ? 0 : _phase);
        double headCenter = _companionAnchor.X + 65 * _scale;
        int x = (int)Math.Clamp(headCenter - image.Width / 2.0, _placement.X + 22 * _scale, Math.Max(_placement.X + 22 * _scale, _placement.Right - image.Width - 22 * _scale));
        _tail.Present(image, x, (int)(_placement.Bottom - 3 * _scale));
    }
    public void Hide() { _animation.Stop(); _tail.Hide(); _window.AppWindow.Hide(); }
    public void Dispose() { _closing = true; _animation.Stop(); _tail.Dispose(); CaptureVisibility.Unregister(_handle); _window.Close(); }
}
