using System;

namespace SimpleShareLibrary.Exceptions
{
    public class ShareAlreadyExistsException : ShareException
    {
        public string Path { get; }

        public ShareAlreadyExistsException(string path)
            : base($"Already exists: '{path}'")
        {
            Path = path;
        }

        public ShareAlreadyExistsException(string path, Exception innerException)
            : base($"Already exists: '{path}'", innerException)
        {
            Path = path;
        }
    }
}
