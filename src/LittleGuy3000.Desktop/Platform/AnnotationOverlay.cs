using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Companion;
using Microsoft.UI.Dispatching;

namespace LittleGuy3000.Desktop.Platform;

internal sealed class AnnotationOverlay : IDisposable
{
    private readonly List<LayeredWindow> _windows = [];
    private readonly DispatcherQueueTimer _watch;
    private WindowContext? _target;
    private DateTimeOffset _expires;
    private nint _allowedPanel;
    internal int VisibleCount => _windows.Count;
    public AnnotationOverlay(DispatcherQueue dispatcher)
    {
        _watch = dispatcher.CreateTimer(); _watch.Interval = TimeSpan.FromMilliseconds(50);
        _watch.Tick += (_, _) =>
        {
            if (_target is null) return;
            if ((Native.GetAsyncKeyState(1) & 0x8000) != 0) { Clear(); return; }
            nint foreground = Native.GetForegroundWindow();
            Native.GetWindowThreadProcessId(_target.Handle, out var pid);
            if (DateTimeOffset.UtcNow >= _expires || !Native.IsWindow(_target.Handle) || Native.IsIconic(_target.Handle) || pid != _target.ProcessId
                || Native.Bounds(_target.Handle) != _target.Bounds || (foreground != _target.Handle && foreground != _allowedPanel)) Clear();
        };
        _watch.Start();
    }
    public void Show(ScreenSnapshot snapshot, IReadOnlyList<Annotation> commands, nint panel)
    {
        Clear(); _target = snapshot.Window; _allowedPanel = panel; _expires = DateTimeOffset.UtcNow.AddSeconds(10);
        foreach (var annotation in commands)
        {
            var p = snapshot.Transform.ToDesktop(new(annotation.X, annotation.Y));
            var end = snapshot.Transform.ToDesktop(new(annotation.X + annotation.Width, annotation.Y + annotation.Height));
            int width = (int)Math.Ceiling(end.X - p.X), height = (int)Math.Ceiling(end.Y - p.Y);
            int padding = 38, canvasWidth = Math.Max(190, width + padding * 2), canvasHeight = height + padding * 2 + 28;
            if (canvasWidth > 8000 || canvasHeight > 8000) continue;
            using var bitmap = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bitmap); g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(235, 230, 250, 133), 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var shadow = new Pen(Color.FromArgb(120, 14, 19, 14), 7);
            g.DrawEllipse(shadow, padding, padding, Math.Max(8, width), Math.Max(8, height));
            g.DrawEllipse(pen, padding, padding, Math.Max(8, width), Math.Max(8, height));
            if (annotation.Shape == "arrow")
            {
                g.DrawLine(pen, 4, 7, padding + 4, padding + 4); g.DrawLine(pen, padding - 8, padding + 4, padding + 4, padding + 4); g.DrawLine(pen, padding + 4, padding - 8, padding + 4, padding + 4);
            }
            using var background = new SolidBrush(Color.FromArgb(235, 22, 28, 22));
            using var text = new SolidBrush(Color.FromArgb(242, 249, 208));
            using var font = new Font("Segoe UI", 10, FontStyle.Bold);
            string label = annotation.Label;
            g.FillRectangle(background, 8, canvasHeight - 28, canvasWidth - 16, 25);
            g.DrawString(label, font, text, new RectangleF(15, canvasHeight - 24, canvasWidth - 26, 22));
            var window = new LayeredWindow(); window.Present(bitmap, (int)p.X - padding, (int)p.Y - padding); _windows.Add(window);
        }
    }
    public void Clear() { foreach (var window in _windows) window.Dispose(); _windows.Clear(); _target = null; }
    public void Dispose() { _watch.Stop(); Clear(); }
}
