using System.Text.RegularExpressions;

namespace LittleGuy3000.Core;

public sealed record VoiceCommand(string Action, string Target = "");
public static class VoiceCommandParser
{
    private const RegexOptions IgnoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static string Normalize(string text)
    {
        text = Regex.Replace(text.Trim(), @"\s+", " ").TrimEnd('.', '?', '!').Trim();
        // Speech often combines a wake phrase with a polite request. Remove prefixes only;
        // punctuation and words inside song/playlist names must be preserved.
        for (int i = 0; i < 4; i++)
        {
            string next = Regex.Replace(text, @"^(?:(?:hey\s+)?little guy(?:\s+3000)?[,\s]+|(?:can you|could you|would you|please)\s+)", "", IgnoreCase);
            if (next == text) break;
            text = next.Trim();
        }
        return text;
    }
    public static bool LooksLikeDesktopRequest(string text)
        => Regex.IsMatch(Normalize(text), @"^(?:research|do (?:some )?research|find out|look up|ask chat\s*gpt|open|launch|start|play|pause|resume|stop|skip|next|previous|go back)\b", IgnoreCase);

    public static VoiceCommand Parse(string text)
    {
        text = Normalize(text);
        var research = Regex.Match(text, @"^(?:research|do (?:some )?research (?:on|about)|look up|find out about)\s+(.+)$", IgnoreCase);
        if (research.Success && research.Groups[1].Value.Length <= 4000) return new("research", research.Groups[1].Value);
        var chat = Regex.Match(text, @"^(?:(?:open|launch|start)\s+(?:the\s+)?chat\s*gpt(?:\s+app)?(?:[,;.]?\s+(?:and\s+)?(?:start|open|create)\s+(?:a\s+)?(?:new\s+)?(?:chat|thread)(?:\s+(?:about|for|to)\s+(.+))?)?|(?:start|open|create)\s+(?:a\s+)?(?:new\s+)?(?:chat|thread)\s+(?:in|on|with)\s+chat\s*gpt(?:\s+about\s+(.+))?|ask\s+chat\s*gpt\s+(.+))$", IgnoreCase);
        if (chat.Success) return new("chatgpt", string.Join("", chat.Groups.Cast<Group>().Skip(1).Select(g => g.Value)));
        // Recognize only the supported Spotify + media combination, not an arbitrary
        // command sequence. A comma/period is a separator here, never inside a title.
        var compound = Regex.Match(text, @"^(?:open|launch|start)\s+(?:up\s+)?(?:my\s+)?spotify(?:\s*[,;.]\s*(?:(?:and\s+then|and|then)\s+)?|\s+(?:and\s+then|and|then)\s+)(.+)$", IgnoreCase);
        bool openSpotify = compound.Success;
        if (openSpotify)
        {
            text = Normalize(compound.Groups[1].Value);
            if (!Regex.IsMatch(text, @"^(?:play|resume|pause|stop|next|skip|previous|go back)\b", IgnoreCase)) return new("unsupported");
        }
        if (Regex.IsMatch(text,@"^(?:pause|stop)(?:\s+(?:the\s+)?(?:music|playback|spotify))?$",RegexOptions.IgnoreCase)) return new("pause");
        if (Regex.IsMatch(text,@"^(?:next|skip)(?:\s+(?:song|track))?$",RegexOptions.IgnoreCase)) return new("next");
        if (Regex.IsMatch(text,@"^(?:previous|go back)(?:\s+(?:song|track))?$",RegexOptions.IgnoreCase)) return new("previous");
        if (Regex.IsMatch(text,@"^(?:play|resume)(?:\s+(?:the\s+)?(?:music|playback|spotify))?$",RegexOptions.IgnoreCase)) return new("play",openSpotify || text.EndsWith("spotify",StringComparison.OrdinalIgnoreCase)?"spotify":"");
        var open = Regex.Match(text,@"^(?:open|launch|start)\s+(?:up\s+)?(?:my\s+)?(.+)$",RegexOptions.IgnoreCase);
        if (open.Success)
        {
            string app = open.Groups[1].Value.ToLowerInvariant();
            app = app switch { "file explorer"=>"explorer", "windows settings"=>"settings", "calc"=>"calculator", _=>app };
            if (app is "spotify" or "notepad" or "calculator" or "explorer" or "settings") return new("open",app);
        }
        var song = Regex.Match(text,@"^play\s+(?:(?:the|my|a)\s+)?(?:(?:song|track|playlist)\s+(?:(?:called|named)\s+)?)?(.+?)(?:\s+(?:on|in)\s+spotify)?$",RegexOptions.IgnoreCase);
        if (song.Success && song.Groups[1].Value.Length <= 160) return new("spotify_play",song.Groups[1].Value.Trim(' ', '"'));
        return new("unsupported");
    }
}
