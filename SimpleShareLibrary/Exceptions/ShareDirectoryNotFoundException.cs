using System;

namespace SimpleShareLibrary.Exceptions
{
    public class ShareDirectoryNotFoundException : ShareException
    {
        public string Path { get; }

        public ShareDirectoryNotFoundException(string path)
            : base($"Directory not found: '{path}'")
        {
            Path = path;
        }

        public ShareDirectoryNotFoundException(string path, Exception innerException)
            : base($"Directory not found: '{path}'", innerException)
        {
            Path = path;
        }
    }
}
