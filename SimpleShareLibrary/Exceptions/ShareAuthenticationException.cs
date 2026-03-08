using System;

namespace SimpleShareLibrary.Exceptions
{
    /// <summary>
    /// Thrown when authentication with the remote share server fails.
    /// </summary>
    public class ShareAuthenticationException : ShareException
    {
        /// <summary>Initializes a new instance with a message.</summary>
        /// <param name="message">The error message.</param>
        public ShareAuthenticationException(string message) : base(message) { }

        /// <summary>Initializes a new instance with a message and inner exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The exception that caused this error.</param>
        public ShareAuthenticationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
