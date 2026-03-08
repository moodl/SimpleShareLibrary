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
    /// All synchronous SMBLibrary calls are wrapped in async helper methods via <see cref="Task.Run(Action)"/>.
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

        #region SMB IO Wrappers

        /// <summary>Wraps <see cref="ISMBFileStore.CreateFile"/> in a <see cref="Task.Run(System.Func{TResult})"/> call.</summary>
        private Task<(NTStatus Status, object Handle, FileStatus FileStatus)> CreateFileAsync(
            string path,
            AccessMask accessMask,
            SMBLibrary.FileAttributes fileAttributes,
            ShareAccess shareAccess,
            CreateDisposition disposition,
            CreateOptions createOptions,
            SecurityContext securityContext)
        {
            return Task.Run(() =>
            {
                var status = _fileStore.CreateFile(
                    out object handle,
                    out FileStatus fileStatus,
                    path,
                    accessMask,
                    fileAttributes,
                    shareAccess,
                    disposition,
                    createOptions,
                    securityContext);

                return (status, handle, fileStatus);
            });
        }

        /// <summary>Wraps <see cref="ISMBFileStore.CloseFile"/> in a <see cref="Task.Run(Action)"/> call.</summary>
        private Task CloseFileAsync(object handle)
        {
            return Task.Run(() => _fileStore.CloseFile(handle));
        }

        /// <summary>Wraps <see cref="ISMBFileStore.GetFileInformation"/> in a <see cref="Task.Run(System.Func{TResult})"/> call.</summary>
        private Task<(NTStatus Status, FileInformation Info)> GetFileInformationAsync(
            object handle,
            FileInformationClass informationClass)
        {
            return Task.Run(() =>
            {
                var status = _fileStore.GetFileInformation(
                    out FileInformation fileInfo,
                    handle,
                    informationClass);

                return (status, fileInfo);
            });
        }

        /// <summary>Wraps <see cref="ISMBFileStore.QueryDirectory"/> in a <see cref="Task.Run(System.Func{TResult})"/> call.</summary>
        private Task<(NTStatus Status, List<QueryDirectoryFileInformation> Entries)> QueryDirectoryAsync(
            object handle,
            string searchPattern,
            FileInformationClass informationClass)
        {
            return Task.Run(() =>
            {
                var status = _fileStore.QueryDirectory(
                    out List<QueryDirectoryFileInformation> entries,
                    handle,
                    searchPattern,
                    informationClass);

                return (status, entries);
            });
        }

        /// <summary>Wraps <see cref="ISMBFileStore.SetFileInformation"/> in a <see cref="Task.Run(System.Func{TResult})"/> call.</summary>
        private Task<NTStatus> SetFileInformationAsync(object handle, FileInformation information)
        {
            return Task.Run(() => _fileStore.SetFileInformation(handle, information));
        }

        #endregion

        #region Metadata & Listing

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);

            return await RetryHelper.ExecuteAsync(async () =>
            {
                var (status, handle, _) = await CreateFileAsync(
                    normalized,
                    (AccessMask)0x00000080, // FILE_READ_ATTRIBUTES
                    0,
                    ShareAccess.Read | ShareAccess.Write,
                    CreateDisposition.FILE_OPEN,
                    0,
                    null).ConfigureAwait(false);

                if (status == NTStatus.STATUS_SUCCESS)
                {
                    await CloseFileAsync(handle).ConfigureAwait(false);
                    return true;
                }

                if (status == NTStatus.STATUS_OBJECT_NAME_NOT_FOUND ||
                    status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
                {
                    return false;
                }

                NTStatusMapper.ThrowOnFailure(status, normalized);
                return false;
            }, _resilience).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ShareFileInfo> GetInfoAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);

            return await RetryHelper.ExecuteAsync(async () =>
            {
                var (status, handle, _) = await CreateFileAsync(
                    normalized,
                    (AccessMask)0x00000080, // FILE_READ_ATTRIBUTES
                    0,
                    ShareAccess.Read | ShareAccess.Write,
                    CreateDisposition.FILE_OPEN,
                    0,
                    null).ConfigureAwait(false);

                NTStatusMapper.ThrowOnFailure(status, normalized);

                try
                {
                    var (infoStatus, fileInfo) = await GetFileInformationAsync(
                        handle,
                        FileInformationClass.FileAllInformation).ConfigureAwait(false);

                    NTStatusMapper.ThrowOnFailure(infoStatus, normalized);

                    return ToShareFileInfo(normalized, (FileAllInformation)fileInfo);
                }
                finally
                {
                    await CloseFileAsync(handle).ConfigureAwait(false);
                }
            }, _resilience).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ShareFileInfo>> ListAsync(string path, string pattern = "*", CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);

            return await RetryHelper.ExecuteAsync(
                () => ListInternalAsync(normalized, pattern),
                _resilience).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ShareFileInfo>> ListRecursiveAsync(string path, string pattern = "*", CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);

            return await RetryHelper.ExecuteAsync(async () =>
            {
                var results = new List<ShareFileInfo>();
                await ListRecursiveInternalAsync(normalized, pattern, results, ct).ConfigureAwait(false);
                return (IReadOnlyList<ShareFileInfo>)results;
            }, _resilience).ConfigureAwait(false);
        }

        #endregion

        #region Read

        /// <inheritdoc />
        public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);

            return await RetryHelper.ExecuteAsync(async () =>
            {
                var (status, handle, _) = await CreateFileAsync(
                    normalized,
                    AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                    null).ConfigureAwait(false);

                NTStatusMapper.ThrowOnFailure(status, normalized);

                using (var stream = new SmbFileStream(_fileStore, handle, true, false))
                {
                    return await Task.Run(() =>
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
                    }, ct).ConfigureAwait(false);
                }
            }, _resilience).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<string> ReadAllTextAsync(string path, Encoding encoding = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            var bytes = await ReadAllBytesAsync(path, ct).ConfigureAwait(false);
            return (encoding ?? Encoding.UTF8).GetString(bytes);
        }

        /// <inheritdoc />
        public async Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);

            return await RetryHelper.ExecuteAsync(async () =>
            {
                var (status, handle, _) = await CreateFileAsync(
                    normalized,
                    AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                    null).ConfigureAwait(false);

                NTStatusMapper.ThrowOnFailure(status, normalized);

                return (Stream)new SmbFileStream(_fileStore, handle, true, false);
            }, _resilience).ConfigureAwait(false);
        }

        #endregion

        #region Write

        /// <inheritdoc />
        public async Task WriteAllBytesAsync(string path, byte[] data, bool overwrite = true, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);
            var disposition = overwrite
                ? CreateDisposition.FILE_OVERWRITE_IF
                : CreateDisposition.FILE_CREATE;

            await RetryHelper.ExecuteAsync(async () =>
            {
                var (status, handle, _) = await CreateFileAsync(
                    normalized,
                    AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.None,
                    disposition,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                    null).ConfigureAwait(false);

                NTStatusMapper.ThrowOnFailure(status, normalized);

                using (var stream = new SmbFileStream(_fileStore, handle, false, true))
                {
                    await Task.Run(() => stream.Write(data, 0, data.Length), ct).ConfigureAwait(false);
                }
            }, _resilience).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task WriteAllTextAsync(string path, string text, Encoding encoding = null, bool overwrite = true, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            var bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
            return WriteAllBytesAsync(path, bytes, overwrite, ct);
        }

        /// <inheritdoc />
        public async Task<Stream> OpenWriteAsync(string path, bool overwrite = true, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);
            var disposition = overwrite
                ? CreateDisposition.FILE_OVERWRITE_IF
                : CreateDisposition.FILE_CREATE;

            return await RetryHelper.ExecuteAsync(async () =>
            {
                var (status, handle, _) = await CreateFileAsync(
                    normalized,
                    AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.None,
                    disposition,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                    null).ConfigureAwait(false);

                NTStatusMapper.ThrowOnFailure(status, normalized);

                return (Stream)new SmbFileStream(_fileStore, handle, false, true);
            }, _resilience).ConfigureAwait(false);
        }

        #endregion

        #region Copy

        /// <inheritdoc />
        public async Task CopyFileAsync(string src, string dst, CopyOptions options = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            options = options ?? new CopyOptions();

            if (!options.Overwrite)
            {
                var exists = await ExistsAsync(dst, ct).ConfigureAwait(false);
                if (exists)
                {
                    throw new ShareAlreadyExistsException(dst);
                }
            }

            using (var readStream = await OpenReadAsync(src, ct).ConfigureAwait(false))
            using (var writeStream = await OpenWriteAsync(dst, options.Overwrite, ct).ConfigureAwait(false))
            {
                await Task.Run(() =>
                {
                    var buffer = new byte[(int)_fileStore.MaxReadSize];
                    int bytesRead;
                    while ((bytesRead = readStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        writeStream.Write(buffer, 0, bytesRead);
                    }
                }, ct).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task CopyDirectoryAsync(string src, string dst, CopyOptions options = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
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
        }

        #endregion

        #region Move

        /// <inheritdoc />
        public async Task MoveFileAsync(string src, string dst, MoveOptions options = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
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
                await RenameInternalAsync(src, dst, options.Overwrite).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task MoveDirectoryAsync(string src, string dst, MoveOptions options = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
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
                await RenameInternalAsync(src, dst, options.Overwrite).ConfigureAwait(false);
            }
        }

        #endregion

        #region Delete

        /// <inheritdoc />
        public async Task DeleteFileAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);

            await RetryHelper.ExecuteAsync(async () =>
            {
                var (status, handle, _) = await CreateFileAsync(
                    normalized,
                    AccessMask.GENERIC_WRITE | AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.None,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_DELETE_ON_CLOSE,
                    null).ConfigureAwait(false);

                NTStatusMapper.ThrowOnFailure(status, normalized);
                await CloseFileAsync(handle).ConfigureAwait(false);
            }, _resilience).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteDirectoryAsync(string path, bool recursive = false, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);

            if (recursive)
            {
                var items = await ListAsync(path, "*", ct).ConfigureAwait(false);
                foreach (var item in items)
                {
                    ct.ThrowIfCancellationRequested();
                    if (item.IsDirectory)
                    {
                        await DeleteDirectoryAsync(item.FullPath, true, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await DeleteFileAsync(item.FullPath, ct).ConfigureAwait(false);
                    }
                }
            }

            await RetryHelper.ExecuteAsync(async () =>
            {
                var (status, handle, _) = await CreateFileAsync(
                    normalized,
                    AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                    SMBLibrary.FileAttributes.Directory,
                    ShareAccess.Read | ShareAccess.Write,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_DIRECTORY_FILE | CreateOptions.FILE_DELETE_ON_CLOSE,
                    null).ConfigureAwait(false);

                NTStatusMapper.ThrowOnFailure(status, normalized);
                await CloseFileAsync(handle).ConfigureAwait(false);
            }, _resilience).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteAllAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var items = await ListAsync(path, "*", ct).ConfigureAwait(false);
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                if (item.IsDirectory)
                {
                    await DeleteDirectoryAsync(item.FullPath, true, ct).ConfigureAwait(false);
                }
                else
                {
                    await DeleteFileAsync(item.FullPath, ct).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Directories

        /// <inheritdoc />
        public async Task CreateDirectoryAsync(string path, bool createParents = true, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);

            await RetryHelper.ExecuteAsync(async () =>
            {
                if (createParents)
                {
                    await CreateDirectoryRecursiveAsync(normalized).ConfigureAwait(false);
                }
                else
                {
                    await CreateSingleDirectoryAsync(normalized).ConfigureAwait(false);
                }
            }, _resilience).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task EnsureDirectoryExistsAsync(string path, CancellationToken ct = default)
        {
            return CreateDirectoryAsync(path, true, ct);
        }

        #endregion

        #region Rename

        /// <inheritdoc />
        public async Task RenameAsync(string path, string newName, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);
            var parent = PathHelper.GetParent(normalized);
            var newPath = PathHelper.Combine(parent, newName);

            await RetryHelper.ExecuteAsync(
                () => RenameInternalAsync(normalized, newPath, false),
                _resilience).ConfigureAwait(false);
        }

        #endregion

        #region Private Helpers

        private async Task<IReadOnlyList<ShareFileInfo>> ListInternalAsync(string normalizedPath, string pattern)
        {
            var (status, handle, _) = await CreateFileAsync(
                normalizedPath,
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null).ConfigureAwait(false);

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);

            try
            {
                var (queryStatus, entries) = await QueryDirectoryAsync(
                    handle,
                    "*",
                    FileInformationClass.FileDirectoryInformation).ConfigureAwait(false);

                if (queryStatus == NTStatus.STATUS_NO_MORE_FILES)
                    return new List<ShareFileInfo>();

                NTStatusMapper.ThrowOnFailure(queryStatus, normalizedPath);

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
                await CloseFileAsync(handle).ConfigureAwait(false);
            }
        }

        private async Task ListRecursiveInternalAsync(string normalizedPath, string pattern, List<ShareFileInfo> results, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var allItems = await ListInternalAsync(normalizedPath, "*").ConfigureAwait(false);

            foreach (var item in allItems)
            {
                ct.ThrowIfCancellationRequested();

                if (MatchesPattern(item.Name, pattern))
                    results.Add(item);

                if (item.IsDirectory)
                {
                    await ListRecursiveInternalAsync(item.FullPath, pattern, results, ct).ConfigureAwait(false);
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

        private async Task RenameInternalAsync(string srcPath, string dstPath, bool overwrite)
        {
            var normalizedSrc = PathHelper.Normalize(srcPath);
            var normalizedDst = PathHelper.Normalize(dstPath);

            var (status, handle, _) = await CreateFileAsync(
                normalizedSrc,
                AccessMask.GENERIC_WRITE | AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null).ConfigureAwait(false);

            NTStatusMapper.ThrowOnFailure(status, normalizedSrc);

            try
            {
                var renameInfo = new FileRenameInformationType2
                {
                    FileName = "\\" + normalizedDst,
                    ReplaceIfExists = overwrite
                };

                var setStatus = await SetFileInformationAsync(handle, renameInfo).ConfigureAwait(false);
                NTStatusMapper.ThrowOnFailure(setStatus, normalizedSrc);
            }
            finally
            {
                await CloseFileAsync(handle).ConfigureAwait(false);
            }
        }

        private async Task CreateDirectoryRecursiveAsync(string normalizedPath)
        {
            if (string.IsNullOrEmpty(normalizedPath))
                return;

            var (status, handle, _) = await CreateFileAsync(
                normalizedPath,
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN_IF,
                CreateOptions.FILE_DIRECTORY_FILE,
                null).ConfigureAwait(false);

            if (status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
            {
                var parent = PathHelper.GetParent(normalizedPath);
                if (!string.IsNullOrEmpty(parent))
                {
                    await CreateDirectoryRecursiveAsync(parent).ConfigureAwait(false);

                    var (retryStatus, retryHandle, _) = await CreateFileAsync(
                        normalizedPath,
                        AccessMask.GENERIC_READ,
                        SMBLibrary.FileAttributes.Directory,
                        ShareAccess.Read | ShareAccess.Write,
                        CreateDisposition.FILE_OPEN_IF,
                        CreateOptions.FILE_DIRECTORY_FILE,
                        null).ConfigureAwait(false);

                    NTStatusMapper.ThrowOnFailure(retryStatus, normalizedPath);
                    await CloseFileAsync(retryHandle).ConfigureAwait(false);
                    return;
                }
            }

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);
            await CloseFileAsync(handle).ConfigureAwait(false);
        }

        private async Task CreateSingleDirectoryAsync(string normalizedPath)
        {
            var (status, handle, _) = await CreateFileAsync(
                normalizedPath,
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_CREATE,
                CreateOptions.FILE_DIRECTORY_FILE,
                null).ConfigureAwait(false);

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);
            await CloseFileAsync(handle).ConfigureAwait(false);
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
