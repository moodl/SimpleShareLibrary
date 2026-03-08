using System;

namespace SimpleShareLibrary
{
    public class ShareFileInfo
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime LastWriteUtc { get; set; }
        public DateTime LastAccessUtc { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsHidden { get; set; }
    }
}
