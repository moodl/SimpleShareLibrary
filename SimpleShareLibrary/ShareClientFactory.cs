namespace SimpleShareLibrary
{
    /// <summary>
    /// Public entry point for creating IShareClientFactory instances.
    /// </summary>
    public static class ShareClientFactory
    {
        /// <summary>
        /// Creates a new SMB-backed IShareClientFactory.
        /// </summary>
        public static IShareClientFactory CreateSmb()
        {
            return new Providers.Smb.SmbShareClientFactory();
        }
    }
}
