namespace SimpleShareLibrary.Providers.Smb
{
    internal static class PathHelper
    {
        /// <summary>
        /// Normalizes a path to use backslashes (SMB convention) and strips leading/trailing slashes.
        /// Accepts both '/' and '\' as input separators.
        /// </summary>
        internal static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // Replace forward slashes with backslashes
            var normalized = path.Replace('/', '\\');

            // Trim leading and trailing backslashes
            normalized = normalized.Trim('\\');

            return normalized;
        }

        /// <summary>
        /// Combines two path segments using backslash separator.
        /// </summary>
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
        /// Gets the parent directory of a path, or empty string if at root.
        /// </summary>
        internal static string GetParent(string path)
        {
            var normalized = Normalize(path);
            var lastSep = normalized.LastIndexOf('\\');
            return lastSep < 0 ? string.Empty : normalized.Substring(0, lastSep);
        }

        /// <summary>
        /// Gets the file/directory name (last segment) of a path.
        /// </summary>
        internal static string GetName(string path)
        {
            var normalized = Normalize(path);
            var lastSep = normalized.LastIndexOf('\\');
            return lastSep < 0 ? normalized : normalized.Substring(lastSep + 1);
        }
    }
}
