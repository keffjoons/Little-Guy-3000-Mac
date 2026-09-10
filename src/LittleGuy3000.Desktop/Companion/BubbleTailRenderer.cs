using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LittleGuy3000.Desktop.Companion;

internal static class BubbleTailRenderer
{
    public static Bitmap RenderAbove(bool thinking, double scale, double phase = 0)
    {
        var image = new Bitmap((int)Math.Ceiling(54 * scale), (int)Math.Ceiling(50 * scale), PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(image); g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform((float)scale, (float)scale);
        using var fill = new SolidBrush(Color.FromArgb(255, 34, 41, 31));
        using var outline = new Pen(Color.FromArgb(255, 186, 210, 128), 2);
        if (thinking)
        {
            foreach (var r in new[] { new RectangleF(15, 3, 22, 22), new RectangleF(19, 28, 14, 14), new RectangleF(22, 44, 6, 6) })
            { var rect = r; rect.Offset((float)(Math.Sin(phase) * .6), 0); g.FillEllipse(fill, rect); g.DrawEllipse(outline, rect); }
        }
        else
        {
            using var path = new GraphicsPath();
            path.AddBezier(8, 0, 14, 20, 21, 34, 27, 46);
            path.AddBezier(27, 46, 30, 28, 39, 12, 46, 0); path.CloseFigure();
            g.FillPath(fill, path); g.DrawPath(outline, path);
        }
        return image;
    }
    public static Bitmap Render(bool thinking, bool pointLeft, double scale, double phase = 0)
    {
        var image = new Bitmap((int)Math.Ceiling(58 * scale), (int)Math.Ceiling(66 * scale), PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(image); g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform((float)scale, (float)scale);
        if (!pointLeft) { g.TranslateTransform(58, 0); g.ScaleTransform(-1, 1); }
        using var fill = new SolidBrush(Color.FromArgb(255, 34, 41, 31));
        using var outline = new Pen(Color.FromArgb(255, 186, 210, 128), 2);
        if (thinking)
        {
            // Shrinking rounded islands visually link the thought to the companion.
            foreach (var r in new[] { new RectangleF(33, 5, 23, 23), new RectangleF(17, 29, 15, 15), new RectangleF(5, 47, 8, 8) })
            {
                float pulse = (float)(Math.Sin(phase) * .6);
                var rect = r; rect.Offset(0, pulse);
                g.FillEllipse(fill, rect); g.DrawEllipse(outline, rect);
            }
        }
        else
        {
            using var path = new GraphicsPath();
            path.AddBezier(60, 8, 38, 16, 27, 32, 5, 52);
            path.AddBezier(5, 52, 33, 46, 47, 38, 60, 33); path.CloseFigure();
            g.FillPath(fill, path); g.DrawPath(outline, path);
        }
        return image;
    }
}
