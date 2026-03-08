using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimpleShareLibrary.Exceptions;
using SMBLibrary;
using SMBLibrary.Client;

namespace SimpleShareLibrary.Providers.Smb
{
    /// <summary>
    /// SMB implementation of <see cref="IShareClient"/> that wraps an authenticated SMB session.
    /// </summary>
    internal class SmbShareClient : IShareClient
    {
        #region Fields

        private readonly ISMBClient _client;
        private readonly ResilienceOptions _resilience;
        private bool _disposed;

        #endregion

        #region Constructors

        /// <summary>Initializes a new instance wrapping the given SMB client session.</summary>
        /// <param name="client">The connected and authenticated SMB client.</param>
        /// <param name="resilience">Retry and timeout settings. Uses defaults if <c>null</c>.</param>
        internal SmbShareClient(ISMBClient client, ResilienceOptions resilience = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _resilience = resilience ?? new ResilienceOptions();
        }

        #endregion

        #region Public Members

        /// <inheritdoc />
        public bool IsConnected => _client.IsConnected;

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> ListSharesAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                var shares = _client.ListShares(out NTStatus status);
                NTStatusMapper.ThrowOnFailure(status);

                return (IReadOnlyList<string>)shares.AsReadOnly();
            }, ct);
        }

        /// <inheritdoc />
        public Task<IShare> OpenShareAsync(string shareName, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(shareName))
                    throw new ArgumentException("Share name cannot be null or empty.", nameof(shareName));

                var fileStore = _client.TreeConnect(shareName, out NTStatus status);
                NTStatusMapper.ThrowOnFailure(status, shareName);

                return (IShare)new SmbShare(fileStore, _resilience);
            }, ct);
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_client.IsConnected)
                {
                    try { _client.Logoff(); } catch { }
                    try { _client.Disconnect(); } catch { }
                }
                _disposed = true;
            }
        }

        #endregion

        #region Private Helpers

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SmbShareClient));
        }

        #endregion
    }
}
