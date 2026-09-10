using System.Net;
using System.Text;

namespace LittleGuy3000.Core;

public sealed record ResearchFinding(string Topic, string Finding, string SourceTitle, string SourceUrl);
public sealed record ResearchReport(string Title, string Summary, List<ResearchFinding> Findings)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title) || Title.Length > 180 || string.IsNullOrWhiteSpace(Summary) || Summary.Length > 6000 || Findings is not { Count: > 0 and <= 40 })
            throw new InvalidDataException("Research returned an incomplete report. No document was uploaded.");
        foreach (var row in Findings)
            if (string.IsNullOrWhiteSpace(row.Finding) || row.Finding.Length > 6000 || row.Topic.Length > 300 || row.SourceTitle.Length > 500 || !PublicSource(row.SourceUrl))
                throw new InvalidDataException("Research returned an invalid source or finding. No document was uploaded.");
    }
    public static bool PublicSource(string value) => Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme == "https" && !url.IsLoopback && url.UserInfo == "" && url.Host.Contains('.') && !IPAddress.TryParse(url.Host, out _);
    public string ToHtml()
    {
        Validate();
        static string E(string s) => WebUtility.HtmlEncode(s);
        var html = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"></head><body>");
        html.Append($"<h1>{E(Title)}</h1><p>{E(Summary)}</p><p>Prepared by Little Guy 3000 · {DateTimeOffset.Now:yyyy-MM-dd}</p>");
        foreach (var row in Findings) html.Append($"<h2>{E(row.Topic)}</h2><p>{E(row.Finding)}</p><p>Source: <a href=\"{E(row.SourceUrl)}\">{E(row.SourceTitle)}</a></p>");
        return html.Append("</body></html>").ToString();
    }
    public string ToCsv()
    {
        Validate();
        static string Cell(string value)
        {
            // Prevent researched text from becoming an executable spreadsheet formula.
            if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        var csv = new StringBuilder("Topic,Finding,Source title,Source URL\r\n");
        csv.AppendLine(string.Join(',', new[] { "Report", Title + " — " + Summary, "Little Guy 3000", "" }.Select(Cell)));
        foreach (var row in Findings) csv.AppendLine(string.Join(',', new[] { row.Topic, row.Finding, row.SourceTitle, row.SourceUrl }.Select(Cell)));
        return csv.ToString();
    }
}

public static class ChatGptHandoff
{
    // Verified against the installed ChatGPT desktop deep-link parser (26.901.6511).
    public static Uri NewChat(string prompt = "", bool work = false)
    {
        if (prompt.Length > 6000) throw new ArgumentException("Keep the ChatGPT handoff under 6,000 characters.");
        return new("codex://threads/new?mode=" + (work ? "work" : "chat") + (string.IsNullOrWhiteSpace(prompt) ? "" : "&prompt=" + Uri.EscapeDataString(prompt)));
    }
}
