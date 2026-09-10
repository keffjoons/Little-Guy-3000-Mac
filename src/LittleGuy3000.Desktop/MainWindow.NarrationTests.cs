using System.Text.Json;
using LittleGuy3000.Desktop.Services;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private async Task RunNarrationCheckAsync(string directory)
    {
        var report = new Dictionary<string, object>();
        var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
        int starts = 0;
        _narrator.Error += errors.Enqueue;
        _narrator.PlaybackStarted += () => Interlocked.Increment(ref starts);
        try
        {
            // Each option must generate real, nonempty audio through the output device.
            foreach (var voice in NaturalNarrator.Voices.Where(v => v.Id != "windows"))
            {
                int before = starts;
                await _narrator.SpeakAsync("Hello. I'm Little Guy, your helpful desktop companion.", voice.Id, 0).WaitAsync(TimeSpan.FromSeconds(90));
                report["voice_" + voice.Id] = starts == before + 1;
            }
            int chipmunkBefore = starts;
            await _narrator.SpeakAsync("Hi, I'm Little Guy. Let's make something fun together!", "am_michael", 0, 9).WaitAsync(TimeSpan.FromSeconds(40));
            report["maleChipmunkPlayback"] = starts == chipmunkBefore + 1;
            int prior = starts;
            var pending = _narrator.SpeakAsync(new string('A', 260), "af_heart", 0);
            _narrator.Stop();
            await pending.WaitAsync(TimeSpan.FromSeconds(30));
            report["stopBeforePlaybackStaysSilent"] = starts == prior;

            var playing = _narrator.SpeakAsync("This is a longer spoken answer. The stop button should silence it immediately and discard every queued sentence that follows it.", "af_heart", -2);
            var queued = _narrator.SpeakAsync("This queued sentence must never play.", "af_heart", 0);
            for (int i = 0; i < 600 && starts == prior && !playing.IsCompleted; i++) await Task.Delay(50);
            report["playbackStartedBeforeStop"] = starts == prior + 1;
            StopNarration();
            await Task.WhenAll(playing, queued).WaitAsync(TimeSpan.FromSeconds(5));
            report["stopDiscardsQueuedAudio"] = starts == prior + 1;

            _lifetime.Restart(); _busy = true;
            long generation = _lifetime.Generation;
            StopNarration(); SpeakNewSentences("This sentence must stay silent after pressing stop.", true);
            report["stopPreservesAnswerGeneration"] = _busy && _lifetime.IsCurrent(generation) && _narrationSuppressed;
            report["lateStreamingSpeechSuppressed"] = starts == prior + 1;
            CancelActive();
            report["newRequestResetsSuppression"] = !_narrationSuppressed;
            await _narrator.SpeakAsync("Ready for your next question.", "af_heart", 0).WaitAsync(TimeSpan.FromSeconds(30));
            report["speechCanRestart"] = starts == prior + 2;
            NarrationVoiceCombo.SelectedIndex = 3;
            report["voiceSelectionSaved"] = Settings.NarrationVoice == "bf_emma" && File.ReadAllText(Path.Combine(_store.DirectoryPath, "settings.json")).Contains("bf_emma");
            report["noAudioErrors"] = errors.IsEmpty;
            if (!errors.IsEmpty) report["audioErrors"] = errors.ToArray();
        }
        catch (Exception ex) { report["error"] = ex.ToString(); }
        await File.WriteAllTextAsync(Path.Combine(directory, "narration-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        await QuitAsync();
    }
}
