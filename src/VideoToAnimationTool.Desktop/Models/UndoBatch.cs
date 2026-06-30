using System.Collections.Generic;

namespace VideoToAnimationTool.Desktop
{
    public sealed class UndoBatch
    {
        public UndoBatch(string folder, string restoreFolder, string selectPath)
        {
            Folder = folder;
            RestoreFolder = restoreFolder;
            SelectPath = selectPath;
            Items = new List<UndoItem>();
        }

        public string Folder { get; private set; }
        public string RestoreFolder { get; private set; }
        public string SelectPath { get; private set; }
        public List<UndoItem> Items { get; private set; }
    }
}
