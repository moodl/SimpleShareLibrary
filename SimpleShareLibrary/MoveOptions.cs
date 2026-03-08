namespace SimpleShareLibrary
{
    public class MoveOptions
    {
        /// <summary>Safe = copy-then-delete (default). Unsafe = rename/move directly.</summary>
        public bool Safe { get; set; } = true;
        public bool Overwrite { get; set; } = false;
        public bool Recursive { get; set; } = true;
    }
}
