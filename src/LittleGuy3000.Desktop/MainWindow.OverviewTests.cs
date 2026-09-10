using System.Text.Json;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendSelectionTestMessage(nint window, uint message, nuint w, nint l);
    private async Task RunOverviewCheckAsync(string directory)
    {
        var report = new Dictionary<string, object>();
        var sample = new Window { Title = "Synthetic interface overview" };
        var stack = new StackPanel { Spacing = 24, Margin = new Thickness(60) };
        stack.Children.Add(new TextBlock { Text = "SAMPLE AUDIO INTERFACE", FontSize = 26 });
        stack.Children.Add(new TextBlock { Text = "Gain knob: input level     Mix dial: dry / wet", FontSize = 20 });
        stack.Children.Add(new TextBlock { Text = "Output meter: signal level", FontSize = 20 });
        int clicks = 0; var button = new Button { Content = "Bypass", FontSize = 20 }; button.Click += (_, _) => clicks++; stack.Children.Add(button);
        sample.Content = new Border { Background = new SolidColorBrush(Microsoft.UI.Colors.DarkSlateGray), Child = stack };
        try
        {
            sample.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(180, 160, 800, 450)); sample.Activate(); await Task.Delay(400);
            nint handle = WinRT.Interop.WindowNative.GetWindowHandle(sample); var bounds = Native.Bounds(handle);
            var region = new PixelRect(bounds.X + 30, bounds.Y + 55, bounds.Width - 60, bounds.Height - 85);
            var selecting = _selector.Start(); await Task.Delay(100);
            var point = new Native.Point((int)(region.X + 20), (int)(region.Y + 20));
            report["selectionInterceptsInput"] = Native.WindowFromPoint(point) == _selector.Handle;
            void Mouse(uint msg, double x, double y)
            {
                var screen = Native.Bounds(_selector.Handle);
                int packed = ((ushort)(short)(int)(x - screen.X)) | ((int)(ushort)(short)(int)(y - screen.Y) << 16);
                SendSelectionTestMessage(_selector.Handle, msg, msg == 0x202 ? 0u : 1u, (nint)packed);
            }
            // Reverse drag verifies normalization and native message routing without injecting global input.
            Mouse(0x201, region.Right, region.Bottom); Mouse(0x200, region.X, region.Y); Mouse(0x202, region.X, region.Y);
            var selected = await selecting.WaitAsync(TimeSpan.FromSeconds(3));
            report["dragCoordinates"] = new { expected = region, actual = selected };
            report["reverseDragCorrect"] = selected == region;
            report["selectionHiddenOnRelease"] = !_selector.Active && !Native.IsWindowVisible(_selector.Handle);
            report["underlyingButtonNotClicked"] = clicks == 0;
            var cancelled = _selector.Start(); CancelActive(); report["cancelStopsSelection"] = await cancelled is null;
            var escape = _selector.Start(); Native.PostMessageW(_selector.Handle, 0x312, 1, 0);
            report["escapeCancelsSelection"] = await escape.WaitAsync(TimeSpan.FromSeconds(3)) is null;
            var target = _capture.Describe(handle, new Native.Point((int)region.X + 100, (int)region.Y + 100), true)!;
            using var crop = await _capture.CaptureAsync(target, new([]), CancellationToken.None, region);
            report["cropInsideSelection"] = region.Contains(new(crop.Transform.DesktopBounds.X, crop.Transform.DesktopBounds.Y))
                && region.Contains(new(crop.Transform.DesktopBounds.Right, crop.Transform.DesktopBounds.Bottom));
            using var bytes = new MemoryStream(crop.Png); using var decoded = new System.Drawing.Bitmap(bytes);
            report["encodedCropSizeMatches"] = decoded.Width == crop.Transform.ImageWidth && decoded.Height == crop.Transform.ImageHeight
                && decoded.Width < bounds.Width && decoded.Height < bounds.Height;
            await File.WriteAllBytesAsync(Path.Combine(directory, "selected-interface.png"), crop.Png);
            _provider = new LittleGuy3000.Codex.CodexProvider();
            await _provider.ConnectAsync(Environment.GetEnvironmentVariable("LITTLEGUY_FIXTURE_EXE")!, Path.Combine(_store.DirectoryPath, "overview-fixture"));
            _availableModels = await _provider.ModelsAsync(); _connected = true; Settings.ScreenEnabled = true;
            await ExplainSelectedInterfaceAsync(target, region);
            report["overviewHasAllSampleControls"] = new[] { "Gain", "Mix", "Bypass", "meter" }.All(t => AnswerText.Text.Contains(t, StringComparison.OrdinalIgnoreCase));
            report["summaryInBubble"] = _bubble.Visible && _bubble.Text.Contains("interface", StringComparison.OrdinalIgnoreCase);
            report["fullExplanationNotTruncated"] = AnswerText.Text.Split(' ').Length > 120;
            OpenDetails(); report["openingPreservesCrop"] = _snapshot?.Transform.DesktopBounds == crop.Transform.DesktopBounds;
            report["overviewHotkeyRegistered"] = _tray.OverviewHotkeyAvailable;
        }
        catch (Exception ex) { report["error"] = ex.ToString(); }
        finally { _selector.Cancel(); sample.Close(); }
        await File.WriteAllTextAsync(Path.Combine(directory, "overview-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        await QuitAsync();
    }
}
