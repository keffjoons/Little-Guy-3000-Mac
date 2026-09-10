using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace LittleGuy3000.Codex;

/// <summary>Owns one isolated JSONL app-server process. Never logs wire payloads.</summary>
public sealed class CodexClient : IAsyncDisposable
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1);
    private readonly CancellationTokenSource _lifetime = new();
    private Process? _process;
    private Task? _readerTask;
    private Task? _errorTask;
    private long _nextId;
    private nint _job;
    public event Action<string, JsonElement>? Notification;
    public event Action? Disconnected;
    public bool IsConnected => _process is { HasExited: false };

    public async Task StartAsync(string executable, string dataDirectory, CancellationToken cancellationToken = default)
    {
        if (_process is not null) throw new InvalidOperationException("A Codex process is already owned by this client.");
        Directory.CreateDirectory(dataDirectory);
        string work = Path.Combine(dataDirectory, "empty-workspace");
        Directory.CreateDirectory(work);
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "config.toml"), GuideConfiguration.Text, cancellationToken);
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = work
        };
        start.ArgumentList.Add("app-server");
        start.ArgumentList.Add("--listen"); start.ArgumentList.Add("stdio://");
        // Only this child receives a dedicated home. Never modify the user's Codex configuration.
        string[] inherited = ["SystemRoot", "WINDIR", "PATH", "PATHEXT", "TEMP", "TMP", "USERPROFILE", "LOCALAPPDATA", "APPDATA", "ProgramFiles", "ProgramFiles(x86)", "ProgramData", "COMSPEC", "DOTNET_ROOT", "DOTNET_ROOT_X64"];
        var allowed = inherited.Select(key => (key, value: Environment.GetEnvironmentVariable(key))).ToArray();
        start.Environment.Clear();
        foreach (var (key, value) in allowed) if (value is not null) start.Environment[key] = value;
        start.Environment["CODEX_HOME"] = dataDirectory;
        _process = Process.Start(start) ?? throw new IOException("Codex could not be started.");
        _job = ProcessJob.Attach(_process);
        _readerTask = ReadLoopAsync(_lifetime.Token);
        _errorTask = DrainErrorsAsync(_lifetime.Token);
        await RequestAsync("initialize", new
        {
            clientInfo = new { name = LittleGuy3000.Core.Brand.Identifier, title = LittleGuy3000.Core.Brand.Name, version = LittleGuy3000.Core.Brand.Version },
            capabilities = new { experimentalApi = true }
        }, cancellationToken);
        await WriteAsync(new { method = "initialized" }, cancellationToken);
    }

    public async Task<JsonElement> RequestAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
    {
        long id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(40));
        using var registration = timeout.Token.Register(() => completion.TrySetCanceled(timeout.Token));
        try
        {
            await WriteAsync(new Dictionary<string, object?> { ["id"] = id, ["method"] = method, ["params"] = parameters ?? new { } }, timeout.Token);
            return await completion.Task;
        }
        finally { _pending.TryRemove(id, out _); }
    }

    private async Task WriteAsync(object message, CancellationToken token)
    {
        string json = JsonSerializer.Serialize(message);
        if (json.Length > 24 * 1024 * 1024) throw new InvalidDataException("The request is too large. Capture a smaller window.");
        await _writeLock.WaitAsync(token);
        try
        {
            if (_process is null || _process.HasExited) throw new IOException("Couldn't connect to Codex.");
            await _process.StandardInput.WriteLineAsync(json.AsMemory(), token);
            await _process.StandardInput.FlushAsync(token);
        }
        finally { _writeLock.Release(); }
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await _process!.StandardOutput.ReadLineAsync(token);
                if (line is null) break;
                if (line.Length > 8 * 1024 * 1024) throw new InvalidDataException("Codex sent an oversized message.");
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("method", out var method))
                {
                    if (root.TryGetProperty("id", out var requestId))
                    {
                        // No server request may execute an action, acquire more permission or run a host tool.
                        object reply = method.GetString() switch
                        {
                            "item/commandExecution/requestApproval" or "item/fileChange/requestApproval" => new { id = requestId.Clone(), result = (object)new { decision = "decline" } },
                            _ => new { id = requestId.Clone(), error = new { code = -32601, message = "This capability is unavailable in Little Guy 3000 guide mode." } }
                        };
                        await WriteAsync(reply, token);
                    }
                    else if (root.TryGetProperty("params", out var body)) Notification?.Invoke(method.GetString()!, body.Clone());
                }
                else if (root.TryGetProperty("id", out var id) && id.TryGetInt64(out long number) && _pending.TryGetValue(number, out var request))
                {
                    if (root.TryGetProperty("error", out var error))
                        request.TrySetException(new CodexException(error.TryGetProperty("code", out var code) ? code.GetInt32() : -1));
                    else if (root.TryGetProperty("result", out var result)) request.TrySetResult(result.Clone());
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { /* Payloads and backend error strings may contain private context. */ }
        finally
        {
            foreach (var pending in _pending.Values) pending.TrySetException(new IOException("The Codex connection closed."));
            Disconnected?.Invoke();
        }
    }

    private async Task DrainErrorsAsync(CancellationToken token)
    {
        var buffer = new char[2048];
        try { while (await _process!.StandardError.ReadAsync(buffer.AsMemory(), token) > 0) Array.Clear(buffer); }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        if (_process is { HasExited: false })
        {
            try { _process.StandardInput.Close(); if (!_process.WaitForExit(500)) _process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
        if (_job != 0) ProcessJob.CloseHandle(_job);
        if (_readerTask is not null) await _readerTask;
        if (_errorTask is not null) await _errorTask;
        _process?.Dispose(); _writeLock.Dispose(); _lifetime.Dispose();
    }
}

public sealed class CodexException(int code) : Exception($"Codex couldn't complete this request (code {code}). Reconnect or check your account.");

public static class GuideConfiguration
{
    public const string Text = """
        cli_auth_credentials_store = "keyring"
        web_search = "disabled"
        approval_policy = "never"
        sandbox_mode = "read-only"
        include_environment_context = false
        include_apps_instructions = false
        include_collaboration_mode_instructions = false
        project_doc_max_bytes = 0
        [analytics]
        enabled = false
        [feedback]
        enabled = false
        [history]
        persistence = "none"
        [features]
        shell_tool = false
        shell_snapshot = false
        view_image = false
        apps = false
        plugins = false
        hooks = false
        browser_use = false
        browser_use_external = false
        computer_use = false
        multi_agent = false
        multi_agent_v2 = false
        memories = false
        skill_search = false
        skip_host_skill_discovery = true
        skill_mcp_dependency_install = false
        tool_suggest = false
        workspace_dependencies = false
        image_generation = false
        goals = false
        sleep_tool = false
        code_mode = false
        code_mode_host = false
        [tools.update_plan]
        enabled = false
        """;
}

internal static class ProcessJob
{
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    { public long PerProcess, PerJob; public uint Flags; public nuint MinWorkingSet, MaxWorkingSet; public uint ActiveLimit; public nuint Affinity; public uint Priority, Scheduling; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong A, B, C, D, E, F; }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
    { public BasicLimits Basic; public IoCounters Io; public nuint ProcessMemory, JobMemory, PeakProcess, PeakJob; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateJobObject(nint attributes, string? name);
    [DllImport("kernel32.dll")] private static extern bool SetInformationJobObject(nint job, int infoClass, ref ExtendedLimits limits, uint length);
    [DllImport("kernel32.dll")] private static extern bool AssignProcessToJobObject(nint job, nint process);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);
    internal static nint Attach(Process process)
    {
        if (!OperatingSystem.IsWindows()) return 0;
        nint job = CreateJobObject(0, null);
        var limits = new ExtendedLimits { Basic = new BasicLimits { Flags = 0x2000 } };
        if (job == 0 || !SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>()) || !AssignProcessToJobObject(job, process.Handle))
        { if (job != 0) CloseHandle(job); process.Kill(true); throw new IOException("Couldn't safely supervise the Codex process."); }
        return job;
    }
}
