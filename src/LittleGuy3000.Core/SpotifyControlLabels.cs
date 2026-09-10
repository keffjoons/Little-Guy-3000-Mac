using System.Text.RegularExpressions;

namespace LittleGuy3000.Core;

public static class SpotifyControlLabels
{
    private static string Normalize(string value) => Regex.Replace(value.Trim(), @"\s+", " ").ToLowerInvariant();
    public static bool IsLibraryPlaylist(string query, string label)
    {
        var match = Regex.Match(label, @"^(.+?)\s+(?:(?:Pinned|Mixed|Prompted)\s+)?Playlist\s*[•·]", RegexOptions.IgnoreCase);
        return match.Success && Normalize(match.Groups[1].Value) == Normalize(query);
    }
    public static bool IsPlaybackButton(string query, string label, bool paused = false)
    {
        string prefix = paused ? "Pause " : "Play ";
        if (!label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        string target = Normalize(label[prefix.Length..]), requested = Normalize(query);
        return requested.Length > 0 && (target == requested || target.StartsWith(requested + " by ", StringComparison.Ordinal));
    }
    public static string SearchArgument(string query) => "--protocol-uri=spotify:search:" + Uri.EscapeDataString(query);
}
