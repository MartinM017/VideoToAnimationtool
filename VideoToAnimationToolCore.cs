using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VideoToAnimationTool.Core
{
    public static class PathUtils
    {
        public static string ToSafeName(string name)
        {
            var source = (name ?? String.Empty).Trim();
            var safe = Regex.Replace(source, @"[^\p{L}\p{Nd}]+", "_").Trim('_');
            return String.IsNullOrWhiteSpace(safe) ? "untitled" : safe;
        }

        public static string NewFrameFileName(string characterName, string actionName, int index, string extension)
        {
            var normalizedExtension = (extension ?? "png").TrimStart('.').ToLowerInvariant();
            return String.Format("{0}_{1}_{2:D4}.{3}", ToSafeName(characterName), ToSafeName(actionName), index, normalizedExtension);
        }

        public static string[] GetFrameFiles(string folderPath)
        {
            if (String.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return new string[0];
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
            return Directory.GetFiles(folderPath)
                .Where(path => allowed.Contains(Path.GetExtension(path)))
                .OrderBy(path => ToNaturalSortKey(Path.GetFileName(path)), StringComparer.Ordinal)
                .ToArray();
        }

        private static string ToNaturalSortKey(string value)
        {
            return Regex.Replace((value ?? String.Empty).ToLowerInvariant(), @"\d+", match => match.Value.PadLeft(12, '0'));
        }
    }

    public sealed class ValidationResult
    {
        public ValidationResult(bool isValid, string[] errors)
        {
            IsValid = isValid;
            Errors = errors ?? new string[0];
        }

        public bool IsValid { get; private set; }
        public string[] Errors { get; private set; }
    }

    public static class ExportOptionsValidator
    {
        public static ValidationResult Validate(string inputPath, string outputFolder, double startSeconds, double endSeconds, int fps, string format)
        {
            var errors = new List<string>();
            if (String.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath)) errors.Add("Input video does not exist.");
            if (String.IsNullOrWhiteSpace(outputFolder) || !Directory.Exists(outputFolder)) errors.Add("Output folder does not exist.");
            if (startSeconds < 0) errors.Add("Start time cannot be negative.");
            if (endSeconds <= startSeconds) errors.Add("End time must be greater than start time.");
            if (fps < 1 || fps > 120) errors.Add("FPS must be between 1 and 120.");
            var normalized = (format ?? String.Empty).ToLowerInvariant();
            if (normalized != "png" && normalized != "jpg" && normalized != "jpeg") errors.Add("Output format must be png, jpg, or jpeg.");
            return new ValidationResult(errors.Count == 0, errors.ToArray());
        }
    }

    public static class PreviewCoordinateMapper
    {
        public static bool TryMapCanvasToImage(
            double canvasX,
            double canvasY,
            double canvasWidth,
            double canvasHeight,
            int previewPixelWidth,
            int previewPixelHeight,
            int sourcePixelWidth,
            int sourcePixelHeight,
            out PointF imagePoint)
        {
            imagePoint = new PointF();
            if (canvasWidth <= 0 || canvasHeight <= 0 || previewPixelWidth <= 0 || previewPixelHeight <= 0 || sourcePixelWidth <= 0 || sourcePixelHeight <= 0) return false;

            var scale = Math.Min(canvasWidth / previewPixelWidth, canvasHeight / previewPixelHeight);
            var displayedWidth = previewPixelWidth * scale;
            var displayedHeight = previewPixelHeight * scale;
            var offsetX = (canvasWidth - displayedWidth) / 2.0;
            var offsetY = (canvasHeight - displayedHeight) / 2.0;
            var previewX = (canvasX - offsetX) / scale;
            var previewY = (canvasY - offsetY) / scale;
            if (previewX < 0 || previewY < 0 || previewX >= previewPixelWidth || previewY >= previewPixelHeight) return false;

            var sourceX = previewX * sourcePixelWidth / previewPixelWidth;
            var sourceY = previewY * sourcePixelHeight / previewPixelHeight;
            imagePoint = new PointF((float)sourceX, (float)sourceY);
            return true;
        }
    }

    public static class FfmpegHelper
    {
        public static string FindExecutable(string appRoot)
        {
            var candidates = new[]
            {
                Path.Combine(appRoot ?? String.Empty, "tools", "ffmpeg", "ffmpeg.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe")
            };
            foreach (var candidate in candidates) if (File.Exists(candidate)) return candidate;
            var envPath = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
            foreach (var folder in envPath.Split(Path.PathSeparator))
            {
                if (String.IsNullOrWhiteSpace(folder)) continue;
                var candidate = Path.Combine(folder.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        public static string[] BuildFrameExportArguments(string inputPath, string outputFolder, string characterName, string actionName, double startSeconds, double endSeconds, int fps, string format)
        {
            var outputPattern = Path.Combine(outputFolder, String.Format("{0}_{1}_%04d.{2}", PathUtils.ToSafeName(characterName), PathUtils.ToSafeName(actionName), (format ?? "png").ToLowerInvariant()));
            return new[] { "-hide_banner", "-y", "-ss", startSeconds.ToString("0.###", CultureInfo.InvariantCulture), "-to", endSeconds.ToString("0.###", CultureInfo.InvariantCulture), "-i", inputPath, "-vf", "fps=" + fps.ToString(CultureInfo.InvariantCulture), outputPattern };
        }

        public static string ToArgumentString(IEnumerable<string> arguments)
        {
            return String.Join(" ", arguments.Select(QuoteArgument).ToArray());
        }

        private static string QuoteArgument(string value)
        {
            if (String.IsNullOrEmpty(value)) return "\"\"";
            if (value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return value;
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }

    public sealed class ChromaKeyResult
    {
        public ChromaKeyResult(int processedCount, string outputFolder)
        {
            ProcessedCount = processedCount;
            OutputFolder = outputFolder;
        }

        public int ProcessedCount { get; private set; }
        public string OutputFolder { get; private set; }
    }

    public sealed class SpriteSheetResult
    {
        public SpriteSheetResult(int frameCount, int columns, int rows, int frameWidth, int frameHeight, string outputPath)
        {
            FrameCount = frameCount;
            Columns = columns;
            Rows = rows;
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            OutputPath = outputPath;
        }

        public int FrameCount { get; private set; }
        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public int FrameWidth { get; private set; }
        public int FrameHeight { get; private set; }
        public string OutputPath { get; private set; }
    }

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
