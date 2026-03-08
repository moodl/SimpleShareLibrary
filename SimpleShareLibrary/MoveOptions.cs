namespace SimpleShareLibrary
{
    /// <summary>
    /// Options that control file and directory move behavior.
    /// </summary>
    public class MoveOptions
    {
        /// <summary>Safe = copy-then-delete (default). Unsafe = rename/move directly.</summary>
        public bool Safe { get; set; } = true;

        /// <summary>Whether to overwrite an existing file at the destination. Defaults to <c>false</c>.</summary>
        public bool Overwrite { get; set; } = false;

        /// <summary>Whether to move subdirectories recursively. Defaults to <c>true</c>.</summary>
        public bool Recursive { get; set; } = true;
    }
}
