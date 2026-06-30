namespace VideoToAnimationTool.Core
{
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
}
