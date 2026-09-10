using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LittleGuy3000.Core;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT;

namespace LittleGuy3000.Desktop.Platform;

internal sealed class ScreenCaptureService
{
    public WindowContext? TargetUnderPointer()
    {
        Native.GetCursorPos(out var cursor);
        nint hwnd = Native.GetAncestor(Native.WindowFromPoint(cursor), 2);
        return Describe(hwnd, cursor);
    }
    public WindowContext? Describe(nint hwnd, Native.Point cursor, bool allowOutside = false)
    {
        if (hwnd == 0 || !Native.IsWindowVisible(hwnd) || Native.IsIconic(hwnd)) return null;
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == Environment.ProcessId && !allowOutside) return null;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            var title = new StringBuilder(512); Native.GetWindowTextW(hwnd, title, title.Capacity);
            var bounds = Native.Bounds(hwnd);
            if (!bounds.IsValid || (!allowOutside && !bounds.Contains(new(cursor.X, cursor.Y)))) return null;
            string name = process.ProcessName;
            if (name is "ShellExperienceHost" or "StartMenuExperienceHost" || string.IsNullOrWhiteSpace(title.ToString())) return null;
            return new(hwnd, pid, name, title.ToString(), bounds, new(cursor.X, cursor.Y), Native.GetDpiForWindow(hwnd));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return null; }
    }
    public bool IsFresh(WindowContext window) => Native.IsWindow(window.Handle) && !Native.IsIconic(window.Handle)
        && Native.GetWindowThreadProcessId(window.Handle, out uint pid) != 0 && pid == window.ProcessId && Native.Bounds(window.Handle) == window.Bounds;

    public async Task<ScreenSnapshot> CaptureAsync(WindowContext target, CapturePolicy policy, CancellationToken token, PixelRect? region = null)
    {
        if (!policy.Allows(target.ProcessName)) throw new InvalidOperationException("Screen capture is disabled for this application.");
        if (!IsFresh(target)) throw new InvalidOperationException("The window changed. Point at it and ask again.");
        if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Screen capture isn't available on this Windows session.");
        var item = CreateItem(target.Handle);
        using var device = CreateDevice();
        using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);
        using var session = pool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;
        var frameReady = new TaskCompletionSource<SoftwareBitmap>(TaskCreationOptions.RunContinuationsAsynchronously);
        int received = 0;
        pool.FrameArrived += async (sender, _) =>
        {
            if (Interlocked.Exchange(ref received, 1) != 0) return;
            try
            {
                using var frame = sender.TryGetNextFrame();
                if (frame is null) { frameReady.TrySetException(new IOException("No screen frame was available.")); return; }
                var copy = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
                if (!frameReady.TrySetResult(copy)) copy.Dispose();
            }
            catch (Exception ex) { frameReady.TrySetException(new IOException("I couldn't capture this window.", ex)); }
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(3));
        using var registration = timeout.Token.Register(() => frameReady.TrySetCanceled(timeout.Token));
        session.StartCapture();
        using var bitmap = await frameReady.Task;
        token.ThrowIfCancellationRequested();
        if (!IsFresh(target)) throw new InvalidOperationException("The window moved during capture. Please ask again.");
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        uint sourceWidth = (uint)Math.Min(item.Size.Width, bitmap.PixelWidth), sourceHeight = (uint)Math.Min(item.Size.Height, bitmap.PixelHeight);
        var selectedBounds = region is { } requested ? InterfaceOverview.Clip(requested, target.Bounds) : target.Bounds;
        double selectedWidth = selectedBounds.Width / target.Bounds.Width * sourceWidth;
        double selectedHeight = selectedBounds.Height / target.Bounds.Height * sourceHeight;
        double scale = Math.Min(1, 2200.0 / Math.Max(selectedWidth, selectedHeight));
        uint width = Math.Max(1, (uint)Math.Round(sourceWidth * scale)), height = Math.Max(1, (uint)Math.Round(sourceHeight * scale));
        encoder.BitmapTransform.ScaledWidth = width; encoder.BitmapTransform.ScaledHeight = height;
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        if (region is not null)
        {
            // WinRT applies scaling before cropping. Round inward to avoid including unselected pixels.
            uint x = (uint)Math.Ceiling((selectedBounds.X - target.Bounds.X) / target.Bounds.Width * width);
            uint y = (uint)Math.Ceiling((selectedBounds.Y - target.Bounds.Y) / target.Bounds.Height * height);
            uint right = (uint)Math.Floor((selectedBounds.Right - target.Bounds.X) / target.Bounds.Width * width);
            uint bottom = (uint)Math.Floor((selectedBounds.Bottom - target.Bounds.Y) / target.Bounds.Height * height);
            if (right <= x || bottom <= y) throw new InvalidOperationException("Draw a larger selection.");
            encoder.BitmapTransform.Bounds = new BitmapBounds { X = x, Y = y, Width = right - x, Height = bottom - y };
            selectedBounds = new(target.Bounds.X + x * target.Bounds.Width / width, target.Bounds.Y + y * target.Bounds.Height / height,
                (right - x) * target.Bounds.Width / width, (bottom - y) * target.Bounds.Height / height);
            width = right - x; height = bottom - y;
        }
        await encoder.FlushAsync(); token.ThrowIfCancellationRequested();
        if (stream.Size > 12 * 1024 * 1024) throw new InvalidOperationException("This window image is too large. Use a smaller window.");
        stream.Seek(0); var png = new byte[(int)stream.Size];
        using (var reader = new DataReader(stream.GetInputStreamAt(0))) { await reader.LoadAsync((uint)png.Length); reader.ReadBytes(png); }
        return new(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, target, new(selectedBounds, (int)width, (int)height), png);
    }
    private static GraphicsCaptureItem CreateItem(nint hwnd)
    {
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out var name));
        var interopId = typeof(IGraphicsCaptureItemInterop).GUID;
        int activationResult = RoGetActivationFactory(name, ref interopId, out var factoryPtr);
        WindowsDeleteString(name);
        Marshal.ThrowExceptionForHR(activationResult);
        try
        {
            var factory = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
            var itemId = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
            Marshal.ThrowExceptionForHR(factory.CreateForWindow(hwnd, ref itemId, out var itemPtr));
            try { return MarshalInspectable<GraphicsCaptureItem>.FromAbi(itemPtr); }
            finally { Marshal.Release(itemPtr); Marshal.ReleaseComObject(factory); }
        }
        finally { Marshal.Release(factoryPtr); }
    }
    private static IDirect3DDevice CreateDevice()
    {
        Marshal.ThrowExceptionForHR(D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7, out var device, out _, out var context));
        try
        {
            var iid = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c"); Marshal.ThrowExceptionForHR(Marshal.QueryInterface(device, in iid, out var dxgi));
            try
            {
                Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out var inspectable));
                try { return MarshalInterface<IDirect3DDevice>.FromAbi(inspectable); }
                finally { Marshal.Release(inspectable); }
            }
            finally { Marshal.Release(dxgi); }
        }
        finally { Marshal.Release(context); Marshal.Release(device); }
    }
    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig] int CreateForWindow(nint hwnd, ref Guid iid, out nint result);
        [PreserveSig] int CreateForMonitor(nint monitor, ref Guid iid, out nint result);
    }
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(nint name, ref Guid iid, out nint factory);
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string value, int length, out nint result);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint value);
    [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(nint adapter, int driverType, nint software, uint flags, nint levels, uint levelCount, uint sdk, out nint device, out int level, out nint context);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgi, out nint device);
}
