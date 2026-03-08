using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleShareLibrary
{
    public interface IShare : IDisposable
    {
        // Metadata & listing
        Task<bool> ExistsAsync(string path, CancellationToken ct = default);
        Task<ShareFileInfo> GetInfoAsync(string path, CancellationToken ct = default);
        Task<IReadOnlyList<ShareFileInfo>> ListAsync(string path, string pattern = "*", CancellationToken ct = default);
        Task<IReadOnlyList<ShareFileInfo>> ListRecursiveAsync(string path, string pattern = "*", CancellationToken ct = default);

        // Read
        Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default);
        Task<string> ReadAllTextAsync(string path, Encoding encoding = null, CancellationToken ct = default);
        Task<Stream> OpenReadAsync(string path, CancellationToken ct = default);

        // Write
        Task WriteAllBytesAsync(string path, byte[] data, bool overwrite = true, CancellationToken ct = default);
        Task WriteAllTextAsync(string path, string text, Encoding encoding = null, bool overwrite = true, CancellationToken ct = default);
        Task<Stream> OpenWriteAsync(string path, bool overwrite = true, CancellationToken ct = default);

        // Copy
        Task CopyFileAsync(string src, string dst, CopyOptions options = null, CancellationToken ct = default);
        Task CopyDirectoryAsync(string src, string dst, CopyOptions options = null, CancellationToken ct = default);

        // Move (safe by default: copy-then-delete)
        Task MoveFileAsync(string src, string dst, MoveOptions options = null, CancellationToken ct = default);
        Task MoveDirectoryAsync(string src, string dst, MoveOptions options = null, CancellationToken ct = default);

        // Delete
        Task DeleteFileAsync(string path, CancellationToken ct = default);
        Task DeleteDirectoryAsync(string path, bool recursive = false, CancellationToken ct = default);
        Task DeleteAllAsync(string path, CancellationToken ct = default);

        // Directories
        Task CreateDirectoryAsync(string path, bool createParents = true, CancellationToken ct = default);
        Task EnsureDirectoryExistsAsync(string path, CancellationToken ct = default);

        // Rename
        Task RenameAsync(string path, string newName, CancellationToken ct = default);
    }
}
