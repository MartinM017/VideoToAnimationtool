namespace VideoToAnimationTool.Core
{
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
}
