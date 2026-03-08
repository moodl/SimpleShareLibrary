using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleShareLibrary.Exceptions;
using SMBLibrary;
using SMBLibrary.Client;

namespace SimpleShareLibrary.Providers.Smb
{
    /// <summary>
    /// SMB implementation of <see cref="IShare"/> using SMBLibrary.
    /// All synchronous SMBLibrary calls are wrapped in <see cref="Task.Run(Action)"/> at the lowest level.
    /// </summary>
    internal class SmbShare : IShare
    {
        #region Fields

        private readonly ISMBFileStore _fileStore;
        private readonly ResilienceOptions _resilience;
        private bool _disposed;

        #endregion

        #region Constructors

        /// <summary>Initializes a new instance wrapping the given SMB file store.</summary>
        /// <param name="fileStore">The SMB file store to operate on.</param>
        /// <param name="resilience">Retry and timeout settings. Uses defaults if <c>null</c>.</param>
        internal SmbShare(ISMBFileStore fileStore, ResilienceOptions resilience = null)
        {
            _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
            _resilience = resilience ?? new ResilienceOptions();
        }

        #endregion

        #region Metadata & Listing

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return RetryHelper.ExecuteAsync(() => Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var normalized = PathHelper.Normalize(path);

                var status = _fileStore.CreateFile(
                    out object handle,
                    out FileStatus _,
                    normalized,
                    (AccessMask)0x00000080, // FILE_READ_ATTRIBUTES
                    0,
                    ShareAccess.Read | ShareAccess.Write,
                    CreateDisposition.FILE_OPEN,
                    0,
                    null);

                if (status == NTStatus.STATUS_SUCCESS)
                {
                    _fileStore.CloseFile(handle);
                    return true;
                }

                if (status == NTStatus.STATUS_OBJECT_NAME_NOT_FOUND ||
                    status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
                {
                    return false;
                }

                NTStatusMapper.ThrowOnFailure(status, normalized);
                return false;
            }, ct), _resilience);
        }

        /// <inheritdoc />
        public Task<ShareFileInfo> GetInfoAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return RetryHelper.ExecuteAsync(() => Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var normalized = PathHelper.Normalize(path);

                var status = _fileStore.CreateFile(
                    out object handle,
                    out FileStatus _,
                    normalized,
                    (AccessMask)0x00000080, // FILE_READ_ATTRIBUTES
                    0,
                    ShareAccess.Read | ShareAccess.Write,
                    CreateDisposition.FILE_OPEN,
                    0,
                    null);

                NTStatusMapper.ThrowOnFailure(status, normalized);

                try
                {
                    status = _fileStore.GetFileInformation(
                        out FileInformation fileInfo,
                        handle,
                        FileInformationClass.FileAllInformation);

                    NTStatusMapper.ThrowOnFailure(status, normalized);

                    return ToShareFileInfo(normalized, (FileAllInformation)fileInfo);
                }
                finally
                {
                    _fileStore.CloseFile(handle);
                }
            }, ct), _resilience);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<ShareFileInfo>> ListAsync(string path, string pattern = "*", CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return RetryHelper.ExecuteAsync(() => Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var normalized = PathHelper.Normalize(path);
                return ListInternal(normalized, pattern);
            }, ct), _resilience);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<ShareFileInfo>> ListRecursiveAsync(string path, string pattern = "*", CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return RetryHelper.ExecuteAsync(() => Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var normalized = PathHelper.Normalize(path);
                var results = new List<ShareFileInfo>();
                ListRecursiveInternal(normalized, pattern, results, ct);
                return (IReadOnlyList<ShareFileInfo>)results;
            }, ct), _resilience);
        }

        #endregion

        #region Read

        /// <inheritdoc />
        public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return RetryHelper.ExecuteAsync(() => Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var normalized = PathHelper.Normalize(path);

                var status = _fileStore.CreateFile(
                    out object handle,
                    out FileStatus _,
                    normalized,
                    AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                    null);

                NTStatusMapper.ThrowOnFailure(status, normalized);

                using (var stream = new SmbFileStream(_fileStore, handle, true, false))
                {
                    using (var ms = new MemoryStream())
                    {
                        var buffer = new byte[(int)_fileStore.MaxReadSize];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            ms.Write(buffer, 0, bytesRead);
                        }
                        return ms.ToArray();
                    }
                }
            }, ct), _resilience);
        }

        /// <inheritdoc />
        public Task<string> ReadAllTextAsync(string path, Encoding encoding = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return RetryHelper.ExecuteAsync(() => Task.Run(async () =>
            {
                var bytes = await ReadAllBytesAsync(path, ct).ConfigureAwait(false);
                return (encoding ?? Encoding.UTF8).GetString(bytes);
            }, ct), _resilience);
        }

        /// <inheritdoc />
        public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return RetryHelper.ExecuteAsync(() => Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var normalized = PathHelper.Normalize(path);

                var status = _fileStore.CreateFile(
                    out object handle,
                    out FileStatus _,
                    normalized,
                    AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                    null);

                NTStatusMapper.ThrowOnFailure(status, normalized);

                return (Stream)new SmbFileStream(_fileStore, handle, true, false);
            }, ct), _resilience);
        }

        #endregion

        #region Write

        /// <inheritdoc />
        public Task WriteAllBytesAsync(string path, byte[] data, bool overwrite = true, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return RetryHelper.ExecuteAsync(() => Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var normalized = PathHelper.Normalize(path);
                var disposition = overwrite
                    ? CreateDisposition.FILE_OVERWRITE_IF
                    : CreateDisposition.FILE_CREATE;

                var status = _fileStore.CreateFile(
                    out object handle,
                    out FileStatus _,
                    normalized,
                    AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.None,
                    disposition,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                    null);

                NTStatusMapper.ThrowOnFailure(status, normalized);

                using (var stream = new SmbFileStream(_fileStore, handle, false, true))
                {
                    stream.Write(data, 0, data.Length);
                }
            }, ct), _resilience);
        }

        /// <inheritdoc />
        public Task WriteAllTextAsync(string path, string text, Encoding encoding = null, bool overwrite = true, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return Task.Run(() =>
            {
                var bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
                return WriteAllBytesAsync(path, bytes, overwrite, ct);
            }, ct);
        }

        /// <inheritdoc />
        public Task<Stream> OpenWriteAsync(string path, bool overwrite = true, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return RetryHelper.ExecuteAsync(() => Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var normalized = PathHelper.Normalize(path);
                var disposition = overwrite
                    ? CreateDisposition.FILE_OVERWRITE_IF
                    : CreateDisposition.FILE_CREATE;

                var status = _fileStore.CreateFile(
                    out object handle,
                    out FileStatus _,
                    normalized,
                    AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.None,
                    disposition,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                    null);

                NTStatusMapper.ThrowOnFailure(status, normalized);

                return (Stream)new SmbFileStream(_fileStore, handle, false, true);
            }, ct), _resilience);
        }

        #endregion

        #region Copy

        /// <inheritdoc />
        public Task CopyFileAsync(string src, string dst, CopyOptions options = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return Task.Run(async () =>
            {
                ct.ThrowIfCancellationRequested();
                options = options ?? new CopyOptions();

                if (!options.Overwrite)
                {
                    var exists = await ExistsAsync(dst, ct).ConfigureAwait(false);
                    if (exists)
                        throw new ShareAlreadyExistsException(dst);
                }

                using (var readStream = await OpenReadAsync(src, ct).ConfigureAwait(false))
                using (var writeStream = await OpenWriteAsync(dst, options.Overwrite, ct).ConfigureAwait(false))
                {
                    var buffer = new byte[(int)_fileStore.MaxReadSize];
                    int bytesRead;
                    while ((bytesRead = readStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        writeStream.Write(buffer, 0, bytesRead);
                    }
                }
            }, ct);
        }

        /// <inheritdoc />
        public Task CopyDirectoryAsync(string src, string dst, CopyOptions options = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return Task.Run(async () =>
            {
                ct.ThrowIfCancellationRequested();
                options = options ?? new CopyOptions();

                await EnsureDirectoryExistsAsync(dst, ct).ConfigureAwait(false);

                var items = await ListAsync(src, "*", ct).ConfigureAwait(false);
                foreach (var item in items)
                {
                    ct.ThrowIfCancellationRequested();
                    var dstPath = PathHelper.Combine(dst, item.Name);

                    if (item.IsDirectory)
                    {
                        if (options.Recursive)
                        {
                            await CopyDirectoryAsync(item.FullPath, dstPath, options, ct).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await CopyFileAsync(item.FullPath, dstPath, options, ct).ConfigureAwait(false);
                    }
                }
            }, ct);
        }

        #endregion

        #region Move

        /// <inheritdoc />
        public Task MoveFileAsync(string src, string dst, MoveOptions options = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return Task.Run(async () =>
            {
                ct.ThrowIfCancellationRequested();
                options = options ?? new MoveOptions();

                if (options.Safe)
                {
                    var copyOpts = new CopyOptions { Overwrite = options.Overwrite };
                    await CopyFileAsync(src, dst, copyOpts, ct).ConfigureAwait(false);
                    await DeleteFileAsync(src, ct).ConfigureAwait(false);
                }
                else
                {
                    RenameInternal(src, dst, options.Overwrite);
                }
            }, ct);
        }

        /// <inheritdoc />
        public Task MoveDirectoryAsync(string src, string dst, MoveOptions options = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return Task.Run(async () =>
            {
                ct.ThrowIfCancellationRequested();
                options = options ?? new MoveOptions();

                if (options.Safe)
                {
                    var copyOpts = new CopyOptions { Overwrite = options.Overwrite, Recursive = options.Recursive };
                    await CopyDirectoryAsync(src, dst, copyOpts, ct).ConfigureAwait(false);
                    await DeleteDirectoryAsync(src, true, ct).ConfigureAwait(false);
                }
                else
                {
                    RenameInternal(src, dst, options.Overwrite);
                }
            }, ct);
        }

        #endregion

        #region Delete

        /// <inheritdoc />
        public Task DeleteFileAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return RetryHelper.ExecuteAsync(() => Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var normalized = PathHelper.Normalize(path);

                var status = _fileStore.CreateFile(
                    out object handle,
                    out FileStatus _,
                    normalized,
                    AccessMask.GENERIC_WRITE | AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.None,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_DELETE_ON_CLOSE,
                    null);

                NTStatusMapper.ThrowOnFailure(status, normalized);
                _fileStore.CloseFile(handle);
            }, ct), _resilience);
        }

        /// <inheritdoc />
        public Task DeleteDirectoryAsync(string path, bool recursive = false, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return Task.Run(async () =>
            {
                ct.ThrowIfCancellationRequested();
                var normalized = PathHelper.Normalize(path);

                if (recursive)
                {
                    var items = await ListAsync(path, "*", ct).ConfigureAwait(false);
                    foreach (var item in items)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (item.IsDirectory)
                            await DeleteDirectoryAsync(item.FullPath, true, ct).ConfigureAwait(false);
                        else
                            await DeleteFileAsync(item.FullPath, ct).ConfigureAwait(false);
                    }
                }

                await RetryHelper.ExecuteAsync(() => Task.Run(() =>
                {
                    var status = _fileStore.CreateFile(
                        out object handle,
                        out FileStatus _,
                        normalized,
                        AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                        SMBLibrary.FileAttributes.Directory,
                        ShareAccess.Read | ShareAccess.Write,
                        CreateDisposition.FILE_OPEN,
                        CreateOptions.FILE_DIRECTORY_FILE | CreateOptions.FILE_DELETE_ON_CLOSE,
                        null);

                    NTStatusMapper.ThrowOnFailure(status, normalized);
                    _fileStore.CloseFile(handle);
                }, ct), _resilience).ConfigureAwait(false);
            }, ct);
        }

        /// <inheritdoc />
        public Task DeleteAllAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return Task.Run(async () =>
            {
                ct.ThrowIfCancellationRequested();
                var items = await ListAsync(path, "*", ct).ConfigureAwait(false);
                foreach (var item in items)
                {
                    ct.ThrowIfCancellationRequested();
                    if (item.IsDirectory)
                        await DeleteDirectoryAsync(item.FullPath, true, ct).ConfigureAwait(false);
                    else
                        await DeleteFileAsync(item.FullPath, ct).ConfigureAwait(false);
                }
            }, ct);
        }

        #endregion

        #region Directories

        /// <inheritdoc />
        public Task CreateDirectoryAsync(string path, bool createParents = true, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return RetryHelper.ExecuteAsync(() => Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var normalized = PathHelper.Normalize(path);

                if (createParents)
                {
                    CreateDirectoryRecursive(normalized);
                }
                else
                {
                    CreateSingleDirectory(normalized);
                }
            }, ct), _resilience);
        }

        /// <inheritdoc />
        public Task EnsureDirectoryExistsAsync(string path, CancellationToken ct = default)
        {
            return CreateDirectoryAsync(path, true, ct);
        }

        #endregion

        #region Rename

        /// <inheritdoc />
        public Task RenameAsync(string path, string newName, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return RetryHelper.ExecuteAsync(() => Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var normalized = PathHelper.Normalize(path);
                var parent = PathHelper.GetParent(normalized);
                var newPath = PathHelper.Combine(parent, newName);
                RenameInternal(normalized, newPath, false);
            }, ct), _resilience);
        }

        #endregion

        #region Private Helpers

        private IReadOnlyList<ShareFileInfo> ListInternal(string normalizedPath, string pattern)
        {
            var status = _fileStore.CreateFile(
                out object handle,
                out FileStatus _,
                normalizedPath,
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);

            try
            {
                status = _fileStore.QueryDirectory(
                    out List<QueryDirectoryFileInformation> entries,
                    handle,
                    "*",
                    FileInformationClass.FileDirectoryInformation);

                if (status == NTStatus.STATUS_NO_MORE_FILES)
                    return new List<ShareFileInfo>();

                NTStatusMapper.ThrowOnFailure(status, normalizedPath);

                var results = new List<ShareFileInfo>();
                foreach (var entry in entries)
                {
                    if (entry is FileDirectoryInformation dirInfo)
                    {
                        var name = dirInfo.FileName;
                        if (name == "." || name == "..")
                            continue;

                        if (!MatchesPattern(name, pattern))
                            continue;

                        results.Add(new ShareFileInfo
                        {
                            Name = name,
                            FullPath = PathHelper.Combine(normalizedPath, name),
                            IsDirectory = (dirInfo.FileAttributes & SMBLibrary.FileAttributes.Directory) != 0,
                            Size = dirInfo.EndOfFile,
                            CreatedUtc = dirInfo.CreationTime.ToUniversalTime(),
                            LastWriteUtc = dirInfo.LastWriteTime.ToUniversalTime(),
                            LastAccessUtc = dirInfo.LastAccessTime.ToUniversalTime(),
                            IsReadOnly = (dirInfo.FileAttributes & SMBLibrary.FileAttributes.ReadOnly) != 0,
                            IsHidden = (dirInfo.FileAttributes & SMBLibrary.FileAttributes.Hidden) != 0,
                        });
                    }
                }

                return results;
            }
            finally
            {
                _fileStore.CloseFile(handle);
            }
        }

        private void ListRecursiveInternal(string normalizedPath, string pattern, List<ShareFileInfo> results, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var allItems = ListInternal(normalizedPath, "*");

            foreach (var item in allItems)
            {
                ct.ThrowIfCancellationRequested();

                if (item.IsDirectory)
                {
                    if (MatchesPattern(item.Name, pattern))
                        results.Add(item);

                    ListRecursiveInternal(item.FullPath, pattern, results, ct);
                }
                else
                {
                    if (MatchesPattern(item.Name, pattern))
                        results.Add(item);
                }
            }
        }

        private static bool MatchesPattern(string name, string pattern)
        {
            if (pattern == "*" || pattern == "*.*")
                return true;

            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                var ext = pattern.Substring(1);
                return name.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
        }

        private void RenameInternal(string srcPath, string dstPath, bool overwrite)
        {
            var normalizedSrc = PathHelper.Normalize(srcPath);
            var normalizedDst = PathHelper.Normalize(dstPath);

            var status = _fileStore.CreateFile(
                out object handle,
                out FileStatus _,
                normalizedSrc,
                AccessMask.GENERIC_WRITE | AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null);

            NTStatusMapper.ThrowOnFailure(status, normalizedSrc);

            try
            {
                var renameInfo = new FileRenameInformationType2
                {
                    FileName = "\\" + normalizedDst,
                    ReplaceIfExists = overwrite
                };

                status = _fileStore.SetFileInformation(handle, renameInfo);
                NTStatusMapper.ThrowOnFailure(status, normalizedSrc);
            }
            finally
            {
                _fileStore.CloseFile(handle);
            }
        }

        private void CreateDirectoryRecursive(string normalizedPath)
        {
            if (string.IsNullOrEmpty(normalizedPath))
                return;

            var status = _fileStore.CreateFile(
                out object handle,
                out FileStatus fileStatus,
                normalizedPath,
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN_IF,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            if (status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
            {
                var parent = PathHelper.GetParent(normalizedPath);
                if (!string.IsNullOrEmpty(parent))
                {
                    CreateDirectoryRecursive(parent);
                    status = _fileStore.CreateFile(
                        out handle,
                        out fileStatus,
                        normalizedPath,
                        AccessMask.GENERIC_READ,
                        SMBLibrary.FileAttributes.Directory,
                        ShareAccess.Read | ShareAccess.Write,
                        CreateDisposition.FILE_OPEN_IF,
                        CreateOptions.FILE_DIRECTORY_FILE,
                        null);
                }
            }

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);
            _fileStore.CloseFile(handle);
        }

        private void CreateSingleDirectory(string normalizedPath)
        {
            var status = _fileStore.CreateFile(
                out object handle,
                out FileStatus _,
                normalizedPath,
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_CREATE,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);
            _fileStore.CloseFile(handle);
        }

        private static ShareFileInfo ToShareFileInfo(string fullPath, FileAllInformation info)
        {
            var basicInfo = info.BasicInformation;
            var standardInfo = info.StandardInformation;
            var nameInfo = info.NameInformation;

            var name = PathHelper.GetName(nameInfo?.FileName ?? fullPath);

            return new ShareFileInfo
            {
                Name = name,
                FullPath = fullPath,
                IsDirectory = (basicInfo.FileAttributes & SMBLibrary.FileAttributes.Directory) != 0,
                Size = standardInfo.EndOfFile,
                CreatedUtc = basicInfo.CreationTime.Time?.ToUniversalTime() ?? DateTime.MinValue,
                LastWriteUtc = basicInfo.LastWriteTime.Time?.ToUniversalTime() ?? DateTime.MinValue,
                LastAccessUtc = basicInfo.LastAccessTime.Time?.ToUniversalTime() ?? DateTime.MinValue,
                IsReadOnly = (basicInfo.FileAttributes & SMBLibrary.FileAttributes.ReadOnly) != 0,
                IsHidden = (basicInfo.FileAttributes & SMBLibrary.FileAttributes.Hidden) != 0,
            };
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SmbShare));
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_disposed)
            {
                _fileStore.Disconnect();
                _disposed = true;
            }
        }

        #endregion
    }
}
