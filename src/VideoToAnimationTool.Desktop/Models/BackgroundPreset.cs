using System;
using System.Globalization;

namespace VideoToAnimationTool.Desktop
{
    public sealed class BackgroundPreset
    {
        public BackgroundPreset(string name, int tolerance, int softness, int colorDespill, int edgeCleanup)
        {
            Name = name;
            Tolerance = Clamp(tolerance);
            Softness = Clamp(softness);
            ColorDespill = Clamp(colorDespill);
            EdgeCleanup = Clamp(edgeCleanup);
        }

        public string Name { get; private set; }
        public int Tolerance { get; set; }
        public int Softness { get; set; }
        public int ColorDespill { get; set; }
        public int EdgeCleanup { get; set; }

        public string Serialize()
        {
            return Escape(Name) + "\t" + Clamp(Tolerance).ToString(CultureInfo.InvariantCulture) + "\t" + Clamp(Softness).ToString(CultureInfo.InvariantCulture) + "\t" + Clamp(ColorDespill).ToString(CultureInfo.InvariantCulture) + "\t" + Clamp(EdgeCleanup).ToString(CultureInfo.InvariantCulture);
        }

        public static BackgroundPreset TryParse(string line)
        {
            if (String.IsNullOrWhiteSpace(line)) return null;
            var parts = line.Split('\t');
            if (parts.Length != 5) return null;
            int tolerance, softness, colorDespill, edgeCleanup;
            if (!Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out tolerance)) return null;
            if (!Int32.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out softness)) return null;
            if (!Int32.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out colorDespill)) return null;
            if (!Int32.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out edgeCleanup)) return null;
            return new BackgroundPreset(Unescape(parts[0]), tolerance, softness, colorDespill, edgeCleanup);
        }

        private static int Clamp(int value) { return Math.Max(0, Math.Min(255, value)); }
        private static string Escape(string value) { return (value ?? String.Empty).Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", String.Empty).Replace("\n", String.Empty); }
        private static string Unescape(string value) { return (value ?? String.Empty).Replace("\\t", "\t").Replace("\\\\", "\\"); }
    }
}
