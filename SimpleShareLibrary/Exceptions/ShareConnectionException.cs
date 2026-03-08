using System;

namespace SimpleShareLibrary.Exceptions
{
    public class ShareConnectionException : ShareException
    {
        public ShareConnectionException(string message) : base(message) { }
        public ShareConnectionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
