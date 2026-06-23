using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

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

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var path = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
            foreach (var folder in path.Split(Path.PathSeparator))
            {
                if (String.IsNullOrWhiteSpace(folder))
                {
                    continue;
                }

                var candidate = Path.Combine(folder.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        public static string[] BuildFrameExportArguments(
            string inputPath,
            string outputFolder,
            string characterName,
            string actionName,
            double startSeconds,
            double endSeconds,
            int fps,
            string format)
        {
            var character = PathUtils.ToSafeName(characterName);
            var action = PathUtils.ToSafeName(actionName);
            var normalizedFormat = (format ?? "png").ToLowerInvariant();
            var outputPattern = Path.Combine(outputFolder, String.Format("{0}_{1}_%04d.{2}", character, action, normalizedFormat));

            return new[]
            {
                "-hide_banner",
                "-y",
                "-ss",
                startSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                "-to",
                endSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                "-i",
                inputPath,
                "-vf",
                "fps=" + fps.ToString(CultureInfo.InvariantCulture),
                outputPattern
            };
        }

        public static string ToArgumentString(IEnumerable<string> arguments)
        {
            var escaped = new List<string>();
            foreach (var argument in arguments)
            {
                escaped.Add(QuoteArgument(argument));
            }

            return String.Join(" ", escaped.ToArray());
        }

        private static string QuoteArgument(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            if (value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
