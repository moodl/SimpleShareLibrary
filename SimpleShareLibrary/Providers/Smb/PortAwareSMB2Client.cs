using System.Net;
using SMBLibrary;
using SMBLibrary.Client;

namespace SimpleShareLibrary.Providers.Smb
{
    /// <summary>
    /// Extends <see cref="SMB2Client"/> to expose the <c>Connect</c> overload
    /// that accepts a custom port number. The base method is <c>protected internal</c>,
    /// so this subclass makes it accessible within the library.
    /// </summary>
    internal class PortAwareSMB2Client : SMB2Client
    {
        /// <summary>
        /// Connects to the specified server address on a custom port.
        /// </summary>
        /// <param name="serverAddress">The IP address of the server.</param>
        /// <param name="transport">The SMB transport type.</param>
        /// <param name="port">The port number to connect on.</param>
        /// <returns><c>true</c> if the connection was established; otherwise <c>false</c>.</returns>
        internal bool ConnectOnPort(IPAddress serverAddress, SMBTransportType transport, int port)
        {
            return Connect(serverAddress, transport, port);
        }
    }
}
