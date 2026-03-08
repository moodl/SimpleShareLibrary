using System;

namespace SimpleShareLibrary.Exceptions
{
    /// <summary>
    /// Thrown when access to a file or directory is denied.
    /// </summary>
    public class ShareAccessDeniedException : ShareException
    {
        /// <summary>The path that was denied access.</summary>
        public string Path { get; }

        /// <summary>Initializes a new instance for the specified path.</summary>
        /// <param name="path">The path that was denied access.</param>
        public ShareAccessDeniedException(string path)
            : base($"Access denied: '{path}'")
        {
            Path = path;
        }

        /// <summary>Initializes a new instance for the specified path with an inner exception.</summary>
        /// <param name="path">The path that was denied access.</param>
        /// <param name="innerException">The exception that caused this error.</param>
        public ShareAccessDeniedException(string path, Exception innerException)
            : base($"Access denied: '{path}'", innerException)
        {
            Path = path;
        }
    }
}
