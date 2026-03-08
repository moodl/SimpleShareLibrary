using System;

namespace SimpleShareLibrary.Exceptions
{
    /// <summary>
    /// Thrown when a directory operation targets a path that does not exist.
    /// </summary>
    public class ShareDirectoryNotFoundException : ShareException
    {
        /// <summary>The path that was not found.</summary>
        public string Path { get; }

        /// <summary>Initializes a new instance for the specified path.</summary>
        /// <param name="path">The directory path that was not found.</param>
        public ShareDirectoryNotFoundException(string path)
            : base($"Directory not found: '{path}'")
        {
            Path = path;
        }

        /// <summary>Initializes a new instance for the specified path with an inner exception.</summary>
        /// <param name="path">The directory path that was not found.</param>
        /// <param name="innerException">The exception that caused this error.</param>
        public ShareDirectoryNotFoundException(string path, Exception innerException)
            : base($"Directory not found: '{path}'", innerException)
        {
            Path = path;
        }
    }
}
