using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace VideoToAnimationTool.Core
{
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
}
