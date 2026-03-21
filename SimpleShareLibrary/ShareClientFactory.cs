using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleShareLibrary
{
    /// <summary>
    /// Public entry point for connecting to remote file shares.
    /// </summary>
    public static class ShareClientFactory
    {
        /// <summary>
        /// Connects to a remote SMB (SMB2/SMB3) file share asynchronously.
        /// </summary>
        /// <param name="options">Connection options including host, credentials, and resilience settings.</param>
        /// <param name="ct">Optional cancellation token.</param>
        /// <returns>A connected <see cref="IShareClient"/>.</returns>
        public static Task<IShareClient> ConnectSmbAsync(ConnectionOptions options, CancellationToken ct = default)
        {
            return new Providers.Smb.SmbShareClientFactory().ConnectAsync(options, ct);
        }

        /// <summary>
        /// Connects to a remote SMB (SMB2/SMB3) file share synchronously.
        /// </summary>
        /// <param name="options">Connection options including host, credentials, and resilience settings.</param>
        /// <returns>A connected <see cref="IShareClient"/>.</returns>
        public static IShareClient ConnectSmb(ConnectionOptions options)
        {
            return new Providers.Smb.SmbShareClientFactory().Connect(options);
        }

        /// <summary>
        /// Creates a new <see cref="IShareClientFactory"/> backed by the SMB protocol (SMB2/SMB3).
        /// </summary>
        /// <returns>An <see cref="IShareClientFactory"/> that creates SMB connections via SMBLibrary.</returns>
        [Obsolete("Use ConnectSmbAsync or ConnectSmb instead. This method will be removed in v1.0.")]
        public static IShareClientFactory CreateSmb()
        {
            return new Providers.Smb.SmbShareClientFactory();
        }
    }
}
