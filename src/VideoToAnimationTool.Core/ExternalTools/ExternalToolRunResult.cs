namespace VideoToAnimationTool.Core.ExternalTools
{
    public sealed class ExternalToolRunResult
    {
        public ExternalToolRunResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput ?? string.Empty;
            StandardError = standardError ?? string.Empty;
        }

        public int ExitCode { get; private set; }
        public string StandardOutput { get; private set; }
        public string StandardError { get; private set; }
    }
}
