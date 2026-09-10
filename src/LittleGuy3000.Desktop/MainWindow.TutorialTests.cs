using System.Text.Json;
using LittleGuy3000.Core;
using LittleGuy3000.Codex;
using LittleGuy3000.Desktop.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private async Task RunTutorialCheckAsync(string directory)
    {
        var report = new Dictionary<string, object>();
        var sample = new Window { Title = "Little Guy tutorial sample", Content = new Border { Background = new SolidColorBrush(Microsoft.UI.Colors.DarkSlateGray), Child = new TextBlock { Text = "Options\nEnable sound", FontSize = 28, Margin = new Thickness(40) } } };
        try
        {
            sample.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(120, 120, 700, 450)); sample.Activate(); await Task.Delay(300);
            nint handle = WinRT.Interop.WindowNative.GetWindowHandle(sample);
            var bounds = Native.Bounds(handle);
            var target = _capture.Describe(handle, new Native.Point((int)bounds.X + 100, (int)bounds.Y + 120), true)!;
            _provider = new CodexProvider(); await _provider.ConnectAsync(Environment.GetEnvironmentVariable("LITTLEGUY_FIXTURE_EXE")!, Path.Combine(_store.DirectoryPath, "tutorial-fixture"));
            _availableModels = await _provider.ModelsAsync(); _connected = true;
            Settings.ScreenEnabled = true; Settings.AutoSpeak = false; Settings.HistoryEnabled = false;
            PrepareTutorial(target);
            report["pointWaitsForGoal"] = _quickGoalPending && _walkthrough.Objective is null && _bubble.Text.Contains("What do you want");
            await ContinueTutorialAsync();
            report["nextCannotInventGoal"] = _quickGoalPending && _walkthrough.Objective is null;
            await SubmitAsync("Enable the sample option");
            report["plainGoalStartsTutorial"] = !_quickGoalPending && _walkthrough.Step == 1 && _walkthrough.Objective == "Enable the sample option";
            string? firstCapture = _snapshot?.Id;
            Native.PostMessageW(_tray.MessageWindow, 0x312, 11, 0);
            for (int i = 0; i < 100 && _walkthrough.Step < 2; i++) await Task.Delay(50);
            report["nextHotkeyAdvancesWithFreshCapture"] = _walkthrough.Step == 2 && _snapshot?.Id != firstCapture;
            report["nextKeepsTargetAndGoal"] = _target?.Handle == handle && _walkthrough.Objective == "Enable the sample option";
            await ContinueTutorialAsync();
            report["nextCompletesTutorial"] = _walkthrough.Objective is null;
            var region = new PixelRect(bounds.X + 20, bounds.Y + 40, 300, 220);
            PrepareTutorial(target, region);
            report["regionWaitsForGoal"] = _quickGoalPending && _tutorialStartRegion == region && _walkthrough.Objective is null;
            await StartTutorialGoalAsync("Enable sound in this selected area");
            report["firstStepUsesSelectedCrop"] = _snapshot?.Transform.DesktopBounds == region && _walkthrough.Objective == "Enable sound in this selected area";
            await ContinueTutorialAsync();
            report["nextSeesFullWindowForNavigation"] = _snapshot?.Transform.DesktopBounds == bounds && _target?.Handle == handle;
            PrepareTutorial(target); HideApp();
            report["hideCancelsPendingGoal"] = !_quickGoalPending && _tutorialStartRegion is null;
            report["tutorialHotkeysRegistered"] = _tray.TutorialNextHotkeyAvailable && _tray.TutorialRegionHotkeyAvailable;
            report["idleReplyWindowDoesNotExist"] = _replyPad.Handle == 0;
        }
        catch (Exception ex) { report["error"] = ex.ToString(); }
        finally { sample.Close(); }
        await File.WriteAllTextAsync(Path.Combine(directory, "tutorial-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        await QuitAsync();
    }
}
