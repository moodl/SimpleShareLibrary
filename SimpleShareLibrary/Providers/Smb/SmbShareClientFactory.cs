using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SimpleShareLibrary.Exceptions;
using SMBLibrary;
using SMBLibrary.Client;

namespace SimpleShareLibrary.Providers.Smb
{
    /// <summary>
    /// SMB implementation of <see cref="IShareClientFactory"/>.
    /// Creates authenticated SMB sessions using SMBLibrary.
    /// </summary>
    internal class SmbShareClientFactory : IShareClientFactory
    {
        #region Fields

        private readonly Func<ISMBClient> _clientFactory;

        #endregion

        #region Constructors

        /// <summary>Initializes a new instance that creates <see cref="SMB2Client"/> instances.</summary>
        internal SmbShareClientFactory()
            : this(() => new SMB2Client())
        {
        }

        /// <summary>Initializes a new instance with a custom client factory for testing.</summary>
        /// <param name="clientFactory">A factory that creates <see cref="ISMBClient"/> instances.</param>
        internal SmbShareClientFactory(Func<ISMBClient> clientFactory)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        }

        #endregion

        #region Public Members

        /// <inheritdoc />
        public Task<IShareClient> ConnectAsync(ConnectionOptions options, CancellationToken ct = default)
        {
            if (options is null)
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

        #endregion
    }
}
