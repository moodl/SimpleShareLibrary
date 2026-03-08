using System.Threading;
using System.Threading.Tasks;

namespace SimpleShareLibrary
{
    /// <summary>
    /// Factory for creating authenticated share client connections.
    /// </summary>
    public interface IShareClientFactory
    {
        /// <summary>Connects to a remote file share server using the specified options.</summary>
        /// <param name="options">Connection and authentication settings.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>An authenticated <see cref="IShareClient"/> session.</returns>
        Task<IShareClient> ConnectAsync(ConnectionOptions options, CancellationToken ct = default);
    }
}
