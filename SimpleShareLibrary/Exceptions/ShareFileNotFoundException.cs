using System;

namespace SimpleShareLibrary.Exceptions
{
    public class ShareFileNotFoundException : ShareException
    {
        public string Path { get; }

        public ShareFileNotFoundException(string path)
            : base($"File not found: '{path}'")
        {
            Path = path;
        }

        public ShareFileNotFoundException(string path, Exception innerException)
            : base($"File not found: '{path}'", innerException)
        {
            Path = path;
        }
    }
}
