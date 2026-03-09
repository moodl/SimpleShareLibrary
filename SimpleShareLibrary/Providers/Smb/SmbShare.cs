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
    /// Provides both async and sync overloads via shared Core methods with a <c>bool useAsync</c> parameter.
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

        #region SMB IO Wrappers — Async

        /// <summary>
        /// Asynchronously opens or creates a file or directory handle on the remote share.
        /// Wraps the synchronous <see cref="ISMBFileStore.CreateFile"/> call via <see cref="Task.Run(System.Func{TResult})"/>.
        /// </summary>
        /// <param name="path">The normalized SMB path to the file or directory.</param>
        /// <param name="accessMask">The desired access rights (e.g. read, write, delete).</param>
        /// <param name="fileAttributes">The file attributes to apply (e.g. Normal, Directory).</param>
        /// <param name="shareAccess">The sharing mode for concurrent access.</param>
        /// <param name="disposition">Specifies how to handle existing/non-existing files (open, create, overwrite).</param>
        /// <param name="createOptions">Additional options controlling file behavior (e.g. directory, delete-on-close).</param>
        /// <param name="securityContext">Optional security context; typically <c>null</c>.</param>
        /// <returns>A tuple containing the <see cref="NTStatus"/>, the opened file handle, and the resulting <see cref="FileStatus"/>.</returns>
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

        /// <summary>
        /// Asynchronously closes a previously opened file handle.
        /// Wraps the synchronous <see cref="ISMBFileStore.CloseFile"/> call.
        /// </summary>
        /// <param name="handle">The file handle obtained from <see cref="CreateFileAsync"/>.</param>
        private Task CloseFileAsync(object handle)
        {
            return Task.Run(() => _fileStore.CloseFile(handle));
        }

        /// <summary>
        /// Asynchronously queries metadata for an open file handle.
        /// Wraps the synchronous <see cref="ISMBFileStore.GetFileInformation"/> call.
        /// </summary>
        /// <param name="handle">The file handle obtained from <see cref="CreateFileAsync"/>.</param>
        /// <param name="informationClass">The type of metadata to retrieve.</param>
        /// <returns>A tuple containing the <see cref="NTStatus"/> and the retrieved <see cref="FileInformation"/>.</returns>
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

        /// <summary>
        /// Asynchronously enumerates entries in a directory.
        /// Wraps the synchronous <see cref="ISMBFileStore.QueryDirectory"/> call.
        /// </summary>
        /// <param name="handle">The directory handle obtained from <see cref="CreateFileAsync"/>.</param>
        /// <param name="searchPattern">The wildcard pattern to filter entries.</param>
        /// <param name="informationClass">The type of directory information to retrieve.</param>
        /// <returns>A tuple containing the <see cref="NTStatus"/> and the list of directory entries.</returns>
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

        /// <summary>
        /// Asynchronously sets metadata or performs operations (e.g. rename) on an open file handle.
        /// Wraps the synchronous <see cref="ISMBFileStore.SetFileInformation"/> call.
        /// </summary>
        /// <param name="handle">The file handle obtained from <see cref="CreateFileAsync"/>.</param>
        /// <param name="information">The file information to set.</param>
        /// <returns>The <see cref="NTStatus"/> indicating success or failure.</returns>
        private Task<NTStatus> SetFileInformationAsync(object handle, FileInformation information)
        {
            return Task.Run(() => _fileStore.SetFileInformation(handle, information));
        }

        #endregion

        #region SMB IO Wrappers — Sync

        /// <summary>
        /// Synchronously opens or creates a file or directory handle on the remote share.
        /// Calls <see cref="ISMBFileStore.CreateFile"/> directly on the calling thread.
        /// </summary>
        /// <param name="path">The normalized SMB path to the file or directory.</param>
        /// <param name="accessMask">The desired access rights.</param>
        /// <param name="fileAttributes">The file attributes to apply.</param>
        /// <param name="shareAccess">The sharing mode for concurrent access.</param>
        /// <param name="disposition">Specifies how to handle existing/non-existing files.</param>
        /// <param name="createOptions">Additional options controlling file behavior.</param>
        /// <param name="securityContext">Optional security context; typically <c>null</c>.</param>
        /// <returns>A tuple containing the <see cref="NTStatus"/>, the opened file handle, and the resulting <see cref="FileStatus"/>.</returns>
        private (NTStatus Status, object Handle, FileStatus FileStatus) CreateFileSync(
            string path,
            AccessMask accessMask,
            SMBLibrary.FileAttributes fileAttributes,
            ShareAccess shareAccess,
            CreateDisposition disposition,
            CreateOptions createOptions,
            SecurityContext securityContext)
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
        }

        /// <summary>
        /// Synchronously closes a previously opened file handle.
        /// </summary>
        /// <param name="handle">The file handle to close.</param>
        private void CloseFileSync(object handle)
        {
            _fileStore.CloseFile(handle);
        }

        /// <summary>
        /// Synchronously queries metadata for an open file handle.
        /// </summary>
        /// <param name="handle">The file handle to query.</param>
        /// <param name="informationClass">The type of metadata to retrieve.</param>
        /// <returns>A tuple containing the <see cref="NTStatus"/> and the retrieved <see cref="FileInformation"/>.</returns>
        private (NTStatus Status, FileInformation Info) GetFileInformationSync(
            object handle,
            FileInformationClass informationClass)
        {
            var status = _fileStore.GetFileInformation(
                out FileInformation fileInfo,
                handle,
                informationClass);

            return (status, fileInfo);
        }

        /// <summary>
        /// Synchronously enumerates entries in a directory.
        /// </summary>
        /// <param name="handle">The directory handle to query.</param>
        /// <param name="searchPattern">The wildcard pattern to filter entries.</param>
        /// <param name="informationClass">The type of directory information to retrieve.</param>
        /// <returns>A tuple containing the <see cref="NTStatus"/> and the list of directory entries.</returns>
        private (NTStatus Status, List<QueryDirectoryFileInformation> Entries) QueryDirectorySync(
            object handle,
            string searchPattern,
            FileInformationClass informationClass)
        {
            var status = _fileStore.QueryDirectory(
                out List<QueryDirectoryFileInformation> entries,
                handle,
                searchPattern,
                informationClass);

            return (status, entries);
        }

        /// <summary>
        /// Synchronously sets metadata or performs operations on an open file handle.
        /// </summary>
        /// <param name="handle">The file handle to modify.</param>
        /// <param name="information">The file information to set.</param>
        /// <returns>The <see cref="NTStatus"/> indicating success or failure.</returns>
        private NTStatus SetFileInformationSync(object handle, FileInformation information)
        {
            return _fileStore.SetFileInformation(handle, information);
        }

        #endregion

        #region SMB IO Dispatchers

        /// <summary>
        /// Dispatches to <see cref="CreateFileAsync"/> or <see cref="CreateFileSync"/> based on <paramref name="useAsync"/>.
        /// </summary>
        private async Task<(NTStatus Status, object Handle, FileStatus FileStatus)> CreateFileCore(
            bool useAsync,
            string path,
            AccessMask accessMask,
            SMBLibrary.FileAttributes fileAttributes,
            ShareAccess shareAccess,
            CreateDisposition disposition,
            CreateOptions createOptions,
            SecurityContext securityContext)
        {
            if (useAsync)
            {
                return await CreateFileAsync(path, accessMask, fileAttributes, shareAccess, disposition, createOptions, securityContext).ConfigureAwait(false);
            }

            return CreateFileSync(path, accessMask, fileAttributes, shareAccess, disposition, createOptions, securityContext);
        }

        /// <summary>
        /// Dispatches to <see cref="CloseFileAsync"/> or <see cref="CloseFileSync"/> based on <paramref name="useAsync"/>.
        /// </summary>
        private async Task CloseFileCore(bool useAsync, object handle)
        {
            if (useAsync)
            {
                await CloseFileAsync(handle).ConfigureAwait(false);
            }
            else
            {
                CloseFileSync(handle);
            }
        }

        /// <summary>
        /// Dispatches to <see cref="GetFileInformationAsync"/> or <see cref="GetFileInformationSync"/> based on <paramref name="useAsync"/>.
        /// </summary>
        private async Task<(NTStatus Status, FileInformation Info)> GetFileInformationCore(
            bool useAsync,
            object handle,
            FileInformationClass informationClass)
        {
            if (useAsync)
            {
                return await GetFileInformationAsync(handle, informationClass).ConfigureAwait(false);
            }

            return GetFileInformationSync(handle, informationClass);
        }

        /// <summary>
        /// Dispatches to <see cref="QueryDirectoryAsync"/> or <see cref="QueryDirectorySync"/> based on <paramref name="useAsync"/>.
        /// </summary>
        private async Task<(NTStatus Status, List<QueryDirectoryFileInformation> Entries)> QueryDirectoryCore(
            bool useAsync,
            object handle,
            string searchPattern,
            FileInformationClass informationClass)
        {
            if (useAsync)
            {
                return await QueryDirectoryAsync(handle, searchPattern, informationClass).ConfigureAwait(false);
            }

            return QueryDirectorySync(handle, searchPattern, informationClass);
        }

        /// <summary>
        /// Dispatches to <see cref="SetFileInformationAsync"/> or <see cref="SetFileInformationSync"/> based on <paramref name="useAsync"/>.
        /// </summary>
        private async Task<NTStatus> SetFileInformationCore(bool useAsync, object handle, FileInformation information)
        {
            if (useAsync)
            {
                return await SetFileInformationAsync(handle, information).ConfigureAwait(false);
            }

            return SetFileInformationSync(handle, information);
        }

        #endregion

        #region Core Methods

        /// <summary>
        /// Core logic for checking whether a file or directory exists.
        /// </summary>
        /// <param name="normalizedPath">The normalized path to check.</param>
        /// <param name="useAsync">If <c>true</c>, uses async IO wrappers; otherwise uses sync IO.</param>
        /// <returns><c>true</c> if the path exists; otherwise <c>false</c>.</returns>
        private async Task<bool> ExistsCore(string normalizedPath, bool useAsync)
        {
            var (status, handle, _) = await CreateFileCore(
                useAsync,
                normalizedPath,
                (AccessMask)0x00000080, // FILE_READ_ATTRIBUTES
                0,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                0,
                null).ConfigureAwait(false);

            if (status == NTStatus.STATUS_SUCCESS)
            {
                await CloseFileCore(useAsync, handle).ConfigureAwait(false);
                return true;
            }

            if (status == NTStatus.STATUS_OBJECT_NAME_NOT_FOUND ||
                status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
            {
                return false;
            }

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);
            return false;
        }

        /// <summary>
        /// Core logic for retrieving file or directory metadata.
        /// </summary>
        /// <param name="normalizedPath">The normalized path to query.</param>
        /// <param name="useAsync">If <c>true</c>, uses async IO wrappers; otherwise uses sync IO.</param>
        /// <returns>A <see cref="ShareFileInfo"/> describing the entry.</returns>
        private async Task<ShareFileInfo> GetInfoCore(string normalizedPath, bool useAsync)
        {
            var (status, handle, _) = await CreateFileCore(
                useAsync,
                normalizedPath,
                (AccessMask)0x00000080, // FILE_READ_ATTRIBUTES
                0,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                0,
                null).ConfigureAwait(false);

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);

            try
            {
                var (infoStatus, fileInfo) = await GetFileInformationCore(
                    useAsync,
                    handle,
                    FileInformationClass.FileAllInformation).ConfigureAwait(false);

                NTStatusMapper.ThrowOnFailure(infoStatus, normalizedPath);

                return ToShareFileInfo(normalizedPath, (FileAllInformation)fileInfo);
            }
            finally
            {
                await CloseFileCore(useAsync, handle).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Core logic for listing the contents of a single directory.
        /// </summary>
        /// <param name="normalizedPath">The normalized directory path.</param>
        /// <param name="pattern">A wildcard pattern to filter entries.</param>
        /// <param name="useAsync">If <c>true</c>, uses async IO wrappers; otherwise uses sync IO.</param>
        /// <returns>A read-only list of matching entries.</returns>
        private async Task<IReadOnlyList<ShareFileInfo>> ListInternalCore(string normalizedPath, string pattern, bool useAsync)
        {
            var (status, handle, _) = await CreateFileCore(
                useAsync,
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
                var results = new List<ShareFileInfo>();

                while (true)
                {
                    var (queryStatus, entries) = await QueryDirectoryCore(
                        useAsync,
                        handle,
                        "*",
                        FileInformationClass.FileDirectoryInformation).ConfigureAwait(false);

                    if (queryStatus == NTStatus.STATUS_NO_MORE_FILES)
                    {
                        break;
                    }

                    NTStatusMapper.ThrowOnFailure(queryStatus, normalizedPath);

                    foreach (var entry in entries)
                    {
                        if (entry is FileDirectoryInformation dirInfo)
                        {
                            var name = dirInfo.FileName;
                            if (name == "." || name == "..")
                            {
                                continue;
                            }

                            if (!MatchesPattern(name, pattern))
                            {
                                continue;
                            }

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
                }

                return results;
            }
            finally
            {
                await CloseFileCore(useAsync, handle).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Core logic for opening a read stream.
        /// </summary>
        /// <param name="normalizedPath">The normalized file path.</param>
        /// <param name="useAsync">If <c>true</c>, uses async IO wrappers; otherwise uses sync IO.</param>
        /// <returns>A readable <see cref="Stream"/>.</returns>
        private async Task<Stream> OpenReadCore(string normalizedPath, bool useAsync)
        {
            var (status, handle, _) = await CreateFileCore(
                useAsync,
                normalizedPath,
                AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null).ConfigureAwait(false);

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);

            return new SmbFileStream(_fileStore, handle, true, false);
        }

        /// <summary>
        /// Core logic for opening a write stream.
        /// </summary>
        /// <param name="normalizedPath">The normalized file path.</param>
        /// <param name="disposition">The create disposition (overwrite or create).</param>
        /// <param name="useAsync">If <c>true</c>, uses async IO wrappers; otherwise uses sync IO.</param>
        /// <returns>A writable <see cref="Stream"/>.</returns>
        private async Task<Stream> OpenWriteCore(string normalizedPath, CreateDisposition disposition, bool useAsync)
        {
            var (status, handle, _) = await CreateFileCore(
                useAsync,
                normalizedPath,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.None,
                disposition,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null).ConfigureAwait(false);

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);

            return new SmbFileStream(_fileStore, handle, false, true);
        }

        /// <summary>
        /// Core logic for deleting a single file via the DELETE_ON_CLOSE flag.
        /// </summary>
        /// <param name="normalizedPath">The normalized file path.</param>
        /// <param name="useAsync">If <c>true</c>, uses async IO wrappers; otherwise uses sync IO.</param>
        private async Task DeleteFileCore(string normalizedPath, bool useAsync)
        {
            var (status, handle, _) = await CreateFileCore(
                useAsync,
                normalizedPath,
                AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_DELETE_ON_CLOSE,
                null).ConfigureAwait(false);

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);
            await CloseFileCore(useAsync, handle).ConfigureAwait(false);
        }

        /// <summary>
        /// Core logic for deleting a single empty directory via the DELETE_ON_CLOSE flag.
        /// </summary>
        /// <param name="normalizedPath">The normalized directory path.</param>
        /// <param name="useAsync">If <c>true</c>, uses async IO wrappers; otherwise uses sync IO.</param>
        private async Task DeleteDirectorySingleCore(string normalizedPath, bool useAsync)
        {
            var (status, handle, _) = await CreateFileCore(
                useAsync,
                normalizedPath,
                AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE | CreateOptions.FILE_DELETE_ON_CLOSE,
                null).ConfigureAwait(false);

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);
            await CloseFileCore(useAsync, handle).ConfigureAwait(false);
        }

        /// <summary>
        /// Core logic for renaming (moving) a file or directory.
        /// </summary>
        /// <param name="srcPath">The current path.</param>
        /// <param name="dstPath">The desired new path.</param>
        /// <param name="overwrite">If <c>true</c>, replaces an existing entry at the destination.</param>
        /// <param name="useAsync">If <c>true</c>, uses async IO wrappers; otherwise uses sync IO.</param>
        private async Task RenameInternalCore(string srcPath, string dstPath, bool overwrite, bool useAsync)
        {
            var normalizedSrc = PathHelper.Normalize(srcPath);
            var normalizedDst = PathHelper.Normalize(dstPath);

            var (status, handle, _) = await CreateFileCore(
                useAsync,
                normalizedSrc,
                AccessMask.DELETE | AccessMask.SYNCHRONIZE,
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

                var setStatus = await SetFileInformationCore(useAsync, handle, renameInfo).ConfigureAwait(false);
                NTStatusMapper.ThrowOnFailure(setStatus, normalizedSrc);
            }
            finally
            {
                await CloseFileCore(useAsync, handle).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Core logic for recursively creating a directory and all missing parents.
        /// </summary>
        /// <param name="normalizedPath">The normalized directory path.</param>
        /// <param name="useAsync">If <c>true</c>, uses async IO wrappers; otherwise uses sync IO.</param>
        private async Task CreateDirectoryRecursiveCore(string normalizedPath, bool useAsync)
        {
            if (string.IsNullOrEmpty(normalizedPath))
                return;

            var (status, handle, _) = await CreateFileCore(
                useAsync,
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
                    await CreateDirectoryRecursiveCore(parent, useAsync).ConfigureAwait(false);

                    var (retryStatus, retryHandle, _) = await CreateFileCore(
                        useAsync,
                        normalizedPath,
                        AccessMask.GENERIC_READ,
                        SMBLibrary.FileAttributes.Directory,
                        ShareAccess.Read | ShareAccess.Write,
                        CreateDisposition.FILE_OPEN_IF,
                        CreateOptions.FILE_DIRECTORY_FILE,
                        null).ConfigureAwait(false);

                    NTStatusMapper.ThrowOnFailure(retryStatus, normalizedPath);
                    await CloseFileCore(useAsync, retryHandle).ConfigureAwait(false);
                    return;
                }
            }

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);
            await CloseFileCore(useAsync, handle).ConfigureAwait(false);
        }

        /// <summary>
        /// Core logic for creating a single directory without creating parents.
        /// </summary>
        /// <param name="normalizedPath">The normalized directory path.</param>
        /// <param name="useAsync">If <c>true</c>, uses async IO wrappers; otherwise uses sync IO.</param>
        private async Task CreateSingleDirectoryCore(string normalizedPath, bool useAsync)
        {
            var (status, handle, _) = await CreateFileCore(
                useAsync,
                normalizedPath,
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_CREATE,
                CreateOptions.FILE_DIRECTORY_FILE,
                null).ConfigureAwait(false);

            NTStatusMapper.ThrowOnFailure(status, normalizedPath);
            await CloseFileCore(useAsync, handle).ConfigureAwait(false);
        }

        #endregion

        #region Metadata & Listing — Async

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);
            return RetryHelper.ExecuteAsync(() => ExistsCore(normalized, true), _resilience);
        }

        /// <inheritdoc />
        public Task<ShareFileInfo> GetInfoAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);
            return RetryHelper.ExecuteAsync(() => GetInfoCore(normalized, true), _resilience);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<ShareFileInfo>> ListAsync(string path, string pattern = "*", CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);
            return RetryHelper.ExecuteAsync(() => ListInternalCore(normalized, pattern, true), _resilience);
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

        #region Metadata & Listing — Sync

        /// <inheritdoc />
        public bool Exists(string path)
        {
            ThrowIfDisposed();
            var normalized = PathHelper.Normalize(path);
            return RetryHelper.Execute(() => ExistsCore(normalized, false).GetAwaiter().GetResult(), _resilience);
        }

        /// <inheritdoc />
        public ShareFileInfo GetInfo(string path)
        {
            ThrowIfDisposed();
            var normalized = PathHelper.Normalize(path);
            return RetryHelper.Execute(() => GetInfoCore(normalized, false).GetAwaiter().GetResult(), _resilience);
        }

        /// <inheritdoc />
        public IReadOnlyList<ShareFileInfo> List(string path, string pattern = "*")
        {
            ThrowIfDisposed();
            var normalized = PathHelper.Normalize(path);
            return RetryHelper.Execute(() => ListInternalCore(normalized, pattern, false).GetAwaiter().GetResult(), _resilience);
        }

        /// <inheritdoc />
        public IReadOnlyList<ShareFileInfo> ListRecursive(string path, string pattern = "*")
        {
            ThrowIfDisposed();
            var normalized = PathHelper.Normalize(path);
            var results = new List<ShareFileInfo>();
            ListRecursiveInternalSync(normalized, pattern, results);
            return results;
        }

        #endregion

        #region Read — Async

        /// <inheritdoc />
        public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);

            return await RetryHelper.ExecuteAsync(async () =>
            {
                using (var stream = await OpenReadCore(normalized, true).ConfigureAwait(false))
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
        public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);
            return RetryHelper.ExecuteAsync(() => OpenReadCore(normalized, true), _resilience);
        }

        #endregion

        #region Read — Sync

        /// <inheritdoc />
        public byte[] ReadAllBytes(string path)
        {
            ThrowIfDisposed();
            using (var stream = OpenRead(path))
            {
                using (var ms = new MemoryStream())
                {
                    var buffer = new byte[(int)_fileStore.MaxReadSize];
                    int bytesRead;
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                    }
                    return ms.ToArray();
                }
            }
        }

        /// <inheritdoc />
        public string ReadAllText(string path, Encoding encoding = null)
        {
            ThrowIfDisposed();
            var bytes = ReadAllBytes(path);
            return (encoding ?? Encoding.UTF8).GetString(bytes);
        }

        /// <inheritdoc />
        public Stream OpenRead(string path)
        {
            ThrowIfDisposed();
            var normalized = PathHelper.Normalize(path);
            return RetryHelper.Execute(() => OpenReadCore(normalized, false).GetAwaiter().GetResult(), _resilience);
        }

        #endregion

        #region Write — Async

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
                using (var stream = await OpenWriteCore(normalized, disposition, true).ConfigureAwait(false))
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
        public Task<Stream> OpenWriteAsync(string path, bool overwrite = true, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);
            var disposition = overwrite
                ? CreateDisposition.FILE_OVERWRITE_IF
                : CreateDisposition.FILE_CREATE;
            return RetryHelper.ExecuteAsync(() => OpenWriteCore(normalized, disposition, true), _resilience);
        }

        #endregion

        #region Write — Sync

        /// <inheritdoc />
        public void WriteAllBytes(string path, byte[] data, bool overwrite = true)
        {
            ThrowIfDisposed();
            using (var stream = OpenWrite(path, overwrite))
            {
                stream.Write(data, 0, data.Length);
            }
        }

        /// <inheritdoc />
        public void WriteAllText(string path, string text, Encoding encoding = null, bool overwrite = true)
        {
            ThrowIfDisposed();
            var bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
            WriteAllBytes(path, bytes, overwrite);
        }

        /// <inheritdoc />
        public Stream OpenWrite(string path, bool overwrite = true)
        {
            ThrowIfDisposed();
            var normalized = PathHelper.Normalize(path);
            var disposition = overwrite
                ? CreateDisposition.FILE_OVERWRITE_IF
                : CreateDisposition.FILE_CREATE;
            return RetryHelper.Execute(() => OpenWriteCore(normalized, disposition, false).GetAwaiter().GetResult(), _resilience);
        }

        #endregion

        #region Copy — Async

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

        #region Copy — Sync

        /// <inheritdoc />
        public void CopyFile(string src, string dst, CopyOptions options = null)
        {
            ThrowIfDisposed();
            options = options ?? new CopyOptions();

            if (!options.Overwrite && Exists(dst))
            {
                throw new ShareAlreadyExistsException(dst);
            }

            using (var readStream = OpenRead(src))
            using (var writeStream = OpenWrite(dst, options.Overwrite))
            {
                var buffer = new byte[(int)_fileStore.MaxReadSize];
                int bytesRead;
                while ((bytesRead = readStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writeStream.Write(buffer, 0, bytesRead);
                }
            }
        }

        /// <inheritdoc />
        public void CopyDirectory(string src, string dst, CopyOptions options = null)
        {
            ThrowIfDisposed();
            options = options ?? new CopyOptions();

            EnsureDirectoryExists(dst);

            var items = List(src, "*");
            foreach (var item in items)
            {
                var dstPath = PathHelper.Combine(dst, item.Name);

                if (item.IsDirectory)
                {
                    if (options.Recursive)
                    {
                        CopyDirectory(item.FullPath, dstPath, options);
                    }
                }
                else
                {
                    CopyFile(item.FullPath, dstPath, options);
                }
            }
        }

        #endregion

        #region Move — Async

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
                await RetryHelper.ExecuteAsync(
                    () => RenameInternalCore(src, dst, options.Overwrite, true),
                    _resilience).ConfigureAwait(false);
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
                await RetryHelper.ExecuteAsync(
                    () => RenameInternalCore(src, dst, options.Overwrite, true),
                    _resilience).ConfigureAwait(false);
            }
        }

        #endregion

        #region Move — Sync

        /// <inheritdoc />
        public void MoveFile(string src, string dst, MoveOptions options = null)
        {
            ThrowIfDisposed();
            options = options ?? new MoveOptions();

            if (options.Safe)
            {
                var copyOpts = new CopyOptions { Overwrite = options.Overwrite };
                CopyFile(src, dst, copyOpts);
                DeleteFile(src);
            }
            else
            {
                RetryHelper.Execute(
                    () => RenameInternalCore(src, dst, options.Overwrite, false).GetAwaiter().GetResult(),
                    _resilience);
            }
        }

        /// <inheritdoc />
        public void MoveDirectory(string src, string dst, MoveOptions options = null)
        {
            ThrowIfDisposed();
            options = options ?? new MoveOptions();

            if (options.Safe)
            {
                var copyOpts = new CopyOptions { Overwrite = options.Overwrite, Recursive = options.Recursive };
                CopyDirectory(src, dst, copyOpts);
                DeleteDirectory(src, true);
            }
            else
            {
                RetryHelper.Execute(
                    () => RenameInternalCore(src, dst, options.Overwrite, false).GetAwaiter().GetResult(),
                    _resilience);
            }
        }

        #endregion

        #region Delete — Async

        /// <inheritdoc />
        public Task DeleteFileAsync(string path, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);
            return RetryHelper.ExecuteAsync(() => DeleteFileCore(normalized, true), _resilience);
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

            await RetryHelper.ExecuteAsync(
                () => DeleteDirectorySingleCore(normalized, true),
                _resilience).ConfigureAwait(false);
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

        #region Delete — Sync

        /// <inheritdoc />
        public void DeleteFile(string path)
        {
            ThrowIfDisposed();
            var normalized = PathHelper.Normalize(path);
            RetryHelper.Execute(
                () => DeleteFileCore(normalized, false).GetAwaiter().GetResult(),
                _resilience);
        }

        /// <inheritdoc />
        public void DeleteDirectory(string path, bool recursive = false)
        {
            ThrowIfDisposed();
            var normalized = PathHelper.Normalize(path);

            if (recursive)
            {
                var items = List(path, "*");
                foreach (var item in items)
                {
                    if (item.IsDirectory)
                    {
                        DeleteDirectory(item.FullPath, true);
                    }
                    else
                    {
                        DeleteFile(item.FullPath);
                    }
                }
            }

            RetryHelper.Execute(
                () => DeleteDirectorySingleCore(normalized, false).GetAwaiter().GetResult(),
                _resilience);
        }

        /// <inheritdoc />
        public void DeleteAll(string path)
        {
            ThrowIfDisposed();
            var items = List(path, "*");
            foreach (var item in items)
            {
                if (item.IsDirectory)
                {
                    DeleteDirectory(item.FullPath, true);
                }
                else
                {
                    DeleteFile(item.FullPath);
                }
            }
        }

        #endregion

        #region Directories — Async

        /// <inheritdoc />
        public Task CreateDirectoryAsync(string path, bool createParents = true, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);

            return RetryHelper.ExecuteAsync(
                () => createParents
                    ? CreateDirectoryRecursiveCore(normalized, true)
                    : CreateSingleDirectoryCore(normalized, true),
                _resilience);
        }

        /// <inheritdoc />
        public Task EnsureDirectoryExistsAsync(string path, CancellationToken ct = default)
        {
            return CreateDirectoryAsync(path, true, ct);
        }

        #endregion

        #region Directories — Sync

        /// <inheritdoc />
        public void CreateDirectory(string path, bool createParents = true)
        {
            ThrowIfDisposed();
            var normalized = PathHelper.Normalize(path);

            RetryHelper.Execute(
                () =>
                {
                    if (createParents)
                    {
                        CreateDirectoryRecursiveCore(normalized, false).GetAwaiter().GetResult();
                    }
                    else
                    {
                        CreateSingleDirectoryCore(normalized, false).GetAwaiter().GetResult();
                    }
                },
                _resilience);
        }

        /// <inheritdoc />
        public void EnsureDirectoryExists(string path)
        {
            CreateDirectory(path, true);
        }

        #endregion

        #region Rename — Async

        /// <inheritdoc />
        public Task RenameAsync(string path, string newName, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            var normalized = PathHelper.Normalize(path);
            var parent = PathHelper.GetParent(normalized);
            var newPath = PathHelper.Combine(parent, newName);

            return RetryHelper.ExecuteAsync(
                () => RenameInternalCore(normalized, newPath, false, true),
                _resilience);
        }

        #endregion

        #region Rename — Sync

        /// <inheritdoc />
        public void Rename(string path, string newName)
        {
            ThrowIfDisposed();
            var normalized = PathHelper.Normalize(path);
            var parent = PathHelper.GetParent(normalized);
            var newPath = PathHelper.Combine(parent, newName);

            RetryHelper.Execute(
                () => RenameInternalCore(normalized, newPath, false, false).GetAwaiter().GetResult(),
                _resilience);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Recursively lists all entries under a directory (async path).
        /// </summary>
        private async Task ListRecursiveInternalAsync(string normalizedPath, string pattern, List<ShareFileInfo> results, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var allItems = await ListInternalCore(normalizedPath, "*", true).ConfigureAwait(false);

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

        /// <summary>
        /// Recursively lists all entries under a directory (sync path).
        /// </summary>
        private void ListRecursiveInternalSync(string normalizedPath, string pattern, List<ShareFileInfo> results)
        {
            var allItems = ListInternalCore(normalizedPath, "*", false).GetAwaiter().GetResult();

            foreach (var item in allItems)
            {
                if (MatchesPattern(item.Name, pattern))
                    results.Add(item);

                if (item.IsDirectory)
                {
                    ListRecursiveInternalSync(item.FullPath, pattern, results);
                }
            }
        }

        /// <summary>
        /// Determines whether a file or directory name matches a simple wildcard pattern.
        /// Supports <c>"*"</c>, <c>"*.*"</c>, and extension patterns like <c>"*.txt"</c>.
        /// </summary>
        /// <param name="name">The file or directory name to test.</param>
        /// <param name="pattern">The wildcard pattern to match against.</param>
        /// <returns><c>true</c> if <paramref name="name"/> matches <paramref name="pattern"/>; otherwise <c>false</c>.</returns>
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

        /// <summary>
        /// Converts SMBLibrary's <see cref="FileAllInformation"/> into a protocol-agnostic <see cref="ShareFileInfo"/>.
        /// </summary>
        /// <param name="fullPath">The full normalized path of the file or directory.</param>
        /// <param name="info">The raw SMB file information to convert.</param>
        /// <returns>A populated <see cref="ShareFileInfo"/> instance.</returns>
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

        /// <summary>
        /// Throws <see cref="ObjectDisposedException"/> if this instance has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the share has been disposed.</exception>
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
