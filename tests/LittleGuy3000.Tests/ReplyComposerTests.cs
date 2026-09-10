using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Platform;

internal static class ReplyComposerTests
{
    [StructLayout(LayoutKind.Sequential)] private struct Message { public nint Window; public uint Id; public nuint W; public nint L; public uint Time; public Native.Point Point; public uint Private; }
    [DllImport("user32.dll")] private static extern int GetMessageW(out Message message, nint hwnd, uint first, uint last);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(ref Message message);
    [DllImport("user32.dll")] private static extern nint SetFocus(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowTextW(nint window, string text);

    internal static int Host()
    {
        using var window = new NativeWindow();
        Native.SetWindowLongPtrW(window.Handle, -16, (nint)0x10CF0000);
        Native.SetWindowPos(window.Handle, 0, 120, 180, 700, 400, 0x40);
        Native.CreateWindowExW(0, "STATIC", "SYNTHETIC POST\nMy post: Meet Little Guy 3000.\nAlex: This looks useful!", 0x50000000, 25, 25, 620, 110, window.Handle, 0, 0, 0);
        Native.CreateWindowExW(0, "STATIC", "Reply", 0x50000000, 25, 145, 620, 30, window.Handle, 0, 0, 0);
        nint edit = Native.CreateWindowExW(0x200, "EDIT", "", 0x50810080, 25, 180, 610, 65, window.Handle, 0, 0, 0);
        nint other = Native.CreateWindowExW(0x200, "EDIT", "", 0x50810080, 25, 280, 610, 40, window.Handle, 0, 0, 0);
        window.Message += (m, w, l) =>
        {
            if (m != 0x8050) return null;
            if (w == 1) { SetWindowTextW(edit, ""); SetFocus(edit); }
            if (w == 2) SetFocus(other);
            if (w == 3) SetWindowTextW(edit, "My existing draft");
            return 0;
        };
        Native.SetForegroundWindow(window.Handle); SetFocus(edit);
        Console.WriteLine(window.Handle.ToInt64()); Console.Out.Flush();
        while (GetMessageW(out var message, 0, 0, 0) > 0) { TranslateMessage(ref message); DispatchMessageW(ref message); }
        return 0;
    }

    internal static async Task<int> RunAsync()
    {
        using var host = Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "LittleGuy3000.Tests.exe"), "--composer-host")
        { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true })!;
        try
        {
            nint handle = (nint)long.Parse((await host.StandardOutput.ReadLineAsync())!);
            Native.SetForegroundWindow(handle); await Task.Delay(500);
            var context = new WindowContext(handle, (uint)host.Id, "LittleGuy3000.Tests", "Synthetic replies", Native.Bounds(handle), new(180, 380), Native.GetDpiForWindow(handle));
            async Task Reset() { Native.SetForegroundWindow(handle); Native.PostMessageW(handle, 0x8050, 1, 0); await Task.Delay(150); }
            void Require(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
            await Reset();
            var target = await ReplyComposer.CaptureAsync(context, CancellationToken.None);
            if (target is null) Console.WriteLine("Synthetic editor foreground: " + (Native.GetForegroundWindow() == handle));
            Require(target is not null, "focused empty external editor identified");
            Require(await ReplyComposer.InsertAsync(target!, context, "Thanks, Alex!", CancellationToken.None), "exact editor receives draft without keystrokes");
            Require(await ReplyComposer.CaptureAsync(context, CancellationToken.None) is null, "existing draft preserved");
            await Reset(); target = await ReplyComposer.CaptureAsync(context, CancellationToken.None);
            Native.PostMessageW(handle, 0x8050, 2, 0); await Task.Delay(150);
            Require(!await ReplyComposer.InsertAsync(target!, context, "Wrong field", CancellationToken.None), "changed focus prevents insertion");
            await Reset(); target = await ReplyComposer.CaptureAsync(context, CancellationToken.None);
            Native.PostMessageW(handle, 0x8050, 3, 0); await Task.Delay(150);
            Require(!await ReplyComposer.InsertAsync(target!, context, "Overwrite", CancellationToken.None), "typing during generation prevents overwrite");
            await Reset(); target = await ReplyComposer.CaptureAsync(context, CancellationToken.None);
            Require(!await ReplyComposer.InsertAsync(target! with { CapturedAt = DateTimeOffset.UtcNow.AddMinutes(-4) }, context, "Late", CancellationToken.None), "expired target prevents insertion");
            Require(!await ReplyComposer.InsertAsync(target!, context with { Bounds = new(0, 0, 500, 500) }, "Moved", CancellationToken.None), "changed window prevents insertion");
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            try { await ReplyComposer.InsertAsync(target!, context, "Cancelled", cancel.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { Console.WriteLine("PASS cancelled draft cannot insert"); }
            return 0;
        }
        finally { if (!host.HasExited) { host.Kill(); await host.WaitForExitAsync(); } }
    }
}
