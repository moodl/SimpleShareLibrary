using System;

namespace SimpleShareLibrary.Exceptions
{
    public class ShareAuthenticationException : ShareException
    {
        public ShareAuthenticationException(string message) : base(message) { }
        public ShareAuthenticationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
