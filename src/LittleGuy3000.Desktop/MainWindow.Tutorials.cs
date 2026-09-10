using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Platform;
using Microsoft.UI.Xaml;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private PixelRect? _tutorialStartRegion;

    private void PrepareTutorial(WindowContext? target, PixelRect? region = null)
    {
        CancelActive(); DropSnapshot(); _walkthrough.Cancel(); _replyPad.Hide();
        _lifetime.Restart();
        if (!Settings.ScreenEnabled || target is null)
        {
            OpenDetails();
            ShowNotice(!Settings.ScreenEnabled ? "Enable screen context in Settings, then point at an app and press Ctrl+]."
                : "Point inside the app you want help with and press Ctrl+], or use Ctrl+Shift+] to draw a tutorial area.");
            return;
        }
        _target = target; _tutorialStartRegion = region; _quickGoalPending = true;
        _hidden = false; _compactMode = true; _summaryText = "";
        ModeCombo.SelectedIndex = (int)AnswerMode.Walkthrough;
        WelcomePanel.Visibility = ActionButtons.Visibility = Notice.Visibility = Visibility.Collapsed;
        QuestionText.Text = "New tutorial"; AnswerText.Text = "What do you want to make or learn? Enter a goal to start.";
        QuestionBox.Text = "";
        ContextText.Text = (region is null ? "Pointed control" : "Selected tutorial area") + " · " + target.ProcessName;
        _companion.Anchor = region is { } box ? new PixelPoint(box.Right, box.Y) : target.Cursor;
        ApplyPreferences(); AppWindow.Hide(); UpdateWalkthroughUi();
        _bubble.SetInputHint("Your goal, then Enter…");
        _bubble.SetMessage("Little Guy · start a tutorial", "What do you want to make or learn here? For example: Create a warm pad sound. No special wording needed.", false);
        _bubble.ShowAt(_companion.Anchor.Value, false);
        _bubble.FocusReply();
        SetState(CompanionState.Idle, "Enter a tutorial goal. Ctrl+Alt+Enter checks each step once the tutorial starts.");
    }

    private async Task StartTutorialGoalAsync(string goal)
    {
        goal = goal.Trim();
        if (!_quickGoalPending || _busy || goal.Length == 0 || _paused || _quitting) return;
        long generation = _lifetime.Generation;
        _busy = true;
        try
        {
            if (!_connected) await ConnectAsync(false, false);
            if (!_connected || _provider is null || !_lifetime.IsCurrent(generation) || !_quickGoalPending) return;
            await _provider.NewConversationAsync();
            if (!_lifetime.IsCurrent(generation) || !_quickGoalPending) return;
            _quickGoalPending = false; _lastQuestion = goal;
            _walkthrough.Start(goal); _bubble.SetInputHint(null);
            _bubble.SetWalkthrough(true);
        }
        catch (Exception ex) { ShowNotice(ex.Message, true); return; }
        finally { _busy = false; }
        if (_walkthrough.Objective is not null && _lifetime.IsCurrent(generation))
            await SubmitAsync(goal, WalkthroughAction.Start);
    }

    private async Task ContinueTutorialAsync()
    {
        if (_busy || _paused || _quitting) return;
        if (_quickGoalPending) { _bubble.FocusReply(); return; }
        if (_walkthrough.Objective is null)
        {
            ShowNotice("Start a tutorial with Ctrl+] for a pointed control or Ctrl+Shift+] for a selected area.");
            return;
        }
        // Keep the selected application and objective; the pointer is not a new target.
        if (!AppWindow.IsVisible && !_bubble.Visible && _target is not null)
        {
            _hidden = false; _compactMode = true; _companion.Anchor = _target.Cursor;
            ApplyPreferences(); _bubble.ShowAt(_target.Cursor, true);
        }
        await SubmitAsync("Check the current step against the fresh screen. If it succeeded, give the next step toward my tutorial goal.", WalkthroughAction.Confirm);
    }
}
