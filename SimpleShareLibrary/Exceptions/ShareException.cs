using System;

namespace SimpleShareLibrary.Exceptions
{
    /// <summary>
    /// Base exception for all share-related errors.
    /// </summary>
    public class ShareException : Exception
    {
        /// <summary>Initializes a new instance with a message.</summary>
        /// <param name="message">The error message.</param>
        public ShareException(string message) : base(message) { }

        /// <summary>Initializes a new instance with a message and inner exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The exception that caused this error.</param>
        public ShareException(string message, Exception innerException) : base(message, innerException) { }
    }
}
