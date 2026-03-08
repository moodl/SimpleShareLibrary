using System;

namespace SimpleShareLibrary.Exceptions
{
    public class ShareException : Exception
    {
        public ShareException(string message) : base(message) { }
        public ShareException(string message, Exception innerException) : base(message, innerException) { }
    }
}
