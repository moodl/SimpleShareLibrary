using System;

namespace SimpleShareLibrary.Exceptions
{
    public class ShareAccessDeniedException : ShareException
    {
        public string Path { get; }

        public ShareAccessDeniedException(string path)
            : base($"Access denied: '{path}'")
        {
            Path = path;
        }

        public ShareAccessDeniedException(string path, Exception innerException)
            : base($"Access denied: '{path}'", innerException)
        {
            Path = path;
        }
    }
}
