using System.Diagnostics;
using System.Drawing;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Platform;
using Microsoft.UI.Dispatching;

namespace LittleGuy3000.Desktop.Companion;

internal sealed class LayeredWindow : NativeWindow
{
    public LayeredWindow() : base(Native.Layered | Native.Transparent | Native.ToolWindow | Native.NoActivate)
    {
        Message += (message, _, _) => message switch { 0x84 => (nint)(-1), 0x21 => (nint)3, _ => null };
    }
    public void Present(Bitmap bitmap, int x, int y)
    {
        nint screen = Native.GetDC(0), memory = Native.CreateCompatibleDC(screen), hbitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        nint previous = Native.SelectObject(memory, hbitmap);
        try
        {
            var dest = new Native.Point(x, y); var source = new Native.Point(); var size = new Native.Size(bitmap.Width, bitmap.Height);
            var blend = new Native.Blend { Alpha = 255, Format = 1 };
            if (!Native.UpdateLayeredWindow(Handle, screen, ref dest, ref size, memory, ref source, 0, ref blend, 2)) throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            Native.SetWindowPos(Handle, -1, x, y, bitmap.Width, bitmap.Height, 0x10 | 0x40);
        }
        finally { Native.SelectObject(memory, previous); Native.DeleteObject(hbitmap); Native.DeleteDC(memory); Native.ReleaseDC(0, screen); }
    }
    public void Hide() => Native.ShowWindow(Handle, 0);
}

internal sealed class CompanionWindow : IDisposable
{
    private readonly LayeredWindow _window = new();
    private readonly DispatcherQueueTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _enabled = true;
    private int _lastX = int.MinValue, _lastY, _lastFrame = -1;
    private CompanionState _lastState;
    public CompanionState State { get; set; }
    public bool ReducedMotion { get; set; }
    public PixelPoint? Anchor { get; set; }
    public bool Enabled { get => _enabled; set { if (value && !_enabled) _lastX = int.MinValue; _enabled = value; if (!value) _window.Hide(); } }
    public CompanionWindow(DispatcherQueue dispatcher)
    {
        _timer = dispatcher.CreateTimer(); _timer.Interval = TimeSpan.FromMilliseconds(50); _timer.Tick += (_, _) => Tick(); _timer.Start();
    }
    private void Tick()
    {
        if (!Enabled) return;
        Native.GetCursorPos(out var cursor);
        if (Anchor is { } anchor) cursor = new Native.Point((int)anchor.X, (int)anchor.Y);
        var area = Native.WorkArea(cursor);
        nint target = Native.WindowFromPoint(cursor); double scale = Math.Max(96, Native.GetDpiForWindow(target)) / 96.0;
        int size = (int)(74 * scale), gap = (int)(28 * scale);
        int x = (int)Math.Clamp(cursor.X + gap, area.X, Math.Max(area.X, area.Right - size));
        int y = (int)Math.Clamp(cursor.Y + gap, area.Y, Math.Max(area.Y, area.Bottom - size));
        double t = _clock.Elapsed.TotalSeconds;
        // Sleeping idle animation does not repaint every timer tick.
        int frame = ReducedMotion ? 0 : State == CompanionState.Idle ? (t % 5.8 > 5.5 ? (int)(t * 20) : 0) : (int)(t * 20);
        if (x == _lastX && y == _lastY && State == _lastState && frame == _lastFrame) return;
        using var image = SmileyRenderer.Render(size, State, t, ReducedMotion);
        _window.Present(image, x, y); _lastX = x; _lastY = y; _lastFrame = frame; _lastState = State;
    }
    public void Dispose() { _timer.Stop(); _window.Dispose(); }
}
