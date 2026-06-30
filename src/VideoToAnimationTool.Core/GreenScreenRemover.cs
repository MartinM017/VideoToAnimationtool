using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace VideoToAnimationTool.Core
{
    public static class GreenScreenRemover
    {
        public static Bitmap RemoveGreenScreen(Bitmap source, int tolerance, int softness, int despill)
        {
            return RemoveGreenScreen(source, tolerance, softness, despill, 45);
        }

        public static Bitmap RemoveGreenScreen(Bitmap source, int tolerance, int softness, int despill, int edgeCleanup)
        {
            if (source == null) throw new ArgumentNullException("source");
            tolerance = Math.Max(0, Math.Min(255, tolerance));
            softness = Math.Max(0, Math.Min(255, softness));
            despill = Math.Max(0, Math.Min(255, despill));
            edgeCleanup = Math.Max(0, Math.Min(255, edgeCleanup));
            var keyColor = DetectBackgroundColor(source);
            var output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            var alphas = new int[source.Width, source.Height];
            var colors = new Color[source.Width, source.Height];

            for (var y = 0; y < source.Height; y++)
            {
                for (var x = 0; x < source.Width; x++)
                {
                    var pixel = source.GetPixel(x, y);
                    var alpha = ComputeAlpha(pixel, keyColor, tolerance, softness);
                    var cleaned = RemoveColorSpill(pixel, keyColor, alpha, despill);
                    alphas[x, y] = alpha;
                    colors[x, y] = cleaned;
                }
            }

            if (edgeCleanup > 0) CleanColorEdges(alphas, colors, keyColor, edgeCleanup);

            for (var y = 0; y < source.Height; y++)
            {
                for (var x = 0; x < source.Width; x++)
                {
                    var color = colors[x, y];
                    output.SetPixel(x, y, Color.FromArgb(alphas[x, y], color.R, color.G, color.B));
                }
            }
            return output;
        }

        public static Bitmap RemoveSmartMatteBackground(Bitmap source, int tolerance, int softness, int despill, int edgeCleanup)
        {
            using (var keyed = RemoveGreenScreen(source, tolerance, softness, despill, edgeCleanup))
            {
                return RefineWithConnectedComponents(keyed);
            }
        }

        public static Bitmap RemoveGreenScreen(Bitmap source, int tolerance, int softness)
        {
            return RemoveGreenScreen(source, tolerance, softness, 70);
        }

        public static ChromaKeyResult ProcessFolder(string inputFolder, string outputFolder, int tolerance, int softness, int despill, int edgeCleanup, Action<int, int, string> progress)
        {
            var frames = PathUtils.GetFrameFiles(inputFolder);
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
            for (var i = 0; i < frames.Length; i++)
            {
                var outputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(frames[i]) + ".png");
                using (var source = new Bitmap(frames[i]))
                using (var cutout = RemoveGreenScreen(source, tolerance, softness, despill, edgeCleanup))
                {
                    cutout.Save(outputPath, ImageFormat.Png);
                }
                if (progress != null) progress(i + 1, frames.Length, outputPath);
            }
            return new ChromaKeyResult(frames.Length, outputFolder);
        }

        public static ChromaKeyResult ProcessFolder(string inputFolder, string outputFolder, int tolerance, int softness, int despill, Action<int, int, string> progress)
        {
            return ProcessFolder(inputFolder, outputFolder, tolerance, softness, despill, 45, progress);
        }

        public static ChromaKeyResult ProcessFolder(string inputFolder, string outputFolder, int tolerance, int softness, Action<int, int, string> progress)
        {
            return ProcessFolder(inputFolder, outputFolder, tolerance, softness, 70, progress);
        }

        private static Color DetectBackgroundColor(Bitmap source)
        {
            var sample = Math.Max(2, Math.Min(18, Math.Min(source.Width, source.Height) / 18));
            long a = 0, r = 0, g = 0, b = 0, count = 0;
            AddCornerSample(source, 0, 0, sample, ref a, ref r, ref g, ref b, ref count);
            AddCornerSample(source, Math.Max(0, source.Width - sample), 0, sample, ref a, ref r, ref g, ref b, ref count);
            AddCornerSample(source, 0, Math.Max(0, source.Height - sample), sample, ref a, ref r, ref g, ref b, ref count);
            AddCornerSample(source, Math.Max(0, source.Width - sample), Math.Max(0, source.Height - sample), sample, ref a, ref r, ref g, ref b, ref count);
            if (count == 0) return Color.FromArgb(255, 0, 255, 0);
            return Color.FromArgb((int)(a / count), (int)(r / count), (int)(g / count), (int)(b / count));
        }

        private static void AddCornerSample(Bitmap source, int startX, int startY, int size, ref long a, ref long r, ref long g, ref long b, ref long count)
        {
            for (var y = startY; y < Math.Min(source.Height, startY + size); y++)
            {
                for (var x = startX; x < Math.Min(source.Width, startX + size); x++)
                {
                    var pixel = source.GetPixel(x, y);
                    a += pixel.A;
                    r += pixel.R;
                    g += pixel.G;
                    b += pixel.B;
                    count++;
                }
            }
        }

        private static int ComputeAlpha(Color pixel, Color keyColor, int tolerance, int softness)
        {
            var distance = ColorDistance(pixel, keyColor);
            var threshold = Math.Max(8, tolerance * 1.65);
            var feather = Math.Max(1, softness * 1.35);
            if (distance <= threshold) return 0;
            if (softness <= 0 || distance >= threshold + feather) return pixel.A;
            var ratio = (distance - threshold) / feather;
            var alpha = (int)Math.Round(pixel.A * Math.Max(0, Math.Min(1, ratio)));
            return Math.Max(0, Math.Min(255, alpha));
        }

        private static double ColorDistance(Color left, Color right)
        {
            var dr = left.R - right.R;
            var dg = left.G - right.G;
            var db = left.B - right.B;
            return Math.Sqrt((dr * dr) + (dg * dg) + (db * db));
        }

        private static Color RemoveColorSpill(Color pixel, Color keyColor, int alpha, int despill)
        {
            if (despill <= 0) return pixel;
            var similarity = 1.0 - Math.Min(1.0, ColorDistance(pixel, keyColor) / 260.0);
            if (similarity <= 0) return pixel;
            var edgeWeight = alpha < 255 ? (255 - alpha) / 255.0 : 0.0;
            if (edgeWeight <= 0.02 && similarity < 0.58) return pixel;
            var strength = (despill / 255.0) * similarity * Math.Max(0.08, edgeWeight);
            var r = ReduceSpillChannel(pixel.R, keyColor.R, strength);
            var g = ReduceSpillChannel(pixel.G, keyColor.G, strength);
            var b = ReduceSpillChannel(pixel.B, keyColor.B, strength);
            return Color.FromArgb(pixel.A, r, g, b);
        }

        private static int ReduceSpillChannel(int value, int keyValue, double strength)
        {
            if (keyValue < 96) return value;
            var reduction = (int)Math.Round((keyValue / 255.0) * value * strength * 0.28);
            return Math.Max(0, Math.Min(255, value - reduction));
        }

        private static void CleanColorEdges(int[,] alphas, Color[,] colors, Color keyColor, int edgeCleanup)
        {
            var width = alphas.GetLength(0);
            var height = alphas.GetLength(1);
            var original = (int[,])alphas.Clone();
            var radius = 1 + Math.Min(2, edgeCleanup / 96);
            var cleanupStrength = edgeCleanup / 255.0;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (original[x, y] <= 0) continue;
                    var color = colors[x, y];
                    var colorSimilarity = 1.0 - Math.Min(1.0, ColorDistance(color, keyColor) / 220.0);
                    if (colorSimilarity <= 0) continue;
                    var transparentNeighbors = CountTransparentNeighbors(original, x, y, radius);
                    if (transparentNeighbors == 0) continue;

                    var edgeFactor = Math.Min(1.0, transparentNeighbors / 4.0);
                    var alphaScale = 1.0 - (cleanupStrength * edgeFactor * colorSimilarity);
                    alphas[x, y] = Math.Max(0, Math.Min(255, (int)Math.Round(original[x, y] * alphaScale)));
                }
            }
        }

        private static int CountTransparentNeighbors(int[,] alphas, int x, int y, int radius)
        {
            var width = alphas.GetLength(0);
            var height = alphas.GetLength(1);
            var count = 0;
            for (var yy = Math.Max(0, y - radius); yy <= Math.Min(height - 1, y + radius); yy++)
            {
                for (var xx = Math.Max(0, x - radius); xx <= Math.Min(width - 1, x + radius); xx++)
                {
                    if (xx == x && yy == y) continue;
                    if (alphas[xx, yy] == 0) count++;
                }
            }
            return count;
        }

        private static Bitmap RefineWithConnectedComponents(Bitmap source)
        {
            var width = source.Width;
            var height = source.Height;
            var output = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var visited = new bool[width, height];
            var minComponentSize = Math.Max(24, (width * height) / 6000);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (visited[x, y]) continue;
                    var pixel = source.GetPixel(x, y);
                    if (pixel.A <= 18)
                    {
                        visited[x, y] = true;
                        output.SetPixel(x, y, Color.FromArgb(0, pixel.R, pixel.G, pixel.B));
                        continue;
                    }

                    var component = FloodComponent(source, visited, x, y);
                    var touchesBorder = false;
                    foreach (var point in component)
                    {
                        if (point.X <= 0 || point.Y <= 0 || point.X >= width - 1 || point.Y >= height - 1) { touchesBorder = true; break; }
                    }

                    var keep = component.Count >= minComponentSize || !touchesBorder;
                    foreach (var point in component)
                    {
                        var color = source.GetPixel(point.X, point.Y);
                        if (keep) output.SetPixel(point.X, point.Y, color);
                        else output.SetPixel(point.X, point.Y, Color.FromArgb(0, color.R, color.G, color.B));
                    }
                }
            }

            return output;
        }

        private static List<Point> FloodComponent(Bitmap source, bool[,] visited, int startX, int startY)
        {
            var width = source.Width;
            var height = source.Height;
            var result = new List<Point>();
            var queue = new Queue<Point>();
            visited[startX, startY] = true;
            queue.Enqueue(new Point(startX, startY));

            while (queue.Count > 0)
            {
                var point = queue.Dequeue();
                result.Add(point);
                AddNeighbor(source, visited, queue, point.X - 1, point.Y, width, height);
                AddNeighbor(source, visited, queue, point.X + 1, point.Y, width, height);
                AddNeighbor(source, visited, queue, point.X, point.Y - 1, width, height);
                AddNeighbor(source, visited, queue, point.X, point.Y + 1, width, height);
            }

            return result;
        }

        private static void AddNeighbor(Bitmap source, bool[,] visited, Queue<Point> queue, int x, int y, int width, int height)
        {
            if (x < 0 || y < 0 || x >= width || y >= height || visited[x, y]) return;
            if (source.GetPixel(x, y).A <= 18) return;
            visited[x, y] = true;
            queue.Enqueue(new Point(x, y));
        }
    }
}
