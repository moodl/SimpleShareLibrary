using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleShareLibrary
{
    /// <summary>
    /// Protocol-agnostic interface for file and directory operations on a remote share.
    /// Each operation has both an async and sync overload.
    /// </summary>
    public interface IShare : IDisposable
    {
        #region Metadata & Listing

        /// <summary>Checks whether a file or directory exists at the specified path.</summary>
        /// <param name="path">The path to check.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><c>true</c> if the path exists; otherwise <c>false</c>.</returns>
        Task<bool> ExistsAsync(string path, CancellationToken ct = default);

        /// <inheritdoc cref="ExistsAsync"/>
        bool Exists(string path);

        /// <summary>Gets metadata for the file or directory at the specified path.</summary>
        /// <param name="path">The path to query.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A <see cref="ShareFileInfo"/> describing the entry.</returns>
        Task<ShareFileInfo> GetInfoAsync(string path, CancellationToken ct = default);

        /// <inheritdoc cref="GetInfoAsync"/>
        ShareFileInfo GetInfo(string path);

        /// <summary>Lists files and directories in the given path matching a pattern.</summary>
        /// <param name="path">The directory path to list.</param>
        /// <param name="pattern">Glob pattern for filtering results. Defaults to all files.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A read-only list of matching entries.</returns>
        Task<IReadOnlyList<ShareFileInfo>> ListAsync(string path, string pattern = "*", CancellationToken ct = default);

        /// <inheritdoc cref="ListAsync"/>
        IReadOnlyList<ShareFileInfo> List(string path, string pattern = "*");

        /// <summary>Recursively lists files and directories matching a pattern.</summary>
        /// <param name="path">The root directory path.</param>
        /// <param name="pattern">Glob pattern for filtering results. Defaults to all files.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A read-only list of all matching entries in the subtree.</returns>
        Task<IReadOnlyList<ShareFileInfo>> ListRecursiveAsync(string path, string pattern = "*", CancellationToken ct = default);

        /// <inheritdoc cref="ListRecursiveAsync"/>
        IReadOnlyList<ShareFileInfo> ListRecursive(string path, string pattern = "*");

        #endregion

        #region Read

        /// <summary>Reads the entire contents of a file as a byte array.</summary>
        /// <param name="path">The file path.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The file contents as bytes.</returns>
        Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default);

        /// <inheritdoc cref="ReadAllBytesAsync"/>
        byte[] ReadAllBytes(string path);

        /// <summary>Reads the entire contents of a file as text.</summary>
        /// <param name="path">The file path.</param>
        /// <param name="encoding">Text encoding. Defaults to UTF-8.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The file contents as a string.</returns>
        Task<string> ReadAllTextAsync(string path, Encoding encoding = null, CancellationToken ct = default);

        /// <inheritdoc cref="ReadAllTextAsync"/>
        string ReadAllText(string path, Encoding encoding = null);

        /// <summary>Opens a read-only stream to the specified file.</summary>
        /// <param name="path">The file path.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A readable <see cref="Stream"/>.</returns>
        Task<Stream> OpenReadAsync(string path, CancellationToken ct = default);

        /// <inheritdoc cref="OpenReadAsync"/>
        Stream OpenRead(string path);

        #endregion

        #region Write

        /// <summary>Writes a byte array to a file.</summary>
        /// <param name="path">The destination file path.</param>
        /// <param name="data">The bytes to write.</param>
        /// <param name="overwrite">Whether to overwrite an existing file. Defaults to <c>true</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        Task WriteAllBytesAsync(string path, byte[] data, bool overwrite = true, CancellationToken ct = default);

        /// <inheritdoc cref="WriteAllBytesAsync"/>
        void WriteAllBytes(string path, byte[] data, bool overwrite = true);

        /// <summary>Writes text to a file.</summary>
        /// <param name="path">The destination file path.</param>
        /// <param name="text">The text to write.</param>
        /// <param name="encoding">Text encoding. Defaults to UTF-8.</param>
        /// <param name="overwrite">Whether to overwrite an existing file. Defaults to <c>true</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        Task WriteAllTextAsync(string path, string text, Encoding encoding = null, bool overwrite = true, CancellationToken ct = default);

        /// <inheritdoc cref="WriteAllTextAsync"/>
        void WriteAllText(string path, string text, Encoding encoding = null, bool overwrite = true);

        /// <summary>Opens a write stream to the specified file.</summary>
        /// <param name="path">The destination file path.</param>
        /// <param name="overwrite">Whether to overwrite an existing file. Defaults to <c>true</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A writable <see cref="Stream"/>.</returns>
        Task<Stream> OpenWriteAsync(string path, bool overwrite = true, CancellationToken ct = default);

        /// <inheritdoc cref="OpenWriteAsync"/>
        Stream OpenWrite(string path, bool overwrite = true);

        #endregion

        #region Copy

        /// <summary>Copies a single file from source to destination.</summary>
        /// <param name="src">The source file path.</param>
        /// <param name="dst">The destination file path.</param>
        /// <param name="options">Copy options. Uses defaults if <c>null</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        Task CopyFileAsync(string src, string dst, CopyOptions options = null, CancellationToken ct = default);

        /// <inheritdoc cref="CopyFileAsync"/>
        void CopyFile(string src, string dst, CopyOptions options = null);

        /// <summary>Copies a directory and its contents from source to destination.</summary>
        /// <param name="src">The source directory path.</param>
        /// <param name="dst">The destination directory path.</param>
        /// <param name="options">Copy options. Uses defaults if <c>null</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        Task CopyDirectoryAsync(string src, string dst, CopyOptions options = null, CancellationToken ct = default);

        /// <inheritdoc cref="CopyDirectoryAsync"/>
        void CopyDirectory(string src, string dst, CopyOptions options = null);

        #endregion

        #region Move

        /// <summary>Moves a single file. Uses safe copy-then-delete by default.</summary>
        /// <param name="src">The source file path.</param>
        /// <param name="dst">The destination file path.</param>
        /// <param name="options">Move options. Uses defaults if <c>null</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        Task MoveFileAsync(string src, string dst, MoveOptions options = null, CancellationToken ct = default);

        /// <inheritdoc cref="MoveFileAsync"/>
        void MoveFile(string src, string dst, MoveOptions options = null);

        /// <summary>Moves a directory. Uses safe copy-then-delete by default.</summary>
        /// <param name="src">The source directory path.</param>
        /// <param name="dst">The destination directory path.</param>
        /// <param name="options">Move options. Uses defaults if <c>null</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        Task MoveDirectoryAsync(string src, string dst, MoveOptions options = null, CancellationToken ct = default);

        /// <inheritdoc cref="MoveDirectoryAsync"/>
        void MoveDirectory(string src, string dst, MoveOptions options = null);

        #endregion

        #region Delete

        /// <summary>Deletes a single file.</summary>
        /// <param name="path">The file path to delete.</param>
        /// <param name="ct">Cancellation token.</param>
        Task DeleteFileAsync(string path, CancellationToken ct = default);

        /// <inheritdoc cref="DeleteFileAsync"/>
        void DeleteFile(string path);

        /// <summary>Deletes a directory, optionally including all contents.</summary>
        /// <param name="path">The directory path to delete.</param>
        /// <param name="recursive">Whether to delete contents recursively.</param>
        /// <param name="ct">Cancellation token.</param>
        Task DeleteDirectoryAsync(string path, bool recursive = false, CancellationToken ct = default);

        /// <inheritdoc cref="DeleteDirectoryAsync"/>
        void DeleteDirectory(string path, bool recursive = false);

        /// <summary>Deletes all files and subdirectories within the specified path.</summary>
        /// <param name="path">The directory whose contents will be deleted.</param>
        /// <param name="ct">Cancellation token.</param>
        Task DeleteAllAsync(string path, CancellationToken ct = default);

        /// <inheritdoc cref="DeleteAllAsync"/>
        void DeleteAll(string path);

        #endregion

        #region Directories

        /// <summary>Creates a directory, optionally creating parent directories.</summary>
        /// <param name="path">The directory path to create.</param>
        /// <param name="createParents">Whether to create missing parent directories.</param>
        /// <param name="ct">Cancellation token.</param>
        Task CreateDirectoryAsync(string path, bool createParents = true, CancellationToken ct = default);

        /// <inheritdoc cref="CreateDirectoryAsync"/>
        void CreateDirectory(string path, bool createParents = true);

        /// <summary>Ensures a directory exists, creating it if necessary.</summary>
        /// <param name="path">The directory path.</param>
        /// <param name="ct">Cancellation token.</param>
        Task EnsureDirectoryExistsAsync(string path, CancellationToken ct = default);

        /// <inheritdoc cref="EnsureDirectoryExistsAsync"/>
        void EnsureDirectoryExists(string path);

        #endregion

        #region Rename

        /// <summary>Renames a file or directory.</summary>
        /// <param name="path">The current path.</param>
        /// <param name="newName">The new name (not a full path).</param>
        /// <param name="ct">Cancellation token.</param>
        Task RenameAsync(string path, string newName, CancellationToken ct = default);

        /// <inheritdoc cref="RenameAsync"/>
        void Rename(string path, string newName);

        #endregion
    }
}
