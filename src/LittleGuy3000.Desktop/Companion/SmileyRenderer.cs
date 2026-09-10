using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using LittleGuy3000.Core;

namespace LittleGuy3000.Desktop.Companion;

/// <summary>Original vector character. No sprite assets, captured images, or system cursor replacement.</summary>
public static class SmileyRenderer
{
    public static Bitmap Render(int size, CompanionState state, double time, bool reducedMotion = false)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap); g.SmoothingMode = SmoothingMode.AntiAlias;
        g.ScaleTransform(size / 100f, size / 100f);
        double t = reducedMotion ? 0 : time;
        float bob = state == CompanionState.Success ? (float)(Math.Abs(Math.Sin(t * 5)) * -5) : (float)Math.Sin(t * 1.8) * 1.2f;
        g.TranslateTransform(0, bob);
        using var shadow = new SolidBrush(Color.FromArgb(30, 0, 0, 0)); g.FillEllipse(shadow, 19, 83 - bob, 62, 8);
        var color = state switch { CompanionState.Error => Color.FromArgb(255, 176, 131), CompanionState.Paused => Color.FromArgb(176, 186, 177), _ => Color.FromArgb(237, 248, 152) };
        using var face = new LinearGradientBrush(new Rectangle(13, 12, 74, 72), Color.FromArgb(252, 255, 206), color, 70f);
        using var ink = new SolidBrush(Color.FromArgb(37, 44, 37));
        using var line = new Pen(Color.FromArgb(37, 44, 37), 3.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var outline = new Pen(Color.FromArgb(160, 44, 53, 33), 1.4f);
        g.FillEllipse(face, 13, 12, 74, 72); g.DrawEllipse(outline, 13, 12, 74, 72);
        using var gleam = new Pen(Color.FromArgb(140, Color.White), 2.2f); g.DrawArc(gleam, 19, 17, 62, 59, 215, 68);
        bool blink = !reducedMotion && t % 5.8 > 5.58;
        float glanceX = state == CompanionState.Guiding ? 4 : state == CompanionState.Thinking ? -2 : 0;
        float glanceY = state == CompanionState.Thinking ? -4 : 0;
        if (state == CompanionState.Paused || blink)
        { g.DrawLine(line, 31, 43, 39, 43); g.DrawLine(line, 59, 43, 67, 43); }
        else if (state == CompanionState.Success)
        { g.DrawArc(line, 29, 37, 12, 12, 195, 150); g.DrawArc(line, 57, 37, 12, 12, 195, 150); }
        else
        {
            float eyeHeight = state == CompanionState.Listening ? 15 : 11;
            g.FillEllipse(ink, 31 + glanceX, 35 + glanceY, 8, eyeHeight); g.FillEllipse(ink, 59 + glanceX, 35 + glanceY, 8, eyeHeight);
            using var white = new SolidBrush(Color.FromArgb(240, 255, 255, 244));
            g.FillEllipse(white, 33 + glanceX, 37 + glanceY, 2.1f, 3); g.FillEllipse(white, 61 + glanceX, 37 + glanceY, 2.1f, 3);
        }
        if (state == CompanionState.Speaking)
        { float opening = 5 + (float)Math.Abs(Math.Sin(t * 10)) * 12; g.FillEllipse(ink, 40, 56, 20, opening); using var tongue = new SolidBrush(Color.FromArgb(255, 152, 137)); g.FillEllipse(tongue, 44, 58 + opening / 2, 12, opening / 3); }
        else if (state == CompanionState.Error) g.DrawArc(line, 38, 61, 24, 12, 205, 130);
        else if (state == CompanionState.Paused) g.DrawLine(line, 43, 62, 56, 62);
        else if (state == CompanionState.Thinking) g.DrawArc(line, 43, 57, 18, 9, 20, 125);
        else g.DrawArc(line, 35, 48, 30, 20, 20, 140);
        using var cheek = new SolidBrush(Color.FromArgb(50, 224, 132, 89)); g.FillEllipse(cheek, 25, 51, 10, 4); g.FillEllipse(cheek, 65, 51, 10, 4);
        if (state == CompanionState.Listening)
        { using var pulse = new Pen(Color.FromArgb((int)(95 + 50 * Math.Sin(t * 5)), 187, 236, 142), 2); float p = 3 + (float)(Math.Sin(t * 5) + 1) * 2; g.DrawEllipse(pulse, 13 - p, 12 - p, 74 + p * 2, 72 + p * 2); }
        if (state == CompanionState.Thinking)
            for (int i = 0; i < 3; i++) { double angle = t * 2 + i * 0.45; using var dot = new SolidBrush(Color.FromArgb(220 - i * 50, 133, 180, 112)); g.FillEllipse(dot, 47 + (float)Math.Cos(angle) * 42, 46 + (float)Math.Sin(angle) * 42, 6 - i, 6 - i); }
        if (state == CompanionState.Guiding)
        { g.DrawLine(line, 79, 58, 95, 52); g.DrawLine(line, 88, 49, 95, 52); g.DrawLine(line, 91, 59, 95, 52); }
        if (state == CompanionState.Error) { g.DrawLine(line, 29, 29, 41, 33); g.DrawLine(line, 57, 33, 69, 29); }
        return bitmap;
    }
    public static Icon CreateIcon()
    {
        using var bitmap = Render(64, CompanionState.Idle, 0, true);
        nint handle = bitmap.GetHicon();
        try { using var original = Icon.FromHandle(handle); return (Icon)original.Clone(); }
        finally { DestroyIcon(handle); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
}
