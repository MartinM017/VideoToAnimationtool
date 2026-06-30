namespace VideoToAnimationTool.Desktop
{
    public sealed class UndoItem
    {
        public UndoItem(string path, bool existed, byte[] previousBytes)
        {
            Path = path;
            Existed = existed;
            PreviousBytes = previousBytes;
        }

        public string Path { get; private set; }
        public bool Existed { get; private set; }
        public byte[] PreviousBytes { get; private set; }
    }
}
