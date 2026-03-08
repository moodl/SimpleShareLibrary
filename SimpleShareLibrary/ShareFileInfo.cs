using System;

namespace SimpleShareLibrary
{
    /// <summary>
    /// Metadata about a file or directory on a remote share.
    /// </summary>
    public class ShareFileInfo
    {
        /// <summary>The file or directory name (without path).</summary>
        public string Name { get; set; }

        /// <summary>The full path relative to the share root.</summary>
        public string FullPath { get; set; }

        /// <summary>Whether this entry represents a directory.</summary>
        public bool IsDirectory { get; set; }

        /// <summary>The file size in bytes. Zero for directories.</summary>
        public long Size { get; set; }

        /// <summary>The UTC creation time.</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>The UTC time of the last write.</summary>
        public DateTime LastWriteUtc { get; set; }

        /// <summary>The UTC time of the last access.</summary>
        public DateTime LastAccessUtc { get; set; }

        /// <summary>Whether the file is marked read-only.</summary>
        public bool IsReadOnly { get; set; }

        /// <summary>Whether the file is marked hidden.</summary>
        public bool IsHidden { get; set; }
    }
}
