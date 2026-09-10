using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private readonly NaturalNarrator _narrator = new();
    private bool _narrationSuppressed;

    private void InitializeNarration()
    {
        SpeechPitchSlider.Value = Settings.SpeechPitch;
        NarrationVoiceCombo.ItemsSource = NaturalNarrator.Voices.Select(v => v.Label).ToArray();
        int index = Array.FindIndex(NaturalNarrator.Voices, v => v.Id == Settings.NarrationVoice);
        NarrationVoiceCombo.SelectedIndex = Math.Max(0, index);
        if (index < 0) Settings.NarrationVoice = NaturalNarrator.Voices[0].Id;
        _narrator.Error += message => Dispatch(() => ShowNotice(message));
        _narrator.PlaybackStarted += () => Dispatch(() =>
        {
            if (!_narrationSuppressed && !_paused && !_quitting) SetState(CompanionState.Speaking, "Little Guy is speaking… use Stop speech to silence this answer.");
        });
        _narrator.Finished += () => Dispatch(() =>
        {
            if (!_busy && !_speech.Listening && !_paused && !_quitting) SetState(CompanionState.Idle, "Ready when you are.");
        });
    }

    private void SpeakNarration(string text)
    {
        if (_narrationSuppressed || _quitting || string.IsNullOrWhiteSpace(text)) return;
        if (Settings.NarrationVoice == "windows")
        {
            try { _speech.Speak(text); }
            catch { ShowNotice("Windows speech couldn't play. Try a natural voice in Settings."); }
        }
        else _ = _narrator.SpeakAsync(text, Settings.NarrationVoice, Settings.SpeechRate, Settings.SpeechPitch);
    }

    private void StopNarration()
    {
        _narrationSuppressed = true;
        _narrator.Stop(); _speech.StopSpeaking();
        if (!_busy && !_speech.Listening && !_paused) SetState(CompanionState.Idle, "Speech stopped.");
    }
    private void StopSpeech_Click(object sender, RoutedEventArgs e) => StopNarration();

    private void NarrationVoice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || NarrationVoiceCombo.SelectedIndex < 0) return;
        StopNarration();
        Settings.NarrationVoice = NaturalNarrator.Voices[NarrationVoiceCombo.SelectedIndex].Id;
        _store.SaveSettings();
    }
    private void SpeechPitch_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_loaded) return;
        StopNarration(); Settings.SpeechPitch = (int)e.NewValue; _store.SaveSettings();
    }
    private void ChipmunkVoice_Click(object sender, RoutedEventArgs e)
    {
        NarrationVoiceCombo.SelectedIndex = 2; SpeechPitchSlider.Value = 9;
        PreviewVoice_Click(sender, e);
    }
    private void PreviewVoice_Click(object sender, RoutedEventArgs e)
    {
        StopNarration();
        const string preview = "Hi, I'm Little Guy 3000. Point at something, and I'll help you figure it out.";
        if (Settings.NarrationVoice == "windows")
        {
            try { _speech.Speak(preview); }
            catch { ShowNotice("Windows speech couldn't play. Try a natural voice instead."); }
        }
        else _ = _narrator.SpeakAsync(preview, Settings.NarrationVoice, Settings.SpeechRate, Settings.SpeechPitch);
    }
}
