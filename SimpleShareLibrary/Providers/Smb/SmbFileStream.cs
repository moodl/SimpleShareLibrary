using System;
using System.IO;
using SMBLibrary;
using SMBLibrary.Client;

namespace SimpleShareLibrary.Providers.Smb
{
    /// <summary>
    /// A Stream implementation that reads/writes over SMB using chunked I/O.
    /// The underlying file handle is managed internally and closed on dispose.
    /// </summary>
    internal class SmbFileStream : Stream
    {
        private readonly ISMBFileStore _fileStore;
        private readonly object _handle;
        private readonly bool _canRead;
        private readonly bool _canWrite;
        private readonly int _maxReadSize;
        private readonly int _maxWriteSize;
        private long _position;
        private bool _disposed;

        internal SmbFileStream(ISMBFileStore fileStore, object handle, bool canRead, bool canWrite)
        {
            _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            _canRead = canRead;
            _canWrite = canWrite;
            _maxReadSize = (int)fileStore.MaxReadSize;
            _maxWriteSize = (int)fileStore.MaxWriteSize;
            _position = 0;
        }

        public override bool CanRead => _canRead && !_disposed;
        public override bool CanWrite => _canWrite && !_disposed;
        public override bool CanSeek => true;

        public override long Length => throw new NotSupportedException("SMB streams do not support Length.");

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

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

                totalWritten += bytesWritten;
                _position += bytesWritten;
            }
        }

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
                    throw new NotSupportedException("SeekOrigin.End is not supported for SMB streams.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }
            return _position;
        }

        public override void Flush()
        {
            ThrowIfDisposed();
            _fileStore.FlushFileBuffers(_handle);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("SMB streams do not support SetLength.");
        }

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

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SmbFileStream));
        }
    }
}
