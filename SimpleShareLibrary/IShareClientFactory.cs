using System.Threading;
using System.Threading.Tasks;

namespace SimpleShareLibrary
{
    public interface IShareClientFactory
    {
        Task<IShareClient> ConnectAsync(ConnectionOptions options, CancellationToken ct = default);
    }
}
