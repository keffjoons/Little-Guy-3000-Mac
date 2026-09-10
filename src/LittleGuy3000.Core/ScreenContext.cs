namespace LittleGuy3000.Core;

public readonly record struct PixelPoint(double X, double Y);
public readonly record struct PixelRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;
    public bool Contains(PixelPoint p) => p.X >= X && p.Y >= Y && p.X <= Right && p.Y <= Bottom;
}

public sealed record ScreenTransform(PixelRect DesktopBounds, int ImageWidth, int ImageHeight)
{
    public bool IsValid => DesktopBounds.IsValid && ImageWidth > 0 && ImageHeight > 0;
    public PixelPoint ToDesktop(PixelPoint p) => IsValid
        ? new(DesktopBounds.X + p.X * DesktopBounds.Width / ImageWidth, DesktopBounds.Y + p.Y * DesktopBounds.Height / ImageHeight)
        : throw new InvalidOperationException("Invalid screen transform.");
    public PixelPoint ToImage(PixelPoint p) => IsValid
        ? new((p.X - DesktopBounds.X) * ImageWidth / DesktopBounds.Width, (p.Y - DesktopBounds.Y) * ImageHeight / DesktopBounds.Height)
        : throw new InvalidOperationException("Invalid screen transform.");
}

public sealed record WindowContext(nint Handle, uint ProcessId, string ProcessName, string Title, PixelRect Bounds, PixelPoint Cursor, uint Dpi);
public sealed record ScreenSnapshot(string Id, DateTimeOffset CapturedAt, WindowContext Window, ScreenTransform Transform, byte[] Png) : IDisposable
{
    public void Dispose() => Array.Clear(Png);
}

public sealed class CapturePolicy
{
    private readonly HashSet<string> _excluded;
    public CapturePolicy(IEnumerable<string> excluded) => _excluded = new(excluded.Select(Normalize), StringComparer.OrdinalIgnoreCase);
    private static string Normalize(string name) => Path.GetFileNameWithoutExtension(name.Trim());
    public bool Allows(string processName) => !string.IsNullOrWhiteSpace(processName) && !_excluded.Contains(Normalize(processName));
}
