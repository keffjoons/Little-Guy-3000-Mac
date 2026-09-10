using System.Text.Json;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Companion;
using LittleGuy3000.Desktop.Platform;
using LittleGuy3000.Desktop.Services;
using Microsoft.UI.Xaml;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private void ApplyCaptureVisibility()
    {
        bool applied = CaptureVisibility.Apply(Settings.ShowInScreenCapture);
        StreamVisibilityStatus.Text = !applied
            ? "Windows could not update capture visibility for every window. Restart Little Guy and check your recording preview."
            : Settings.ShowInScreenCapture ? "Little Guy is available to screen capture."
            : "Windows capture exclusion is enabled for Little Guy.";
    }

    private void StreamVisibility_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.ShowInScreenCapture = StreamVisibilityToggle.IsOn;
        _store.SaveSettings();
        ApplyCaptureVisibility();
    }

    private async Task RunStreamVisibilityCheckAsync(string directory)
    {
        var report = new Dictionary<string, object>();
        bool AllHave(uint expected) => CaptureVisibility.Handles.All(handle =>
            Native.GetWindowDisplayAffinity(handle, out uint actual) && actual == expected);
        try
        {
            report["unusedPopupsStartHidden"] = !_bubble.Visible && !_replyPad.Visible
                && !Native.IsWindowVisible(_bubble.Handle) && !Native.IsWindowVisible(_replyPad.Handle);
            report["unusedPopupsHaveBoundedSize"] = Native.Bounds(_bubble.Handle).Width <= 430 && _replyPad.Handle == 0;
            report["olderProfilesDefaultToHidden"] = !JsonSerializer.Deserialize<UserSettings>("{}")!.ShowInScreenCapture;
            StreamVisibilityToggle.IsOn = false;
            report["toggleExcludesEveryExistingWindow"] = AllHave(0x11);
            report["includesMainBubbleReplyAndSelection"] = CaptureVisibility.Handles.Contains(Hwnd)
                && CaptureVisibility.Handles.Contains(_bubble.Handle) && _replyPad.Handle == 0
                && CaptureVisibility.Handles.Contains(_selector.Handle) && CaptureVisibility.Handles.Length >= 10;
            using (var lateOverlay = new LayeredWindow())
            {
                report["newOverlayInheritsHidden"] = Native.GetWindowDisplayAffinity(lateOverlay.Handle, out uint value) && value == 0x11;
                StreamVisibilityToggle.IsOn = true;
                report["toggleIncludesEveryExistingWindow"] = AllHave(0);
                using (var visibleOverlay = new LayeredWindow())
                    report["newOverlayInheritsVisible"] = Native.GetWindowDisplayAffinity(visibleOverlay.Handle, out value) && value == 0;
                report["visibleChoicePersisted"] = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(Path.Combine(_store.DirectoryPath, "settings.json")))!.ShowInScreenCapture;
                ShowTab("settings"); AppWindow.Show(); Activate();
                await Task.Delay(300);
                using var capture = await _capture.CaptureAsync(_capture.Describe(Hwnd, new Native.Point(0, 0), true)!, new CapturePolicy([]), CancellationToken.None);
                await File.WriteAllBytesAsync(Path.Combine(directory, "stream-visible.png"), capture.Png);
                report["visibleWindowCaptureSucceeded"] = capture.Transform.ImageWidth > 100 && capture.Png.Length > 1000;
                await CheckPopupRenderingAsync(directory, report);
                StreamVisibilityToggle.IsOn = false;
                report["toggleBackExcludesEveryWindow"] = AllHave(0x11);
                report["hiddenChoicePersisted"] = !JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(Path.Combine(_store.DirectoryPath, "settings.json")))!.ShowInScreenCapture;
            }
            report["disposedWindowsUnregistered"] = CaptureVisibility.Handles.All(Native.IsWindow);
        }
        catch (Exception ex) { report["error"] = ex.ToString(); }
        await File.WriteAllTextAsync(Path.Combine(directory, "stream-visibility-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        await QuitAsync();
    }

    private async Task CheckPopupRenderingAsync(string directory, Dictionary<string, object> report)
    {
        for (int i = 0; i < 8; i++)
        {
            StreamVisibilityToggle.IsOn = false;
            StreamVisibilityToggle.IsOn = true;
        }
        report["visibilityTogglesDoNotShowUnusedPopups"] = !Native.IsWindowVisible(_bubble.Handle) && !Native.IsWindowVisible(_replyPad.Handle);
        var anchor = new PixelPoint(300, 300);
        var focus = Native.GetForegroundWindow();
        _bubble.SetMessage("Little Guy · rendering check", "A compact speech bubble with a real explanation.", false);
        _bubble.ShowAt(anchor, true);
        await Task.Delay(500);
        report["bubbleShowsWithoutTakingFocus"] = Native.GetForegroundWindow() == focus;
        report["bubbleRendersAtBoundedSize"] = await CheckRenderedPopupAsync(_bubble.Handle, "stream-bubble", directory, 800, 700);
        _bubble.SetMessage("Little Guy · thinking…", "Checking the interface…", true);
        StreamVisibilityToggle.IsOn = false; StreamVisibilityToggle.IsOn = true;
        await Task.Delay(350);
        report["thinkingBubbleRendersAfterToggle"] = await CheckRenderedPopupAsync(_bubble.Handle, "stream-thinking", directory, 800, 700);
        _bubble.Hide();
        _replyPad.ShowDrafts(new ReplyBatch("multiple", false, [new("Sample person", "How does this work?", "Point at a control and ask Little Guy."), new("Another person", "Can I demo it?", "Yes, enable recording visibility in Settings.")]), anchor, "Synthetic reply preview");
        await Task.Delay(500);
        report["replyPadShowsWithoutTakingFocus"] = Native.GetForegroundWindow() == focus;
        report["replyPadRendersAtBoundedSize"] = await CheckRenderedPopupAsync(_replyPad.Handle, "stream-replies", directory, 900, 1400);
        StreamVisibilityToggle.IsOn = false; StreamVisibilityToggle.IsOn = true;
        await Task.Delay(350);
        report["replyPadRendersAfterToggle"] = await CheckRenderedPopupAsync(_replyPad.Handle, "stream-replies-toggled", directory, 900, 1400);
        _replyPad.Hide();
        StreamVisibilityToggle.IsOn = false; StreamVisibilityToggle.IsOn = true;
        report["replySurfaceDestroyedOnDismiss"] = _replyPad.Handle == 0;
        report["dismissedPopupsStayHiddenAfterToggle"] = !Native.IsWindowVisible(_bubble.Handle) && !Native.IsWindowVisible(_replyPad.Handle);
    }

    private async Task<bool> CheckRenderedPopupAsync(nint handle, string name, string directory, double maxWidth, double maxHeight)
    {
        var bounds = Native.Bounds(handle);
        if (!bounds.IsValid || bounds.Width > maxWidth || bounds.Height > maxHeight) return false;
        using var screenshot = await _capture.CaptureAsync(_capture.Describe(handle, new Native.Point(0, 0), true)!, new CapturePolicy([]), CancellationToken.None);
        await File.WriteAllBytesAsync(Path.Combine(directory, name + ".png"), screenshot.Png);
        using var stream = new MemoryStream(screenshot.Png);
        using var bitmap = new System.Drawing.Bitmap(stream);
        int samples = 0, dark = 0, white = 0;
        for (int y = 35; y < bitmap.Height - 10; y += 5)
        for (int x = 10; x < bitmap.Width - 10; x += 5)
        {
            var pixel = bitmap.GetPixel(x, y); samples++;
            if (pixel.R < 100 && pixel.G < 110 && pixel.B < 100) dark++;
            if (pixel.R > 240 && pixel.G > 240 && pixel.B > 240) white++;
        }
        return samples > 100 && dark > samples * .5 && white < samples * .15;
    }
}
