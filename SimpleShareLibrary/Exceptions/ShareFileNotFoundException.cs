using System;

namespace SimpleShareLibrary.Exceptions
{
    /// <summary>
    /// Thrown when a file operation targets a path that does not exist.
    /// </summary>
    public class ShareFileNotFoundException : ShareException
    {
        /// <summary>The path that was not found.</summary>
        public string Path { get; }

        /// <summary>Initializes a new instance for the specified path.</summary>
        /// <param name="path">The file path that was not found.</param>
        public ShareFileNotFoundException(string path)
            : base($"File not found: '{path}'")
        {
            Path = path;
        }

        /// <summary>Initializes a new instance for the specified path with an inner exception.</summary>
        /// <param name="path">The file path that was not found.</param>
        /// <param name="innerException">The exception that caused this error.</param>
        public ShareFileNotFoundException(string path, Exception innerException)
            : base($"File not found: '{path}'", innerException)
        {
            Path = path;
        }
    }
}
