using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace LibertyFramework.Hud
{
    // T-049: the one sprite the HUD needs that the game does not give us, the wanted star. Drawn once with GDI+ as plain white on
    // transparent, so the palette tints it; original geometry (a regular five-point star), no game art.
    internal static class HudArt
    {
        private const int Size = 64;
        private const float InnerRatio = 0.42f;

        internal static byte[] StarPng()
        {
            using (Bitmap bitmap = new Bitmap(Size, Size, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                PointF[] points = new PointF[10];
                float centre = Size / 2f, outer = Size / 2f - 2f;
                for (int index = 0; index < points.Length; index++)
                {
                    double angle = -Math.PI / 2 + index * Math.PI / 5;
                    float radius = index % 2 == 0 ? outer : outer * InnerRatio;
                    points[index] = new PointF(centre + (float)(Math.Cos(angle) * radius), centre + (float)(Math.Sin(angle) * radius));
                }
                using (Brush fill = new SolidBrush(Color.White)) { g.FillPolygon(fill, points); }
                using (MemoryStream stream = new MemoryStream()) { bitmap.Save(stream, ImageFormat.Png); return stream.ToArray(); }
            }
        }
    }
}
