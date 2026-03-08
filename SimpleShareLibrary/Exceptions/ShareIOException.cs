using System;

namespace SimpleShareLibrary.Exceptions
{
    public class ShareIOException : ShareException
    {
        public ShareIOException(string message) : base(message) { }
        public ShareIOException(string message, Exception innerException) : base(message, innerException) { }
    }
}
