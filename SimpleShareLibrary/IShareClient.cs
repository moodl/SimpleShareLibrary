using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleShareLibrary
{
    public interface IShareClient : IDisposable
    {
        bool IsConnected { get; }
        Task<IReadOnlyList<string>> ListSharesAsync(CancellationToken ct = default);
        Task<IShare> OpenShareAsync(string shareName, CancellationToken ct = default);
    }
}
