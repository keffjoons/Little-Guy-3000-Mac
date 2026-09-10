using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Platform;
using Microsoft.UI.Xaml;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private readonly RegionSelector _selector = new();
    private bool _overviewActive;

    private async Task SelectInterfaceAsync(bool tutorial = false)
    {
        if (_paused || _quitting) return;
        CancelActive(); DropSnapshot(); _replyPad.Hide(); _bubble.Hide(); _compactMode = false;
        _walkthrough.Cancel(); ModeCombo.SelectedIndex = (int)AnswerMode.TeachMe; _quickGoalPending = false; _holding = false;
        if (!Settings.ScreenEnabled) { OpenDetails(); ShowNotice("Enable screen context in Settings to explain a selected interface."); return; }
        long generation = _lifetime.Restart();
        AppWindow.Hide(); _companion.Enabled = false;
        try
        {
            var region = await _selector.Start();
            if (!_lifetime.IsCurrent(generation)) return;
            if (region is null) { SetState(CompanionState.Idle, "Selection cancelled. Press Ctrl+Shift+\\ to try again."); return; }
            var center = new Native.Point((int)(region.Value.X + region.Value.Width / 2), (int)(region.Value.Y + region.Value.Height / 2));
            var target = _capture.Describe(Native.GetAncestor(Native.WindowFromPoint(center), 2), center);
            if (target is null) { OpenDetails(); ShowNotice("Draw the box around one application's interface, with that app at the center."); return; }
            if (tutorial) PrepareTutorial(target, region.Value);
            else await ExplainSelectedInterfaceAsync(target, region.Value);
        }
        catch (Exception ex) { if (_lifetime.IsCurrent(generation)) { OpenDetails(); ShowNotice(ex.Message, true); } }
        finally { if (!_quitting) ApplyPreferences(); }
    }

    private async Task ExplainSelectedInterfaceAsync(WindowContext target, PixelRect region)
    {
        CancelActive(); long generation = _lifetime.Restart(); var token = _lifetime.Token;
        _hidden = false; _compactMode = true; _overviewActive = true; _summaryText = ""; _target = target;
        _busy = true; _companion.Anchor = new(region.Right, region.Y); ApplyPreferences();
        _bubble.ShowAt(_companion.Anchor.Value, false); _bubble.SetMessage("Little Guy · interface overview", "Reading the controls inside your yellow box…", true);
        QuestionText.Text = "Explain this entire interface"; AnswerText.Text = ""; QuestionBox.Text = "";
        _lastQuestion = "Help me use the selected interface.";
        WelcomePanel.Visibility = ActionButtons.Visibility = Notice.Visibility = Visibility.Collapsed;
        SendButton.IsEnabled = false; CancelButton.Visibility = Visibility.Visible; UpdateWalkthroughUi();
        try
        {
            using var captured = await _capture.CaptureAsync(target, new(Settings.Exclusions), token, region);
            if (!_lifetime.IsCurrent(generation)) return;
            DropSnapshot(); _snapshot = captured with { Png = captured.Png.ToArray() };
            ContextText.Text = $"Selected interface · {target.ProcessName} · {captured.Transform.ImageWidth} × {captured.Transform.ImageHeight}";
            if (!_connected)
            {
                for (int i = 0; _connecting && i < 100; i++) await Task.Delay(50, token);
                if (!_connected) await ConnectAsync(false, false);
            }
            if (!_lifetime.IsCurrent(generation)) return;
            if (!_connected || _provider is null) { ShowNotice("Connect your account in Settings to explain this interface.", true); return; }
            if (!ConfigureTurn()) return;
            SetState(CompanionState.Thinking, "Mapping the interface and explaining its controls…");
            var answer = await _provider.ExplainInterfaceAsync(captured, text => Dispatch(() =>
            {
                if (_lifetime.IsCurrent(generation)) AnswerText.Text = text;
            }), token);
            if (!_lifetime.IsCurrent(generation)) return;
            if (answer.CaptureId != captured.Id) throw new InvalidDataException("The overview didn't match this selection. Draw the box again.");
            AnswerText.Text = answer.Answer; _summaryText = CompactReply.Preview(answer.Summary, answer.Answer);
            _bubble.SetMessage("Little Guy · interface overview", _summaryText, false);
            _store.SaveExchange(QuestionText.Text, answer.Answer); ActionButtons.Visibility = Visibility.Visible;
            SetState(CompanionState.Success, "Interface overview ready. Open the full answer for every visible control.");
            if (Settings.AutoSpeak) SpeakNarration(_summaryText);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (_lifetime.IsCurrent(generation)) ShowNotice(ex.Message, true); }
        finally
        {
            if (_lifetime.IsCurrent(generation)) { _overviewActive = false; _busy = false; SendButton.IsEnabled = true; CancelButton.Visibility = Visibility.Collapsed; UpdateWalkthroughUi(); }
        }
    }
}
