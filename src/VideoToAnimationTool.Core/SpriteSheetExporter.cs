using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace VideoToAnimationTool.Core
{
    public static class SpriteSheetExporter
    {
        public static SpriteSheetResult Export(string[] framePaths, string outputPath, int columns)
        {
            if (framePaths == null || framePaths.Length == 0) throw new ArgumentException("At least one frame is required.", "framePaths");
            if (String.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output path is required.", "outputPath");
            columns = Math.Max(1, Math.Min(columns, framePaths.Length));

            var bitmaps = new List<Bitmap>();
            try
            {
                foreach (var path in framePaths)
                {
                    if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("Frame file was not found.", path);
                    bitmaps.Add(new Bitmap(path));
                }

                var frameWidth = bitmaps.Max(bitmap => bitmap.Width);
                var frameHeight = bitmaps.Max(bitmap => bitmap.Height);
                var rows = (int)Math.Ceiling(bitmaps.Count / (double)columns);
                using (var sheet = new Bitmap(frameWidth * columns, frameHeight * rows, PixelFormat.Format32bppArgb))
                using (var graphics = Graphics.FromImage(sheet))
                {
                    graphics.Clear(Color.FromArgb(0, 0, 0, 0));
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                    graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    for (var i = 0; i < bitmaps.Count; i++)
                    {
                        var x = (i % columns) * frameWidth;
                        var y = (i / columns) * frameHeight;
                        graphics.DrawImage(bitmaps[i], x, y, bitmaps[i].Width, bitmaps[i].Height);
                    }

                    var folder = Path.GetDirectoryName(outputPath);
                    if (!String.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    sheet.Save(outputPath, ImageFormat.Png);
                }

                return new SpriteSheetResult(bitmaps.Count, columns, rows, frameWidth, frameHeight, outputPath);
            }
            finally
            {
                foreach (var bitmap in bitmaps) bitmap.Dispose();
            }
        }
    }
}
