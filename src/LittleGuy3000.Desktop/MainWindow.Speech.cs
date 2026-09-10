using LittleGuy3000.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private bool _refreshingMicrophones;
    private void RefreshMicrophones()
    {
        _refreshingMicrophones = true;
        try
        {
            var names = LocalSpeechService.Microphones().Distinct().ToList();
            if (!string.IsNullOrWhiteSpace(Settings.MicrophoneName) && !names.Contains(Settings.MicrophoneName)) names.Add(Settings.MicrophoneName);
            names.Insert(0, "System default microphone");
            MicrophoneCombo.ItemsSource = names;
            MicrophoneCombo.SelectedIndex = string.IsNullOrWhiteSpace(Settings.MicrophoneName) ? 0 : names.IndexOf(Settings.MicrophoneName);
            _speech.MicrophoneName = Settings.MicrophoneName;
        }
        catch { VoiceInfo.Text = "Couldn't list microphones. Check Windows microphone permissions."; }
        finally { _refreshingMicrophones = false; }
    }
    private void RefreshMicrophones_Click(object sender, RoutedEventArgs e) => RefreshMicrophones();
    private void Microphone_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || _refreshingMicrophones) return;
        CancelActive();
        Settings.MicrophoneName = MicrophoneCombo.SelectedIndex <= 0 ? null : MicrophoneCombo.SelectedItem as string;
        _speech.MicrophoneName = Settings.MicrophoneName; _store.SaveSettings();
    }
}
