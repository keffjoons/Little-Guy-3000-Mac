using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Platform;
using Windows.Media.Control;

namespace LittleGuy3000.Desktop.Services;

internal static class DesktopCommands
{
    public const string Help = "Try: Open Spotify, play [song or playlist] on Spotify, pause music, next song, previous song, open Calculator, Notepad, Explorer or Settings; open ChatGPT; ask ChatGPT [request]; or research [topic] and save to a Google Doc or Sheet.";
    private static string SpotifyPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe");
    public static async Task<string> ExecuteAsync(VoiceCommand command, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (command.Action == "unsupported") return Help;
        if (command.Action == "chatgpt")
        {
            Process.Start(new ProcessStartInfo(ChatGptHandoff.NewChat(command.Target).AbsoluteUri) { UseShellExecute = true })?.Dispose();
            return string.IsNullOrWhiteSpace(command.Target) ? "Opened a new chat in the ChatGPT desktop app." : "Opened ChatGPT with your request in a new chat. Press Send there to begin; Little Guy has not sent it automatically.";
        }
        if (command.Action == "open") { Open(command.Target); return $"Opened {command.Target}."; }
        if (command.Action == "spotify_play") return await PlayNamedAsync(command.Target, token);
        if (command.Action is "play" or "pause" or "next" or "previous")
        {
            if(command.Target=="spotify") { Open("spotify"); await Task.Delay(1200,token); }
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(token);
            var sessions = manager.GetSessions();
            var spotify = sessions.FirstOrDefault(s=>s.SourceAppUserModelId.Contains("spotify",StringComparison.OrdinalIgnoreCase));
            var session = spotify ?? (sessions.Count == 1 ? sessions[0] : null);
            if (session is null) return "No unambiguous media session is available. Open Spotify and start a track, then try again.";
            token.ThrowIfCancellationRequested();
            bool accepted = command.Action switch
            {
                "play" => await session.TryPlayAsync().AsTask(token),
                "pause" => await session.TryPauseAsync().AsTask(token),
                "next" => await session.TrySkipNextAsync().AsTask(token),
                _ => await session.TrySkipPreviousAsync().AsTask(token)
            };
            return accepted ? $"{command.Action switch { "play"=>"Playback started", "pause"=>"Playback paused", "next"=>"Skipped to the next track", _=>"Returned to the previous track" }}." : "The player did not accept that command.";
        }
        return Help;
    }
    private static void Open(string app, string? spotifyQuery = null)
    {
        string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        ProcessStartInfo start = app switch
        {
            "spotify" when File.Exists(SpotifyPath) => new(SpotifyPath) { UseShellExecute = false },
            "notepad" => new(Path.Combine(system,"notepad.exe")) { UseShellExecute = false },
            "calculator" => new(Path.Combine(system,"calc.exe")) { UseShellExecute = false },
            "explorer" => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")) { UseShellExecute = false },
            "settings" => new("ms-settings:") { UseShellExecute = true },
            _ => throw new InvalidOperationException("This app is unavailable. Supported apps: Spotify, Notepad, Calculator, Explorer and Settings.")
        };
        // Spotify's registered Windows protocol command uses --protocol-uri=<uri>.
        if (app == "spotify" && spotifyQuery is not null) start.ArgumentList.Add(SpotifyControlLabels.SearchArgument(spotifyQuery));
        Process.Start(start)?.Dispose();
    }
    private static async Task<string> PlayNamedAsync(string query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 160) return "Say the song or playlist name.";
        token.ThrowIfCancellationRequested();
        Open("spotify");
        var attempt = new SpotifyPlaybackAttempt();
        // Prefer a saved playlist with the exact name, then search using the registered URI syntax.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            while (!timeout.IsCancellationRequested)
            {
                await Task.Delay(600,timeout.Token);
                string? result = await Task.Run(()=>TryPlayVisibleResult(query,attempt,timeout.Token),timeout.Token).WaitAsync(TimeSpan.FromSeconds(4),timeout.Token);
                if (result is not null) return result;
                if (attempt.SawWindow && !attempt.LibraryOpened && !attempt.SearchIssued && !attempt.PlayRequested)
                {
                    token.ThrowIfCancellationRequested();
                    Open("spotify", query);
                    attempt.SearchIssued = true;
                }
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
        catch (TimeoutException) { timeout.Cancel(); }
        if (attempt.PlayRequested) return $"Asked Spotify to play {query}, but couldn't verify that playback started.";
        if (attempt.LibraryOpened) return $"Opened your {query} playlist, but couldn't access its Play control. Select Play in Spotify.";
        return attempt.SearchIssued ? $"Searched Spotify for {query}, but couldn't find one matching accessible Play control. Choose the result in Spotify."
            : "Spotify opened, but its controls aren't available. Bring Spotify to the foreground and try again.";
    }
    private sealed class SpotifyPlaybackAttempt
    {
        public bool SawWindow, ActivationAttempted, LibraryOpened, SearchIssued, PlayRequested;
    }
    private static string? TryPlayVisibleResult(string query, SpotifyPlaybackAttempt attempt, CancellationToken token)
    {
        foreach (var process in Process.GetProcessesByName("Spotify"))
        using (process)
        {
            token.ThrowIfCancellationRequested();
            nint handle = process.MainWindowHandle;
            if (handle == 0) continue;
            if (!attempt.ActivationAttempted)
            {
                token.ThrowIfCancellationRequested(); attempt.ActivationAttempted = true;
                Native.ShowWindow(handle, 9); Native.SetForegroundWindow(handle);
            }
            if (Native.GetForegroundWindow() != handle) continue;
            try
            {
                var root = AutomationElement.FromHandle(handle);
                attempt.SawWindow = true;
                if (!attempt.LibraryOpened && !attempt.PlayRequested)
                {
                    var library = root.FindFirst(TreeScope.Descendants, new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataGrid),
                        new PropertyCondition(AutomationElement.NameProperty, "Your Library")));
                    if (library is not null)
                    {
                        var entries = library.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                        var playlists = entries.Cast<AutomationElement>().Where(e => e.Current.IsEnabled && !e.Current.IsOffscreen
                            && SpotifyControlLabels.IsLibraryPlaylist(query, e.Current.Name)).Take(2).ToArray();
                        if (playlists.Length > 1) return $"Your library has multiple playlists named {query}. Choose the one you want in Spotify.";
                        if (playlists.Length == 1 && InvokeCurrent(playlists[0], handle, token)) { attempt.LibraryOpened = true; return null; }
                    }
                }
                var buttons = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));
                if (!attempt.PlayRequested && buttons.Cast<AutomationElement>().Any(e => e.Current.IsEnabled && !e.Current.IsOffscreen
                    && SpotifyControlLabels.IsPlaybackButton(query, e.Current.Name, true))) return $"{query} is already playing.";
                var matches = buttons.Cast<AutomationElement>().Where(e=>
                {
                    var info = e.Current;
                    return info.IsEnabled && !info.IsOffscreen && SpotifyControlLabels.IsPlaybackButton(query,info.Name,attempt.PlayRequested);
                }).Take(2).ToArray();
                if (attempt.PlayRequested) return matches.Length > 0 ? $"Playing {query}." : null;
                if (matches.Length > 1) return "Spotify found multiple matching Play buttons. Choose the version you want, then say ‘play music’.";
                if (matches.Length != 1) continue;
                if (InvokeCurrent(matches[0], handle, token)) attempt.PlayRequested = true;
                return null;
            }
            catch (ElementNotAvailableException) { }
            catch (InvalidOperationException) { }
        }
        return null;
    }
    private static bool InvokeCurrent(AutomationElement candidate, nint handle, CancellationToken token)
    {
        string original = candidate.Current.Name;
        if (!candidate.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern)) return false;
        token.ThrowIfCancellationRequested();
        if (Native.GetForegroundWindow() != handle || candidate.Current.Name != original || !candidate.Current.IsEnabled || candidate.Current.IsOffscreen) return false;
        ((InvokePattern)pattern).Invoke(); return true;
    }
}
