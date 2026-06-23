using System;
using System.Collections.Generic;
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
            return String.Format(
                "{0}_{1}_{2:D4}.{3}",
                ToSafeName(characterName),
                ToSafeName(actionName),
                index,
                normalizedExtension);
        }

        public static string[] GetFrameFiles(string folderPath)
        {
            if (String.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return new string[0];
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png",
                ".jpg",
                ".jpeg"
            };

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
}
