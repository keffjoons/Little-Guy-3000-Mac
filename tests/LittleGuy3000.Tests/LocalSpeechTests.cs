using System.Diagnostics;
using System.Speech.Synthesis;
using System.Text.Json;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Services;
using NAudio.Wave;

internal static class LocalSpeechTests
{
    public static async Task<int> RunAsync(bool cpuOnly = false)
    {
        var checks = new List<object>(); bool passed = true;
        using var speech = new LocalSpeechService();
        if (cpuOnly) Whisper.net.LibraryLoader.RuntimeOptions.RuntimeLibraryOrder = [Whisper.net.LibraryLoader.RuntimeLibrary.Cpu];
        var cases = new (string Text, string Expected, int Rate)[] {
            ("Spotify.", "Spotify", 0),
            ("Open Spotify.", "open:spotify", 0),
            ("Open my Spotify and play my playlist called morning focus.", "spotify_play", 1),
            ("Play the song Imagine on Spotify.", "spotify_play", -1),
            ("What does this button do?", "button", 0),
            ("Delete all my files.", "unsupported", 0)
        };
        foreach (var test in cases)
        {
            if (cpuOnly && test.Expected != "open:spotify") continue;
            using var wave = MakeWave(test.Text, test.Rate);
            var timer = Stopwatch.StartNew(); speech.Start(wave);
            string text = await speech.FinishAsync();
            var command = VoiceCommandParser.Parse(text);
            bool ok = test.Expected switch {
                "open:spotify" => command == new VoiceCommand("open", "spotify") && speech.LastConfidence >= .6f,
                "spotify_play" => command.Action == "spotify_play" && speech.LastConfidence >= .6f,
                "unsupported" => command.Action == "unsupported",
                _ => text.Contains(test.Expected, StringComparison.OrdinalIgnoreCase)
            };
            passed &= ok;
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {test.Text} => {text} ({timer.ElapsedMilliseconds} ms; confidence {speech.LastConfidence:F2})");
            checks.Add(new { test.Text, Transcript = text, Milliseconds = timer.ElapsedMilliseconds, speech.LastConfidence, Passed = ok });
        }
        if (cpuOnly) { Console.WriteLine(speech.RecognitionDescription); return passed ? 0 : 1; }
        using (var silence = new MemoryStream())
        {
            using (var writer = new WaveFileWriter(new NAudio.Utils.IgnoreDisposeStream(silence), new WaveFormat(16000, 16, 1))) writer.Write(new byte[64000], 0, 64000);
            silence.Position = 0; speech.Start(silence);
            bool ok = (await speech.FinishAsync()) == ""; passed &= ok; checks.Add(new { SilenceProducesNoCommand = ok });
        }
        int lateEvents = 0;
        using (var old = MakeWave("Open calculator.", 0))
        {
            speech.Transcript += Count;
            speech.Start(old); var cancelled = speech.FinishAsync(); speech.CancelListening();
            try { await cancelled; passed = false; } catch (OperationCanceledException) { }
            await Task.Delay(250);
            speech.Transcript -= Count;
            bool ok = lateEvents == 0; passed &= ok; checks.Add(new { CancelSuppressesTranscript = ok });
            void Count(string text) => Interlocked.Increment(ref lateEvents);
        }
        using (var next = MakeWave("Open Spotify.", 0))
        {
            speech.Start(next); bool ok = VoiceCommandParser.Parse(await speech.FinishAsync()) == new VoiceCommand("open", "spotify");
            passed &= ok; checks.Add(new { NextRecordingAfterCancel = ok });
        }
        speech.MicrophoneName = "Unavailable synthetic microphone ID";
        bool disconnected = false;
        try { speech.Start(); } catch (InvalidOperationException) { disconnected = true; }
        passed &= disconnected; checks.Add(new { MissingSelectedMicrophoneDoesNotFallBack = disconnected });
        Console.WriteLine(JsonSerializer.Serialize(new { speech.RecognitionDescription, Passed = passed, Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
        return passed ? 0 : 1;
    }
    private static MemoryStream MakeWave(string text, int rate)
    {
        var wave = new MemoryStream();
        using var synth = new SpeechSynthesizer { Rate = rate };
        synth.SetOutputToWaveStream(wave); synth.Speak(text); synth.SetOutputToNull(); wave.Position = 0;
        return wave;
    }
}
