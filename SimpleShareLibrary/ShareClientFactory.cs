namespace SimpleShareLibrary
{
    /// <summary>
    /// Public entry point for creating IShareClientFactory instances.
    /// </summary>
    public static class ShareClientFactory
    {
        /// <summary>
        /// Creates a new <see cref="IShareClientFactory"/> backed by the SMB protocol (SMB2/SMB3).
        /// </summary>
        /// <returns>An <see cref="IShareClientFactory"/> that creates SMB connections via SMBLibrary.</returns>
        public static IShareClientFactory CreateSmb()
        {
            return new Providers.Smb.SmbShareClientFactory();
        }
    }
}
