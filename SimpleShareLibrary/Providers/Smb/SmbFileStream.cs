using System;
using System.IO;
using SMBLibrary;
using SMBLibrary.Client;

namespace SimpleShareLibrary.Providers.Smb
{
    /// <summary>
    /// A <see cref="Stream"/> implementation that reads/writes over SMB using chunked I/O.
    /// The underlying file handle is managed internally and closed on dispose.
    /// </summary>
    internal class SmbFileStream : Stream
    {
        #region Fields

        private readonly ISMBFileStore _fileStore;
        private readonly object _handle;
        private readonly bool _canRead;
        private readonly bool _canWrite;
        private readonly int _maxReadSize;
        private readonly int _maxWriteSize;
        private readonly Func<bool>? _isParentDisposed;
        private long _position;
        private long _length;
        private bool _disposed;

        #endregion

        #region Constructors

        /// <summary>Initializes a new instance wrapping an SMB file handle.</summary>
        /// <param name="fileStore">The SMB file store that owns the handle.</param>
        /// <param name="handle">The open file handle.</param>
        /// <param name="canRead">Whether the stream supports reading.</param>
        /// <param name="canWrite">Whether the stream supports writing.</param>
        /// <param name="isParentDisposed">Optional callback to check if the parent share or client has been disposed.</param>
        internal SmbFileStream(ISMBFileStore fileStore, object handle, bool canRead, bool canWrite, Func<bool>? isParentDisposed = null)
        {
            _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            _canRead = canRead;
            _canWrite = canWrite;
            _maxReadSize = (int)fileStore.MaxReadSize;
            _maxWriteSize = (int)fileStore.MaxWriteSize;
            _isParentDisposed = isParentDisposed;
            _position = 0;
            _length = QueryFileLength();
        }

        #endregion

        #region Stream Properties

        /// <inheritdoc />
        public override bool CanRead => _canRead && !_disposed;

        /// <inheritdoc />
        public override bool CanWrite => _canWrite && !_disposed;

        /// <inheritdoc />
        public override bool CanSeek => true;

        /// <inheritdoc />
        public override long Length
        {
            get
            {
                ThrowIfDisposed();
                if (_length < 0)
                    throw new NotSupportedException("File length could not be determined.");
                return _length;
            }
        }

        /// <inheritdoc />
        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        #endregion

        #region Stream Methods

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            if (!_canRead)
                throw new NotSupportedException("Stream is not readable.");

            int totalRead = 0;
            while (totalRead < count)
            {
                int chunkSize = Math.Min(count - totalRead, _maxReadSize);
                var status = _fileStore.ReadFile(out byte[] data, _handle, _position, chunkSize);

                if (status == NTStatus.STATUS_END_OF_FILE)
                    break;

                NTStatusMapper.ThrowOnFailure(status);

                if (data == null || data.Length == 0)
                    break;

                Buffer.BlockCopy(data, 0, buffer, offset + totalRead, data.Length);
                totalRead += data.Length;
                _position += data.Length;

                if (data.Length < chunkSize)
                    break;
            }

            return totalRead;
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            if (!_canWrite)
                throw new NotSupportedException("Stream is not writable.");

            int totalWritten = 0;
            while (totalWritten < count)
            {
                int chunkSize = Math.Min(count - totalWritten, _maxWriteSize);
                byte[] chunk = new byte[chunkSize];
                Buffer.BlockCopy(buffer, offset + totalWritten, chunk, 0, chunkSize);

                var status = _fileStore.WriteFile(out int bytesWritten, _handle, _position, chunk);
                NTStatusMapper.ThrowOnFailure(status);

                if (bytesWritten == 0)
                {
                    throw new IOException("SMB server accepted zero bytes on write.");
                }

                totalWritten += bytesWritten;
                _position += bytesWritten;
            }

            if (_length >= 0)
                _length = Math.Max(_length, _position);
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position = offset;
                    break;
                case SeekOrigin.Current:
                    _position += offset;
                    break;
                case SeekOrigin.End:
                    if (_length < 0)
                        throw new NotSupportedException("SeekOrigin.End requires known file length.");
                    _position = _length + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }
            return _position;
        }

        /// <inheritdoc />
        public override void Flush()
        {
            ThrowIfDisposed();
            _fileStore.FlushFileBuffers(_handle);
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException("SMB streams do not support SetLength.");
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _fileStore.CloseFile(_handle);
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Private Helpers

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SmbFileStream));
            if (_isParentDisposed?.Invoke() == true)
                throw new ObjectDisposedException(nameof(SmbShare),
                    "The parent share or client has been disposed.");
        }

        /// <summary>
        /// Queries the file length from the SMB server using the already-open handle.
        /// Returns -1 if the query fails (length will be unavailable).
        /// </summary>
        private long QueryFileLength()
        {
            try
            {
                var status = _fileStore.GetFileInformation(
                    out FileInformation info, _handle,
                    FileInformationClass.FileStandardInformation);

                if (status == NTStatus.STATUS_SUCCESS && info is FileStandardInformation stdInfo)
                {
                    return stdInfo.EndOfFile;
                }
            }
            catch
            {
                // Swallow — length simply won't be available
            }

            return -1;
        }

        #endregion
    }
}
