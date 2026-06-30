using System;
using System.Collections.Generic;
using System.IO;

namespace VideoToAnimationTool.Core
{
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
}
