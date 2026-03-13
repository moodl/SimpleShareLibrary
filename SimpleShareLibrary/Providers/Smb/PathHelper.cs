namespace SimpleShareLibrary.Providers.Smb
{
    /// <summary>
    /// Utility methods for normalizing, combining, and extracting segments from SMB file paths.
    /// Accepts both forward slashes and backslashes and converts to the SMB backslash convention.
    /// </summary>
    internal static class PathHelper
    {
        /// <summary>
        /// Normalizes a path to use backslashes (SMB convention) and strips leading/trailing slashes.
        /// Accepts both <c>/</c> and <c>\</c> as input separators.
        /// </summary>
        /// <param name="path">The raw path to normalize. May be <c>null</c> or empty.</param>
        /// <returns>The normalized path, or <see cref="string.Empty"/> if <paramref name="path"/> is <c>null</c> or empty.</returns>
        internal static string Normalize(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // Replace forward slashes with backslashes
            var normalized = path!.Replace('/', '\\');

            // Trim leading and trailing backslashes
            normalized = normalized.Trim('\\');

            return normalized;
        }

        /// <summary>
        /// Combines two path segments using backslash separator.
        /// Both segments are normalized before joining.
        /// </summary>
        /// <param name="basePath">The base directory path.</param>
        /// <param name="relativePath">The relative path to append.</param>
        /// <returns>The combined, normalized path.</returns>
        internal static string Combine(string basePath, string relativePath)
        {
            var left = Normalize(basePath);
            var right = Normalize(relativePath);

            if (string.IsNullOrEmpty(left))
                return right;
            if (string.IsNullOrEmpty(right))
                return left;

            return left + "\\" + right;
        }

        /// <summary>
        /// Gets the parent directory of a path, or <see cref="string.Empty"/> if at root.
        /// </summary>
        /// <param name="path">The path to extract the parent from.</param>
        /// <returns>The parent directory path, or <see cref="string.Empty"/> if no parent exists.</returns>
        internal static string GetParent(string path)
        {
            var normalized = Normalize(path);
            var lastSep = normalized.LastIndexOf('\\');
            return lastSep < 0 ? string.Empty : normalized.Substring(0, lastSep);
        }

        /// <summary>
        /// Gets the file or directory name (last segment) of a path.
        /// </summary>
        /// <param name="path">The path to extract the name from.</param>
        /// <returns>The last segment of the path after the final backslash.</returns>
        internal static string GetName(string path)
        {
            var normalized = Normalize(path);
            var lastSep = normalized.LastIndexOf('\\');
            return lastSep < 0 ? normalized : normalized.Substring(lastSep + 1);
        }
    }
}
