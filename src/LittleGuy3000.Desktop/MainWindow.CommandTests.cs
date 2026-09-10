using System.Text.Json;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Services;
using Microsoft.UI.Xaml;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private async Task RunCommandRoutingCheckAsync(string directory)
    {
        var report = new Dictionary<string, bool>(); var actions = new List<VoiceCommand>();
        try
        {
            // Exercise the real Ask path with a local executor fixture: no app is launched or music played.
            _executeDesktopCommand = (command, token) => { token.ThrowIfCancellationRequested(); actions.Add(command); return Task.FromResult("Local command fixture executed."); };
            _connected = false; Settings.VoiceCommandsEnabled = true; ModeCombo.SelectedIndex = 0;
            await SubmitAsync("Open Spotify.");
            report["ordinaryAskOpensSpotifyWithoutCloud"] = actions.LastOrDefault() == new VoiceCommand("open", "spotify") && AnswerText.Text == "Local command fixture executed.";
            report["commandResultVisibleWithoutWelcomePanel"] = WelcomePanel.Visibility == Visibility.Collapsed && SendButton.IsEnabled && CancelButton.Visibility == Visibility.Collapsed;
            await SubmitAsync("Play Nirvana on Spotify.");
            report["ordinaryAskRoutesNamedPlayback"] = actions.LastOrDefault() == new VoiceCommand("spotify_play", "Nirvana");
            report["commandTranscriptVisibleInSettings"] = CommandBox.Text == "Play Nirvana on Spotify.";
            await SubmitAsync("Open Spotify, Play ADHD Techno.");
            report["screenshotRequestRoutesToLocalPlayback"] = actions.LastOrDefault() == new VoiceCommand("spotify_play", "ADHD Techno") && AnswerText.Text == "Local command fixture executed.";
            await SubmitAsync("Hey Little Guy, can you please open Spotify. Play ADHD Techno.");
            report["spokenCompoundRequestRoutesToLocalPlayback"] = actions.LastOrDefault() == new VoiceCommand("spotify_play", "ADHD Techno");
            int previous = actions.Count;
            await SubmitAsync("Open Spotify, open an unknown app.");
            report["unmatchedCompoundDoesNotFallThroughToCloud"] = actions.Count == previous && AnswerText.Text.Contains("I heard:");
            await SubmitAsync("What is Spotify?");
            report["questionDoesNotExecute"] = actions.Count == previous;
            Settings.VoiceCommandsEnabled = false; await SubmitAsync("Open Spotify.");
            report["disabledCommandsDoNotExecute"] = actions.Count == previous;
            Settings.VoiceCommandsEnabled = true; _walkthrough.Start("Open Spotify."); await SubmitAsync("Open Spotify.");
            report["walkthroughRemainsGuidance"] = actions.Count == previous; _walkthrough.Cancel();
            await ExecuteVoiceCommandAsync("Launch the moon");
            report["unmatchedCommandShowsTranscript"] = AnswerText.Text.Contains("I heard: ‘Launch the moon’") && actions.Count == previous;
            HideApp(); SendSelectionTestMessage(_tray.MessageWindow, 0x8002, 1, 0);
            report["activationMessageReopensPanel"] = AppWindow.IsVisible;
            HideApp(); SendSelectionTestMessage(_tray.MessageWindow, 0x8002, 2, 0);
            report["settingsActivationOpensSettings"] = AppWindow.IsVisible && SettingsPanel.Visibility == Visibility.Visible;
        }
        finally
        {
            _executeDesktopCommand = DesktopCommands.ExecuteAsync;
            await File.WriteAllTextAsync(Path.Combine(directory, "command-routing-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            await QuitAsync();
        }
    }
}
