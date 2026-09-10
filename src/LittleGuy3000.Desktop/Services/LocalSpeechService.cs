using System.Speech.Synthesis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace LittleGuy3000.Desktop.Services;

/// <summary>Push-to-talk audio stays in memory and is decoded on this PC. No network fallback.</summary>
internal sealed class LocalSpeechService : IDisposable
{
    public const string ModelName = "ggml-large-v3-turbo-q5_0.bin";
    private const int SampleRate = 16000, MaximumSamples = SampleRate * 60;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _decodeGate = new(1);
    private readonly SpeechSynthesizer _synthesis = new();
    private readonly string _modelPath;
    private Task<WhisperFactory>? _factory;
    private Session? _current;
    private bool _disposed;
    public float LastConfidence { get; private set; } = 1;
    public bool Listening { get { lock (_sync) return _current is { } s && !s.Done.Task.IsCompleted; } }
    public bool HasSession { get { lock (_sync) return _current is not null; } }
    public bool Recording { get { lock (_sync) return _current is { Recording: true }; } }
    public bool RecognitionAvailable => File.Exists(_modelPath);
    public string RecognitionDescription => RecognitionAvailable
        ? "Whisper large-v3-turbo · local transcription · " + (RuntimeOptions.LoadedLibrary?.ToString() ?? "GPU preferred, CPU fallback")
        : "Offline speech model missing. Run scripts/Install-SpeechModel.ps1 and rebuild, or restore the Models folder from the portable package.";
    public string? MicrophoneName { get; set; }
    public event Action<string>? Transcript;
    public event Action<string>? PartialTranscript;
    public event Action<int>? AudioLevel;
    public event Action? SpeechEnded;
    public event Action<string>? Error;

    private sealed class Session
    {
        public readonly CancellationTokenSource Cancel = new();
        public readonly TaskCompletionSource<string> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly MemoryStream Pcm = new();
        public WaveInEvent? Input;
        public bool Recording;
        public int Processing;
    }

    public LocalSpeechService()
    {
        _modelPath = Environment.GetEnvironmentVariable("LITTLEGUY_SPEECH_MODEL")
            ?? Path.Combine(AppContext.BaseDirectory, "Models", ModelName);
        RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];
        _synthesis.SpeakCompleted += (_, _) => SpeechEnded?.Invoke();
    }
    public static IReadOnlyList<string> Microphones()
    {
        var names = new List<string>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++) names.Add(WaveInEvent.GetCapabilities(i).ProductName);
        return names;
    }
    public void Start(Stream? syntheticWave = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!RecognitionAvailable) throw new InvalidOperationException(RecognitionDescription);
        CancelListening(); StopSpeaking();
        var session = new Session();
        lock (_sync) { _current = session; LastConfidence = 1; }
        if (syntheticWave is not null)
        {
            // Only test fixtures provide WAV files. Actual capture never writes audio to disk.
            using var reader = new WaveFileReader(syntheticWave);
            ISampleProvider samples = reader.ToSampleProvider();
            if (samples.WaveFormat.Channels == 2) samples = new StereoToMonoSampleProvider(samples);
            if (samples.WaveFormat.Channels != 1) throw new InvalidDataException("Speech input must be mono or stereo.");
            samples = new WdlResamplingSampleProvider(samples, SampleRate);
            var all = new List<float>(); var buffer = new float[4096]; int count;
            while ((count = samples.Read(buffer, 0, Math.Min(buffer.Length, MaximumSamples - all.Count))) > 0) all.AddRange(buffer.Take(count));
            _ = DecodeAsync(session, all.ToArray());
            return;
        }
        try
        {
            int device = -1;
            if (!string.IsNullOrEmpty(MicrophoneName))
            {
                device = Microphones().ToList().FindIndex(x => x == MicrophoneName);
                if (device < 0) throw new InvalidOperationException("Your selected microphone is disconnected. Choose another microphone in Settings.");
            }
            session.Input = new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(SampleRate, 16, 1), BufferMilliseconds = 50 };
            session.Input.DataAvailable += (_, e) =>
            {
                int peak = 0; bool full;
                lock (session.Pcm)
                {
                    if (session.Cancel.IsCancellationRequested) return;
                    int bytes = (int)Math.Min(e.BytesRecorded, MaximumSamples * 2 - session.Pcm.Length);
                    session.Pcm.Write(e.Buffer, 0, bytes);
                    for (int i = 0; i + 1 < bytes; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(e.Buffer, i)));
                    full = session.Pcm.Length >= MaximumSamples * 2;
                }
                Publish(session, () => AudioLevel?.Invoke(Math.Min(100, peak * 100 / 32768)));
                if (full) StopCapture(session);
            };
            session.Input.RecordingStopped += (_, e) =>
            {
                session.Recording = false;
                Interlocked.Exchange(ref session.Input, null)?.Dispose();
                if (e.Exception is not null)
                {
                    session.Done.TrySetException(new InvalidOperationException("The microphone stopped. Check its connection and Windows microphone access."));
                    Publish(session, () => Error?.Invoke("The microphone stopped. Check its connection and Windows microphone access."));
                    return;
                }
                byte[] pcm;
                lock (session.Pcm) { pcm = session.Pcm.ToArray(); if (session.Pcm.TryGetBuffer(out var recorded)) Array.Clear(recorded.Array!); session.Pcm.SetLength(0); }
                var audio = new float[pcm.Length / 2];
                for (int i = 0; i < audio.Length; i++) audio[i] = BitConverter.ToInt16(pcm, i * 2) / 32768f;
                Array.Clear(pcm);
                _ = DecodeAsync(session, audio);
            };
            session.Recording = true;
            session.Input.StartRecording();
            // Capture starts before expensive model initialization, preserving the first word.
            _ = GetFactory();
        }
        catch { CancelListening(); throw; }
    }
    private Task<WhisperFactory> GetFactory()
    {
        lock (_sync) return _factory ??= Task.Run(() => WhisperFactory.FromPath(_modelPath));
    }
    private async Task DecodeAsync(Session session, float[] audio)
    {
        if (Interlocked.Exchange(ref session.Processing, 1) != 0) { Array.Clear(audio); return; }
        var token = session.Cancel.Token; bool entered = false;
        try
        {
            token.ThrowIfCancellationRequested();
            // Reject near-silent input before the model can hallucinate a command.
            if (audio.Length < SampleRate / 5 || audio.Count(x => Math.Abs(x) > .002f) < SampleRate / 20)
            { session.Done.TrySetResult(""); return; }
            Publish(session, () => PartialTranscript?.Invoke("Transcribing on your PC…"));
            await _decodeGate.WaitAsync(token); entered = true;
            var factory = await GetFactory().WaitAsync(token);
            await using var processor = factory.CreateBuilder().WithLanguage("en").WithNoContext()
                .WithThreads(Math.Clamp(Environment.ProcessorCount / 2, 2, 12))
                .WithPrompt("Little Guy 3000, Spotify, Notepad, Calculator, song, playlist.")
                .WithProbabilities().Build();
            var segments = new List<string>(); float confidence = 1;
            await foreach (var segment in processor.ProcessAsync(audio, token))
            {
                if (segment.NoSpeechProbability > .6f || string.IsNullOrWhiteSpace(segment.Text)) continue;
                segments.Add(segment.Text.Trim()); confidence = Math.Min(confidence, segment.Probability);
            }
            token.ThrowIfCancellationRequested();
            string text = string.Join(" ", segments);
            Publish(session, () => { LastConfidence = confidence; if (text.Length > 0) Transcript?.Invoke(text); });
            session.Done.TrySetResult(text);
        }
        catch (OperationCanceledException) { session.Done.TrySetCanceled(); }
        catch (Exception ex) { session.Done.TrySetException(new InvalidOperationException("Local transcription couldn't start. Check that the speech model and runtime files are installed, then restart Little Guy.", ex)); }
        finally { Array.Clear(audio); if (entered) _decodeGate.Release(); }
    }
    private void Publish(Session session, Action action)
    {
        lock (_sync) if (ReferenceEquals(_current, session) && !session.Cancel.IsCancellationRequested && !_disposed) action();
    }
    private static void StopCapture(Session session)
    {
        if (!session.Recording) return;
        session.Recording = false; session.Input?.StopRecording();
    }
    public void StopListening() { Session? session; lock (_sync) session = _current; if (session is not null) StopCapture(session); }
    public async Task<string> FinishAsync()
    {
        Session? session; lock (_sync) session = _current;
        if (session is null) return "";
        StopCapture(session);
        try { return await session.Done.Task.WaitAsync(TimeSpan.FromSeconds(45)); }
        catch (TimeoutException) { CancelListening(); throw new InvalidOperationException("Local transcription took too long. Try a shorter request."); }
    }
    public void CancelListening()
    {
        Session? session; lock (_sync) { session = _current; _current = null; }
        if (session is null) return;
        session.Cancel.Cancel(); session.Done.TrySetCanceled();
        StopCapture(session); Interlocked.Exchange(ref session.Input, null)?.Dispose();
        lock (session.Pcm) { if (session.Pcm.TryGetBuffer(out var data)) Array.Clear(data.Array!); session.Pcm.SetLength(0); }
    }
    public void Speak(string text) { if (string.IsNullOrWhiteSpace(text)) return; _synthesis.SetOutputToDefaultAudioDevice(); _synthesis.SpeakAsync(text); }
    public void StopSpeaking() => _synthesis.SpeakAsyncCancelAll();
    public void SetRate(int rate) => _synthesis.Rate = Math.Clamp(rate, -5, 5);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; CancelListening(); StopSpeaking(); _synthesis.Dispose();
        // Dispose the model only after any outstanding decoder releases it.
        _ = Task.Run(async () => { await _decodeGate.WaitAsync(); try { if (_factory is not null) (await _factory).Dispose(); } catch { } finally { _decodeGate.Release(); } });
    }
}
