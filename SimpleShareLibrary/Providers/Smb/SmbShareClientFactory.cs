using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SimpleShareLibrary.Exceptions;
using SMBLibrary;
using SMBLibrary.Client;

namespace SimpleShareLibrary.Providers.Smb
{
    internal class SmbShareClientFactory : IShareClientFactory
    {
        private readonly Func<ISMBClient> _clientFactory;

        internal SmbShareClientFactory()
            : this(() => new SMB2Client())
        {
        }

        /// <summary>
        /// Constructor for testing — inject a factory that creates mock ISMBClient instances.
        /// </summary>
        internal SmbShareClientFactory(Func<ISMBClient> clientFactory)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        }

        public Task<IShareClient> ConnectAsync(ConnectionOptions options, CancellationToken ct = default)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.Host))
                throw new ArgumentException("Host is required.", nameof(options));

            var resilience = options.Resilience ?? new ResilienceOptions();

            return RetryHelper.ExecuteAsync(async () =>
            {
                ct.ThrowIfCancellationRequested();

                return await Task.Run(() =>
                {
                    var client = _clientFactory();
                    bool connected;

                    if (IPAddress.TryParse(options.Host, out IPAddress ip))
                    {
                        connected = client.Connect(ip, SMBTransportType.DirectTCPTransport);
                    }
                    else
                    {
                        connected = client.Connect(options.Host, SMBTransportType.DirectTCPTransport);
                    }

                    if (!connected)
                    {
                        throw new ShareConnectionException(
                            $"Failed to connect to '{options.Host}'.");
                    }

                    var loginStatus = client.Login(
                        options.Domain ?? string.Empty,
                        options.Username ?? string.Empty,
                        options.Password ?? string.Empty);

                    if (loginStatus != NTStatus.STATUS_SUCCESS)
                    {
                        try { client.Disconnect(); } catch { }
                        NTStatusMapper.ThrowOnFailure(loginStatus);
                    }

                    return (IShareClient)new SmbShareClient(client, resilience);
                }, ct);
            }, resilience);
        }
    }
}
