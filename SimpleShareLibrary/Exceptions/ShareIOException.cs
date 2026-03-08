using System;

namespace SimpleShareLibrary.Exceptions
{
    /// <summary>
    /// Thrown when a general I/O error occurs during a share operation.
    /// </summary>
    public class ShareIOException : ShareException
    {
        /// <summary>Initializes a new instance with a message.</summary>
        /// <param name="message">The error message.</param>
        public ShareIOException(string message) : base(message) { }

        /// <summary>Initializes a new instance with a message and inner exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The exception that caused this error.</param>
        public ShareIOException(string message, Exception innerException) : base(message, innerException) { }
    }
}
