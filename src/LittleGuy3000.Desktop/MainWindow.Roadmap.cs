using System.Text.Json;
using LittleGuy3000.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private void RefreshHistory()
    {
        if(!_loaded) return;
        HistoryItems.Children.Clear();
        var entries=_store.SearchHistory(HistorySearchBox.Text.Trim(),HistoryPinnedOnly.IsOn);
        foreach(var entry in entries)
        {
            var content=new StackPanel { Spacing=8 };
            content.Children.Add(new TextBlock { Text=entry.Question,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text=entry.Answer,TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true,FontSize=14 });
            var actions=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
            var pin=new Button { Content=entry.Pinned?"Unpin":"Pin" }; pin.Click+=(_,_)=> { _store.SetPinned(entry.Id,!entry.Pinned); RefreshHistory(); };
            var copy=new Button { Content="Copy answer" };copy.Click+=(_,_)=> { try { var data=new DataPackage();data.SetText(entry.Answer);Clipboard.SetContent(data);copy.Content="Copied"; } catch { ShowNotice("Clipboard is busy. Try again."); } };
            actions.Children.Add(pin);actions.Children.Add(copy);content.Children.Add(actions);
            HistoryItems.Children.Add(new Border { Padding=new Thickness(12),CornerRadius=new CornerRadius(10),Child=content,BorderThickness=new Thickness(1),BorderBrush=new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255,80,91,66)) });
        }
        if(entries.Count==0) HistoryItems.Children.Add(new TextBlock { Text="No matching saved answers." });
    }
    private void HistorySearch_Changed(object sender,TextChangedEventArgs e)=>RefreshHistory();
    private void HistoryPinned_Changed(object sender,RoutedEventArgs e)=>RefreshHistory();
    private void PinAnswer_Click(object sender,RoutedEventArgs e)
    {
        if(_busy||string.IsNullOrWhiteSpace(AnswerText.Text)) return;
        _store.PinExchange(QuestionText.Text,AnswerText.Text);ShowNotice("Answer pinned. Find it in History → Pinned only.");
    }
    private void SpeechRate_Changed(object sender,Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if(!_loaded) return;Settings.SpeechRate=(int)e.NewValue;_speech.SetRate(Settings.SpeechRate);_store.SaveSettings();
    }
    private void CopyDiagnostics_Click(object sender,RoutedEventArgs e)
    {
        var diagnostic=new { product=Brand.Name,version=Brand.Version,os=Environment.OSVersion.VersionString,
            architecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),connected=_connected,
            microphoneAvailable=_speech.RecognitionAvailable,speechEngine=_speech.RecognitionDescription,screenEnabled=Settings.ScreenEnabled,voiceEnabled=Settings.VoiceEnabled,
            hotkeys=new { ask=_tray.HotkeyAvailable,explain=_tray.ExplainHotkeyAvailable,walkthrough=_tray.WalkthroughHotkeyAvailable,
                replies=_tray.ReplyHotkeyAvailable,overview=_tray.OverviewHotkeyAvailable,circle=_tray.CircleHotkeyAvailable,commands=_tray.CommandHotkeyAvailable },
            capturedAt=DateTimeOffset.UtcNow };
        try { var data=new DataPackage();data.SetText(JsonSerializer.Serialize(diagnostic,new JsonSerializerOptions { WriteIndented=true }));Clipboard.SetContent(data);ShowNotice("Diagnostic summary copied. It contains version and capability status, with no account details, screenshots, transcripts or conversations."); }
        catch { ShowNotice("Clipboard is busy. Try again."); }
    }
}
