using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace VideoToAnimationTool.Core
{
    public static class WatermarkRemover
    {
        public static Bitmap RemoveWatermark(Bitmap source, PointF[] polygon)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (polygon == null || polygon.Length < 3) throw new ArgumentException("A lasso needs at least three points.", "polygon");

            var output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(output)) graphics.DrawImage(source, 0, 0, source.Width, source.Height);

            using (var path = new GraphicsPath())
            {
                path.AddPolygon(polygon);
                var bounds = Rectangle.Round(path.GetBounds());
                bounds.Intersect(new Rectangle(0, 0, source.Width, source.Height));

                for (var y = bounds.Top; y < bounds.Bottom; y++)
                {
                    for (var x = bounds.Left; x < bounds.Right; x++)
                    {
                        if (path.IsVisible(x + 0.5f, y + 0.5f)) output.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
                    }
                }
            }

            return output;
        }
    }
}
