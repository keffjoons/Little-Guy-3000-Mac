using System.Text.Json;
using System.Speech.Synthesis;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Companion;
using LittleGuy3000.Desktop.Platform;
using LittleGuy3000.Desktop.Services;
using Microsoft.UI.Xaml;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private async Task RunRoadmapCheckAsync(string directory)
    {
        var report=new Dictionary<string,object>();
        try
        {
            var context=_capture.Describe(Hwnd,new Native.Point(0,0),true)!;
            _bubble.ShowAt(new(context.Bounds.X+100,context.Bounds.Y+100),false);
            _bubble.SetMessage("Little Guy · thinking…","Looking at the item you circled…",true);
            report["thinkingState"]=_bubble.Thinking;
            using(var thought=BubbleTailRenderer.Render(true,true,3)) thought.Save(Path.Combine(directory,"thinking-tail.png"));
            _bubble.SetMessage("Little Guy · your answer","This knob controls how strongly the effect is applied.",false);
            report["speechState"]=!_bubble.Thinking;
            using(var speech=BubbleTailRenderer.Render(false,true,3)) speech.Save(Path.Combine(directory,"speech-tail.png"));
            Native.SetWindowDisplayAffinity(_bubble.Handle,0); await Task.Delay(350);
            var bubbleContext=_capture.Describe(_bubble.Handle,new Native.Point(0,0),true)!;
            using(var image=await _capture.CaptureAsync(bubbleContext,new([]),CancellationToken.None)) await File.WriteAllBytesAsync(Path.Combine(directory,"speech-bubble.png"),image.Png);
            var selection=_selector.Start(true);
            var inputBounds=Native.Bounds(_selector.Handle);
            void Point(uint msg,double x,double y)
            {
                int packed=((ushort)(short)(int)(x-inputBounds.X))|((int)(ushort)(short)(int)(y-inputBounds.Y)<<16);
                SendSelectionTestMessage(_selector.Handle,msg,msg==0x202?0u:1u,(nint)packed);
            }
            double cx=context.Bounds.X+200,cy=context.Bounds.Y+180;
            Point(0x201,cx+50,cy);
            for(int i=1;i<25;i++) { double angle=i*Math.PI*2/24;Point(0x200,cx+50*Math.Cos(angle),cy+40*Math.Sin(angle)); }
            Point(0x202,cx+50,cy);
            var selected=await selection;
            report["closedCircleSelectsArea"]=selected is { Width:>=99,Height:>=79 };
            _store.Settings.HistoryEnabled=false;_store.SaveExchange("unrecorded","answer");
            report["historyOffRespected"]=_store.SearchHistory("unrecorded",false).Count==0;
            _store.PinExchange("Synthetic pinned question","Synthetic pinned answer");
            var saved=_store.SearchHistory("pinned",true);
            report["explicitPinAndSearch"]=saved.Count==1;
            _store.SetPinned(saved[0].Id,false);report["unpinWorks"]=_store.SearchHistory("pinned",true).Count==0;
            _store.DeleteHistory();report["deleteClearsPinnedHistory"]=_store.SearchHistory("",false).Count==0;
            string legacyPath=Path.Combine(directory,"legacy-profile-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(legacyPath);
            using(var legacyDb=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+Path.Combine(legacyPath,"history.db")))
            {
                legacyDb.Open();using var command=legacyDb.CreateCommand();
                command.CommandText="CREATE TABLE Messages (Id INTEGER PRIMARY KEY,Created TEXT NOT NULL,Content BLOB NOT NULL)";command.ExecuteNonQuery();
                command.CommandText="INSERT INTO Messages(Created,Content) VALUES('fixture',$data)";
                var encrypted=System.Security.Cryptography.ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes("{\"question\":\"Legacy question\",\"answer\":\"Legacy answer\"}"),null,System.Security.Cryptography.DataProtectionScope.CurrentUser);
                command.Parameters.AddWithValue("$data",encrypted);command.ExecuteNonQuery();
            }
            using(var legacy=new LocalStore(legacyPath))
            {
                var entry=legacy.SearchHistory("Legacy",false).Single();legacy.SetPinned(entry.Id,true);
                report["legacyHistoryMigrationPreservesContent"]=legacy.SearchHistory("Legacy",true).Single().Answer=="Legacy answer";
            }
            using(var wave=new MemoryStream())
            {
                using(var synth=new SpeechSynthesizer()) { synth.SetOutputToWaveStream(wave);synth.Speak("Open calculator.");synth.SetOutputToNull(); }
                wave.Position=0;
                using var speech=new LocalSpeechService();var recognized=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                speech.Transcript+=text=>recognized.TrySetResult(text);speech.Start(wave);
                string transcript=await speech.FinishAsync();speech.CancelListening();
                report["offlineVoiceCommandRecognized"]=VoiceCommandParser.Parse(transcript)==new VoiceCommand("open","calculator");
            }
            report["circleHotkeyRegistered"]=_tray.CircleHotkeyAvailable;
            report["commandHotkeyRegistered"]=_tray.CommandHotkeyAvailable;
            _provider=new LittleGuy3000.Codex.CodexProvider();
            await _provider.ConnectAsync(Environment.GetEnvironmentVariable("LITTLEGUY_FIXTURE_EXE")!,Path.Combine(_store.DirectoryPath,"circle-fixture"));
            _availableModels=await _provider.ModelsAsync();_connected=true;Settings.ScreenEnabled=true;
            _snapshot=await _capture.CaptureAsync(context,new([]),CancellationToken.None,selected!.Value);
            var selectedTransform=_snapshot.Transform;_target=context;_circlePending=true;_compactMode=true;
            await AskCircleAsync("What is the circled item?");
            report["circleAnswerUsesSelectedImage"]=AnswerText.Text=="Synthetic answer."&&_snapshot?.Transform==selectedTransform;
            using(var wave=new MemoryStream())
            {
                using(var synth=new SpeechSynthesizer()) { synth.SetOutputToWaveStream(wave);synth.Speak("What does this do?");synth.SetOutputToNull(); }
                wave.Position=0;QuestionBox.Text="";_lifetime.Restart();_specialVoice=SpecialVoice.Circle;_speech.Start(wave);
                await FinishSpecialVoiceAsync();
                report["spokenCircleQuestionReachesAnswer"]=QuestionText.Text.Contains("does",StringComparison.OrdinalIgnoreCase)&&AnswerText.Text=="Synthetic answer.";
            }
            var noAction=await DesktopCommands.ExecuteAsync(VoiceCommandParser.Parse("delete all my files"),CancellationToken.None);
            report["unsupportedActionDoesNotExecute"]=noAction==DesktopCommands.Help;
            using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();try { await DesktopCommands.ExecuteAsync(new("open","calculator"),cancelled.Token);report["cancelBlocksAction"]=false; }
                catch(OperationCanceledException) { report["cancelBlocksAction"]=true; }
            }
            HideApp();report["hideClosesBubbleAndSelection"]=!_bubble.Visible&&!_selector.Active;
        }
        catch(Exception ex) { report["error"]=ex.ToString(); }
        await File.WriteAllTextAsync(Path.Combine(directory,"roadmap-report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true}));
        await QuitAsync();
    }
}
