namespace LittleGuy3000.Core;

public static class CompactReply
{
    public static (PixelRect Bubble, PixelPoint CompanionAnchor) PlaceAbove(PixelPoint anchor, PixelRect work, double width, double height, double scale)
    {
        double size = Math.Min(74 * scale, work.Height / 3), gap = Math.Min(44 * scale, work.Height / 6);
        width = Math.Min(width, work.Width); height = Math.Min(height, work.Height - size - gap);
        double guyX = Math.Clamp(anchor.X + 28 * scale, work.X, Math.Max(work.X, work.Right - size));
        double guyY = Math.Clamp(anchor.Y + 28 * scale, work.Y + height + gap, work.Bottom - size);
        double x = Math.Clamp(guyX + size / 2 - width / 2, work.X, work.Right - width);
        return (new(x, guyY - gap - height, width, height), new(guyX - 28 * scale, guyY - 28 * scale));
    }
    public static string Preview(string? summary, string answer)
    {
        string text = string.Join(" ", (string.IsNullOrWhiteSpace(summary) ? answer : summary).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length <= 240) return text;
        int boundary = text.LastIndexOf(' ', 236);
        return text[..(boundary > 100 ? boundary : 236)].TrimEnd() + "…";
    }
    public static PixelRect Place(PixelPoint anchor, PixelRect work, double width, double height, double scale, double horizontalGap = 104)
    {
        width = Math.Min(width, work.Width); height = Math.Min(height, work.Height);
        double x = anchor.X + horizontalGap * scale;
        if (x + width > work.Right) x = anchor.X - width - 18 * scale;
        return new(Math.Clamp(x, work.X, work.Right - width), Math.Clamp(anchor.Y + 18 * scale, work.Y, work.Bottom - height), width, height);
    }
}
