using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;
using NAudio.Wave;

namespace LittleGuy3000.Desktop.Services;

/// <summary>Local neural narration. Cancellation silences playback and invalidates queued synthesis.</summary>
internal sealed class NaturalNarrator : IDisposable
{
    internal static readonly (string Id, string Label)[] Voices =
    [
        ("af_heart", "Heart · American · natural"),
        ("af_bella", "Bella · American · natural"),
        ("am_michael", "Michael · American · natural"),
        ("bf_emma", "Emma · British · natural"),
        ("bm_george", "George · British · natural"),
        ("windows", "Windows default · classic")
    ];
    private readonly object _sync = new();
    private CancellationTokenSource _session = new();
    private Task _tail = Task.CompletedTask;
    private KokoroWavSynthesizer? _engine;
    private WaveOutEvent? _output;
    private bool _disposed;
    public event Action<string>? Error;
    public event Action? PlaybackStarted;
    public event Action? Finished;

    public Task SpeakAsync(string text, string voice, int rate, int pitch = 0)
    {
        lock (_sync)
        {
            if (_disposed || string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;
            var token = _session.Token;
            var previous = _tail;
            return _tail = Task.Run(async () =>
            {
                await previous.ConfigureAwait(false);
                try
                {
                    foreach (string chunk in Chunks(text))
                    {
                        token.ThrowIfCancellationRequested();
                        _engine ??= new KokoroWavSynthesizer(Path.Combine(AppContext.BaseDirectory, "Models", "kokoro.onnx"));
                        token.ThrowIfCancellationRequested();
                        var selected = KokoroVoiceManager.GetVoice(voice);
                        byte[] bytes = await _engine.SynthesizeAsync(chunk, selected,
                            new KokoroTTSPipelineConfig { Speed = 1f + Math.Clamp(rate, -5, 5) * .07f }).ConfigureAwait(false);
                        try
                        {
                            token.ThrowIfCancellationRequested();
                            if (bytes.Length == 0) throw new InvalidOperationException("The voice returned no audio.");
                            await PlayAsync(bytes, pitch, token).ConfigureAwait(false);
                        }
                        finally { Array.Clear(bytes); }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { if (!token.IsCancellationRequested) Error?.Invoke("Speech couldn't play: " + ex.GetBaseException().Message); }
                finally { if (!token.IsCancellationRequested) Finished?.Invoke(); }
            });
        }
    }

    private async Task PlayAsync(byte[] bytes, int pitch, CancellationToken token)
    {
        using var stream = new RawSourceWaveStream(new MemoryStream(bytes, false), new WaveFormat(24000, 16, 1));
        using var output = new WaveOutEvent();
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, e) => { if (e.Exception is null) ended.TrySetResult(); else ended.TrySetException(e.Exception); };
        var shifted = new NAudio.Wave.SampleProviders.SmbPitchShiftingSampleProvider(stream.ToSampleProvider()) { PitchFactor = (float)Math.Pow(2, Math.Clamp(pitch, -6, 12) / 12.0) };
        output.Init(shifted);
        try
        {
            lock (_sync)
            {
                token.ThrowIfCancellationRequested();
                _output = output;
                output.Play();
                PlaybackStarted?.Invoke();
            }
            await ended.Task.ConfigureAwait(false);
        }
        finally { lock (_sync) { if (ReferenceEquals(_output, output)) _output = null; } }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _session.Cancel(); _session.Dispose(); _session = new();
            _output?.Stop();
        }
    }

    private static IEnumerable<string> Chunks(string text)
    {
        // Bound inference time so stopping a long answer doesn't delay the next request.
        while (text.Length > 280)
        {
            int end = text.LastIndexOfAny(['.', '!', '?', '\n'], 279, 220) + 1;
            if (end < 60) end = text.LastIndexOf(' ', 279, 220);
            if (end < 60) end = 280;
            yield return text[..end]; text = text[end..].TrimStart();
        }
        if (!string.IsNullOrWhiteSpace(text)) yield return text;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            Stop(); _disposed = true; _session.Dispose();
            // The library cannot interrupt an ONNX inference; dispose only after it returns.
            _ = _tail.ContinueWith(_ => _engine?.Dispose(), TaskScheduler.Default);
        }
    }
}
