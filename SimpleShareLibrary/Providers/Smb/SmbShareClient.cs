using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimpleShareLibrary.Exceptions;
using SMBLibrary;
using SMBLibrary.Client;

namespace SimpleShareLibrary.Providers.Smb
{
    internal class SmbShareClient : IShareClient
    {
        private readonly ISMBClient _client;
        private readonly ResilienceOptions _resilience;
        private bool _disposed;

        internal SmbShareClient(ISMBClient client, ResilienceOptions resilience = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _resilience = resilience ?? new ResilienceOptions();
        }

        public bool IsConnected => _client.IsConnected;

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

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SmbShareClient));
        }
    }
}
