using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Platform;
using LittleGuy3000.Desktop.Services;
using Microsoft.UI.Xaml;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private enum SpecialVoice { None, Circle, Command }
    private SpecialVoice _specialVoice;
    private bool _circlePending;
    private bool _commandExecuting;
    private Func<VoiceCommand, CancellationToken, Task<string>> _executeDesktopCommand = DesktopCommands.ExecuteAsync;

    private async Task CircleAskAsync()
    {
        if (_paused || _quitting) return;
        CancelActive(); DropSnapshot(); _walkthrough.Cancel(); _bubble.Hide(); _replyPad.Hide(); AppWindow.Hide();
        _circlePending = false; _compactMode = false; _companion.Enabled = false;
        long generation = _lifetime.Restart(); var token = _lifetime.Token;
        try
        {
            if (!Settings.ScreenEnabled) { OpenDetails(); ShowNotice("Enable screen context to ask about a circled item."); return; }
            var region = await _selector.Start(true);
            if (!_lifetime.IsCurrent(generation) || region is null) return;
            var center = new Native.Point((int)(region.Value.X+region.Value.Width/2),(int)(region.Value.Y+region.Value.Height/2));
            _target = _capture.Describe(Native.GetAncestor(Native.WindowFromPoint(center),2),center);
            if (_target is null) { OpenDetails(); ShowNotice("Circle an item inside one application."); return; }
            var image = await _capture.CaptureAsync(_target,new(Settings.Exclusions),token,region.Value);
            if (!_lifetime.IsCurrent(generation)) { image.Dispose(); return; }
            _snapshot = image; _circlePending = true; _compactMode = true; _hidden = false;
            _companion.Anchor = new(region.Value.Right,region.Value.Y); ApplyPreferences();
            _bubble.ShowAt(_companion.Anchor.Value,false); _bubble.SetInputHint("Or type your question and press Enter…");
            _bubble.SetMessage("Little Guy · item selected",Settings.VoiceEnabled ? "Hold your Ask shortcut, ask about the circled item, then release. Or type below." : "Type your question below. Enable the microphone in Settings to ask aloud.",false);
            ContextText.Text = "Circled area attached · " + _target.ProcessName;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (_lifetime.IsCurrent(generation)) { OpenDetails(); ShowNotice(ex.Message,true); } }
        finally { if (!_quitting) ApplyPreferences(); }
    }
    private void BeginSpecialVoice(bool command)
    {
        if (_paused || _quitting) return;
        CancelActive(); _lifetime.Restart();
        if (command) { _circlePending = false; DropSnapshot(); }
        _hidden = false; _compactMode = true; _summaryText = "";
        Native.GetCursorPos(out var cursor); _companion.Anchor ??= new(cursor.X,cursor.Y); ApplyPreferences(); AppWindow.Hide();
        _bubble.ShowAt(_companion.Anchor.Value,false); _bubble.SetInputHint(command ? null : "Or type your question…");
        if (!Settings.VoiceEnabled || !_speech.RecognitionAvailable) { ShowNotice("Enable the microphone in Settings, or use the typed command field there."); return; }
        if (command && !Settings.VoiceCommandsEnabled) { ShowNotice("Voice commands are disabled in Settings."); return; }
        _specialVoice = command ? SpecialVoice.Command : SpecialVoice.Circle;
        _holding = true; _holdStarted = true; _holdTime = _clock.Elapsed.TotalSeconds;
        _bubble.SetMessage(command ? "Little Guy · listening for a command" : "Little Guy · listening", "Keep holding Space while speaking. Release to finish.",true);
        QuestionBox.Text = "";
        StartListening();
        if (!_speech.Listening) { _specialVoice = SpecialVoice.None; _holding = false; }
    }
    private async Task FinishSpecialVoiceAsync()
    {
        if (_submittingVoice) return;
        _submittingVoice = true; var mode = _specialVoice; long generation = _lifetime.Generation;
        try
        {
            _bubble.SetMessage("Little Guy · transcribing", "Turning your speech into text on your PC…", true);
            string text = (await _speech.FinishAsync()).Trim();
            if (!_lifetime.IsCurrent(generation)) return;
            QuestionBox.Text = text; _specialVoice = SpecialVoice.None;
            if (string.IsNullOrWhiteSpace(text)) { ShowNotice("I didn't catch that. Hold the shortcut and try again, or type your request."); return; }
            if(mode==SpecialVoice.Command && _speech.LastConfidence<.6f) { ShowNotice("I heard ‘"+text+"’, but I'm not confident enough to act. Repeat it, or use the typed command field in Settings."); return; }
            if (mode == SpecialVoice.Command) await ExecuteVoiceCommandAsync(text);
            else await AskCircleAsync(text);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (_lifetime.IsCurrent(generation)) ShowNotice(ex.Message,true); }
        finally { _submittingVoice = false; MicButton.Content = "Mic"; VoiceInfo.Text = _speech.RecognitionDescription; }
    }
    private async Task AskCircleAsync(string question)
    {
        if (_busy || !_circlePending || _snapshot is null) return;
        CancelActive(); long generation = _lifetime.Restart(); var token = _lifetime.Token; _busy = true;
        _summaryText = ""; _bubble.SetInputHint(null); QuestionText.Text = question; AnswerText.Text = "";
        try
        {
            if (!_capture.IsFresh(_snapshot.Window)) { DropSnapshot(); ShowNotice("That window moved or closed. Circle the item again to attach a fresh view."); return; }
            if (!_connected) await ConnectAsync(false,false);
            if (!_lifetime.IsCurrent(generation)) return;
            if (!_connected || _provider is null || !ConfigureTurn()) { ShowNotice("Connect your account in Settings."); return; }
            SetState(CompanionState.Thinking,"Looking at the item you circled…");
            var answer = await _provider.AskAsync("The attached image is the bounding area of the item I circled. Answer about that selected item: " + question,
                _snapshot,AnswerMode.Balanced,text=>Dispatch(()=>{ if(_lifetime.IsCurrent(generation)) AnswerText.Text=text; }),token);
            if (!_lifetime.IsCurrent(generation)) return;
            AnswerText.Text=answer.Answer; _summaryText=CompactReply.Preview(answer.Summary,answer.Answer);
            _bubble.SetMessage("Little Guy · your circled item",_summaryText,false); _bubble.SetInputHint("Ask a follow-up…");
            _store.SaveExchange(question,answer.Answer); SetState(CompanionState.Success,"Answer ready.");
            if (Settings.AutoSpeak) SpeakNarration(_summaryText);
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) { if(_lifetime.IsCurrent(generation)) ShowNotice(ex.Message,true); }
        finally { if(_lifetime.IsCurrent(generation)) _busy=false; }
    }
    private async Task ExecuteVoiceCommandAsync(string text)
    {
        CancelActive(); long generation = _lifetime.Restart(); var token=_lifetime.Token;
        DropSnapshot(); _circlePending=false; _busy=true; _commandExecuting=true; _hidden=false; _compactMode=true; _summaryText="";
        Native.GetCursorPos(out var cursor); _companion.Anchor=new(cursor.X,cursor.Y); ApplyPreferences();
        _bubble.ShowAt(_companion.Anchor.Value,false); _bubble.SetInputHint(null);
        WelcomePanel.Visibility = Visibility.Collapsed; ActionButtons.Visibility = Visibility.Collapsed;
        QuestionText.Text = text; QuestionBox.Text = ""; AnswerText.Text = "";
        CancelButton.Visibility = Visibility.Visible; SendButton.IsEnabled = false;
        AnswerScroll.ChangeView(null, 0, null);
        try
        {
            if (!Settings.VoiceCommandsEnabled) { ShowNotice("Voice commands are disabled in Settings."); return; }
            _bubble.SetMessage("Little Guy · working on your command",text,true); SetState(CompanionState.Thinking,"Working on your command…");
            var command = VoiceCommandParser.Parse(text);
            string result = command.Action == "research" ? await RunResearchAsync(command.Target, token)
                : command.Action == "unsupported"
                ? $"I heard: ‘{text}’. I couldn't match that to a supported command. {DesktopCommands.Help}"
                : await _executeDesktopCommand(command,token);
            if(!_lifetime.IsCurrent(generation)) return;
            QuestionText.Text=text; CommandBox.Text=text; AnswerText.Text=result; _summaryText=result;
            _bubble.SetMessage("Little Guy · command result",result,false); SetState(CompanionState.Success,result);
            if(Settings.AutoSpeak) SpeakNarration(result);
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) { if(_lifetime.IsCurrent(generation)) ShowNotice(ex.Message,true); }
        finally { if(_lifetime.IsCurrent(generation)) { _busy=false; _commandExecuting=false; CancelButton.Visibility=Visibility.Collapsed; SendButton.IsEnabled=true; } }
    }
    private async void RunCommand_Click(object sender,RoutedEventArgs e) => await ExecuteVoiceCommandAsync(CommandBox.Text);
}
