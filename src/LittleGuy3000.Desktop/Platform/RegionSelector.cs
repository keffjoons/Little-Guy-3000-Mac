using System.Drawing;
using System.Runtime.InteropServices;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Companion;

namespace LittleGuy3000.Desktop.Platform;

/// <summary>Owns the drag input; no mouse click is delivered to the underlying application.</summary>
internal sealed class RegionSelector : IDisposable
{
    private readonly NativeWindow _input = new(Native.Layered | Native.ToolWindow | Native.NoActivate);
    private readonly LayeredWindow[] _edges = [new(), new(), new(), new()];
    private readonly LayeredWindow _hint = new();
    private TaskCompletionSource<PixelRect?>? _completion;
    private PixelPoint? _start;
    private bool _escapeRegistered;
    private bool _circle;
    private readonly List<PixelPoint> _stroke = [];
    private readonly LayeredWindow _ink = new();
    public bool Active => _completion is not null;
    internal nint Handle => _input.Handle;
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(nint window, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern nint SetCapture(nint window);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern nint LoadCursorW(nint instance, nint name);
    [DllImport("user32.dll")] private static extern nint SetCursor(nint cursor);
    public RegionSelector()
    {
        // Alpha 1 intercepts input without dimming the selected application.
        SetLayeredWindowAttributes(Handle, 0, 1, 2);
        _input.Message += OnMessage;
    }
    public Task<PixelRect?> Start(bool circle = false)
    {
        Cancel(); _completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _start = null;
        _circle = circle; _stroke.Clear();
        _escapeRegistered = Native.RegisterHotKey(Handle, 1, 0x4000, 0x1B);
        Native.GetCursorPos(out var cursor);
        var work = Native.WorkArea(cursor);
        using var bitmap = new Bitmap(470, 48);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(245, 34, 41, 31));
            using var font = new Font("Segoe UI", 11);
            g.DrawString(circle ? "Circle an item · then hold Ctrl+Space to ask aloud" : "Drag a yellow box around an interface · Esc to cancel", font, Brushes.LightGoldenrodYellow, 12, 13);
        }
        Native.SetWindowPos(Handle, -1, GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79), 0x10 | 0x40);
        _hint.Present(bitmap, (int)Math.Clamp(cursor.X + 20, work.X, Math.Max(work.X, work.Right - 470)), (int)Math.Clamp(cursor.Y + 24, work.Y, Math.Max(work.Y, work.Bottom - 48)));
        SetCursor(LoadCursorW(0, 32515));
        return _completion.Task;
    }
    private nint? OnMessage(uint message, nuint w, nint l)
    {
        if (!Active) return null;
        if (message == 0x312 && w == 1) { Cancel(); return 0; }
        if (message == 0x84) return 1;
        if (message == 0x21) return 3;
        if (message == 0x20) { SetCursor(LoadCursorW(0, 32515)); return 1; }
        if (message == 0x204) { Cancel(); return 0; }
        if (message == 0x215 && _start is not null) { Cancel(); return 0; }
        if (message is 0x201 or 0x200 or 0x202)
        {
            // Message coordinates are relative to the physical virtual-desktop input window.
            var bounds = Native.Bounds(Handle);
            var point = new PixelPoint(bounds.X + (short)((long)l & 0xffff), bounds.Y + (short)(((long)l >> 16) & 0xffff));
            if (message == 0x201) { _start = point; _hint.Hide(); SetCapture(Handle); }
            if (_start is { } start)
            {
                var rect = InterfaceOverview.Rectangle(start, point);
                if (_circle)
                {
                    if (_stroke.Count < 2048 && (_stroke.Count == 0 || Math.Abs(point.X-_stroke[^1].X)+Math.Abs(point.Y-_stroke[^1].Y)>3)) _stroke.Add(point);
                    if (message == 0x202)
                    {
                        try { Finish(InterfaceOverview.CircleBounds(_stroke)); } catch (InvalidOperationException) { Finish(null); }
                    }
                    else DrawStroke();
                    return 0;
                }
                if (message == 0x202) Finish(rect.Width >= 24 && rect.Height >= 24 ? rect : null);
                else Draw(rect);
            }
            return 0;
        }
        return null;
    }
    private void Draw(PixelRect rect)
    {
        if (!rect.IsValid) return;
        var edges = new[] { new PixelRect(rect.X, rect.Y, rect.Width, 3), new(rect.X, rect.Bottom - 3, rect.Width, 3),
            new(rect.X, rect.Y, 3, rect.Height), new(rect.Right - 3, rect.Y, 3, rect.Height) };
        for (int i = 0; i < 4; i++)
        {
            var edge = edges[i]; using var bitmap = new Bitmap(Math.Max(1, (int)edge.Width), Math.Max(1, (int)edge.Height));
            using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.FromArgb(255, 255, 221, 52));
            _edges[i].Present(bitmap, (int)edge.X, (int)edge.Y);
        }
    }
    private void DrawStroke()
    {
        if (_stroke.Count < 2) return;
        int x = (int)_stroke.Min(p=>p.X)-5, y = (int)_stroke.Min(p=>p.Y)-5;
        int width = (int)_stroke.Max(p=>p.X)-x+6, height = (int)_stroke.Max(p=>p.Y)-y+6;
        if ((long)width*height > 40_000_000) return;
        using var bitmap = new Bitmap(width,height);
        using(var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(255,255,221,52),3) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLines(pen,_stroke.Select(p=>new PointF((float)(p.X-x),(float)(p.Y-y))).ToArray());
        }
        _ink.Present(bitmap,x,y);
    }
    public void Cancel() => Finish(null);
    private void Finish(PixelRect? result)
    {
        var completion = _completion; _completion = null; _start = null;
        if (_escapeRegistered) { Native.UnregisterHotKey(Handle, 1); _escapeRegistered = false; }
        if (completion is not null) ReleaseCapture();
        Native.ShowWindow(Handle, 0); _hint.Hide(); _ink.Hide(); foreach (var edge in _edges) edge.Hide();
        completion?.TrySetResult(result);
    }
    public void Dispose() { Cancel(); _input.Dispose(); _hint.Dispose(); _ink.Dispose(); foreach (var edge in _edges) edge.Dispose(); }
}
