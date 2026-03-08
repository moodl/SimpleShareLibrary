using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleShareLibrary
{
    /// <summary>
    /// Represents an authenticated session to a remote file share server.
    /// </summary>
    public interface IShareClient : IDisposable
    {
        /// <summary>Gets whether the client is currently connected.</summary>
        bool IsConnected { get; }

        /// <summary>Lists the available share names on the server.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A read-only list of share names.</returns>
        Task<IReadOnlyList<string>> ListSharesAsync(CancellationToken ct = default);

        /// <summary>Opens a share by name for file and directory operations.</summary>
        /// <param name="shareName">The name of the share to open.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>An <see cref="IShare"/> for performing operations on the share.</returns>
        Task<IShare> OpenShareAsync(string shareName, CancellationToken ct = default);
    }
}
