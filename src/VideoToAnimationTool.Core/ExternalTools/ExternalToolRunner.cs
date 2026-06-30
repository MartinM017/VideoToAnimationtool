using System;
using System.Diagnostics;
using System.IO;

namespace VideoToAnimationTool.Core.ExternalTools
{
    public sealed class ExternalToolRunner
    {
        public ExternalToolRunResult Run(ExternalToolRunOptions options)
        {
            if (options == null) throw new ArgumentNullException("options");
            if (String.IsNullOrWhiteSpace(options.FileName)) throw new ArgumentException("Tool filename is required.", "options");
            if (!File.Exists(options.FileName)) throw new FileNotFoundException("External tool was not found.", options.FileName);

            var startInfo = new ProcessStartInfo
            {
                FileName = options.FileName,
                Arguments = FfmpegHelper.ToArgumentString(options.Arguments ?? new string[0]),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (!String.IsNullOrWhiteSpace(options.WorkingDirectory)) startInfo.WorkingDirectory = options.WorkingDirectory;
            if (options.EnvironmentPathEntries != null && options.EnvironmentPathEntries.Length > 0)
            {
                startInfo.EnvironmentVariables["PATH"] = String.Join(Path.PathSeparator.ToString(), options.EnvironmentPathEntries) + Path.PathSeparator + (startInfo.EnvironmentVariables["PATH"] ?? String.Empty);
            }

            if (options.EnvironmentVariables != null)
            {
                foreach (var pair in options.EnvironmentVariables) startInfo.EnvironmentVariables[pair.Key] = pair.Value;
            }

            using (var process = Process.Start(startInfo))
            {
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ExternalToolRunResult(process.ExitCode, stdout, stderr);
            }
        }
    }
}
