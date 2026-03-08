using System;

namespace SimpleShareLibrary.Exceptions
{
    /// <summary>
    /// Thrown when a create or copy operation targets a path that already exists.
    /// </summary>
    public class ShareAlreadyExistsException : ShareException
    {
        /// <summary>The path that already exists.</summary>
        public string Path { get; }

        /// <summary>Initializes a new instance for the specified path.</summary>
        /// <param name="path">The path that already exists.</param>
        public ShareAlreadyExistsException(string path)
            : base($"Already exists: '{path}'")
        {
            Path = path;
        }

        /// <summary>Initializes a new instance for the specified path with an inner exception.</summary>
        /// <param name="path">The path that already exists.</param>
        /// <param name="innerException">The exception that caused this error.</param>
        public ShareAlreadyExistsException(string path, Exception innerException)
            : base($"Already exists: '{path}'", innerException)
        {
            Path = path;
        }
    }
}
