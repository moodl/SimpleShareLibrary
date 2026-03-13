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
        #region Constants

        /// <summary>The default SMB port.</summary>
        private const int DefaultSmbPort = 445;

        #endregion

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

            return RetryHelper.ExecuteAsync(() => Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                return ConnectCore(options, resilience);
            }, ct), resilience);
        }

        /// <inheritdoc />
        public IShareClient Connect(ConnectionOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.Host))
                throw new ArgumentException("Host is required.", nameof(options));

            var resilience = options.Resilience ?? new ResilienceOptions();

            return RetryHelper.Execute(() => ConnectCore(options, resilience), resilience);
        }

        #endregion

        #region Private Members

        /// <summary>
        /// Core connection logic shared by both async and sync paths.
        /// </summary>
        private IShareClient ConnectCore(ConnectionOptions options, ResilienceOptions resilience)
        {
            int port = options.Port;
            bool useCustomPort = port != DefaultSmbPort;

            // When a custom port is needed, create a PortAwareSMB2Client instead of using the factory
            var client = useCustomPort
                ? (ISMBClient)new PortAwareSMB2Client()
                : _clientFactory();

            try
            {
                bool connected = ConnectToHost(client, options.Host, port, useCustomPort);

                if (!connected)
                {
                    throw new ShareConnectionException(
                        $"Failed to connect to '{options.Host}:{port}'.");
                }

                var loginStatus = client.Login(
                    options.Domain ?? string.Empty,
                    options.Username ?? string.Empty,
                    options.Password ?? string.Empty);

                if (loginStatus != NTStatus.STATUS_SUCCESS)
                {
                    NTStatusMapper.ThrowOnFailure(loginStatus);
                }

                return new SmbShareClient(client, resilience);
            }
            catch
            {
                try { client.Disconnect(); } catch { }
                throw;
            }
        }

        /// <summary>
        /// Connects the SMB client to the specified host and port.
        /// </summary>
        private static bool ConnectToHost(ISMBClient client, string host, int port, bool useCustomPort)
        {
            if (useCustomPort)
            {
                // Custom port requires PortAwareSMB2Client and an IP address
                var portClient = (PortAwareSMB2Client)client;
                IPAddress ip = ResolveHost(host);
                return portClient.ConnectOnPort(ip, SMBTransportType.DirectTCPTransport, port);
            }

            // Default port: use the standard ISMBClient.Connect overloads
            if (IPAddress.TryParse(host, out IPAddress parsedIp))
            {
                return client.Connect(parsedIp, SMBTransportType.DirectTCPTransport);
            }

            return client.Connect(host, SMBTransportType.DirectTCPTransport);
        }

        /// <summary>
        /// Resolves a hostname to an IP address. Returns the address directly if already an IP.
        /// </summary>
        private static IPAddress ResolveHost(string host)
        {
            if (IPAddress.TryParse(host, out IPAddress ip))
            {
                return ip;
            }

            var addresses = Dns.GetHostAddresses(host);
            if (addresses.Length == 0)
            {
                throw new ShareConnectionException($"Could not resolve host '{host}'.");
            }

            return addresses[0];
        }

        #endregion
    }
}
