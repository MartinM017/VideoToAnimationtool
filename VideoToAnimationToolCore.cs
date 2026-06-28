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
                var fill = SampleBorderColor(source, path, bounds);

                for (var y = bounds.Top; y < bounds.Bottom; y++)
                {
                    for (var x = bounds.Left; x < bounds.Right; x++)
                    {
                        if (path.IsVisible(x + 0.5f, y + 0.5f)) output.SetPixel(x, y, fill);
                    }
                }
            }

            return output;
        }

        private static Color SampleBorderColor(Bitmap source, GraphicsPath path, Rectangle bounds)
        {
            long a = 0, r = 0, g = 0, b = 0, count = 0;
            var sampleBounds = Rectangle.Inflate(bounds, 10, 10);
            sampleBounds.Intersect(new Rectangle(0, 0, source.Width, source.Height));

            for (var y = sampleBounds.Top; y < sampleBounds.Bottom; y++)
            {
                for (var x = sampleBounds.Left; x < sampleBounds.Right; x++)
                {
                    if (path.IsVisible(x + 0.5f, y + 0.5f)) continue;
                    var near = x >= bounds.Left - 6 && x <= bounds.Right + 6 && y >= bounds.Top - 6 && y <= bounds.Bottom + 6;
                    if (!near) continue;
                    var pixel = source.GetPixel(x, y);
                    a += pixel.A;
                    r += pixel.R;
                    g += pixel.G;
                    b += pixel.B;
                    count++;
                }
            }

            if (count == 0) return Color.FromArgb(0, 0, 0, 0);
            return Color.FromArgb((int)(a / count), (int)(r / count), (int)(g / count), (int)(b / count));
        }
    }
}
