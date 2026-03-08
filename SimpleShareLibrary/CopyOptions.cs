namespace SimpleShareLibrary
{
    /// <summary>
    /// Options that control file and directory copy behavior.
    /// </summary>
    public class CopyOptions
    {
        /// <summary>Whether to overwrite an existing file at the destination. Defaults to <c>false</c>.</summary>
        public bool Overwrite { get; set; } = false;

        /// <summary>Whether to copy subdirectories recursively. Defaults to <c>true</c>.</summary>
        public bool Recursive { get; set; } = true;
    }
}
