using System;
using SimpleShareLibrary.Exceptions;
using SMBLibrary;

namespace SimpleShareLibrary.Providers.Smb
{
    /// <summary>
    /// Maps SMBLibrary <see cref="NTStatus"/> codes to protocol-agnostic
    /// <see cref="ShareException"/> types, enabling provider-independent error handling.
    /// </summary>
    internal static class NTStatusMapper
    {
        /// <summary>
        /// Throws a protocol-agnostic exception if the <paramref name="status"/> indicates failure.
        /// Does nothing when <paramref name="status"/> is <see cref="NTStatus.STATUS_SUCCESS"/>.
        /// </summary>
        /// <param name="status">The SMB status code to evaluate.</param>
        /// <param name="path">Optional file or directory path included in the exception message.</param>
        /// <exception cref="ShareException">Thrown when <paramref name="status"/> is not <see cref="NTStatus.STATUS_SUCCESS"/>.</exception>
        internal static void ThrowOnFailure(NTStatus status, string path = null)
        {
            if (status == NTStatus.STATUS_SUCCESS)
                return;

            throw ToException(status, path);
        }

        /// <summary>
        /// Determines whether the given <paramref name="status"/> represents a transient,
        /// retryable failure (e.g. timeout, sharing violation, network loss).
        /// </summary>
        /// <param name="status">The SMB status code to evaluate.</param>
        /// <returns><c>true</c> if the status is transient and the operation may succeed on retry; otherwise <c>false</c>.</returns>
        internal static bool IsTransient(NTStatus status)
        {
            switch (status)
            {
                case NTStatus.STATUS_IO_TIMEOUT:
                case NTStatus.STATUS_SHARING_VIOLATION:
                case NTStatus.STATUS_NETWORK_NAME_DELETED:
                case NTStatus.STATUS_INSUFFICIENT_RESOURCES:
                case NTStatus.STATUS_REQUEST_NOT_ACCEPTED:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Converts an <see cref="NTStatus"/> failure code into the appropriate <see cref="ShareException"/> subclass.
        /// </summary>
        /// <param name="status">The failing SMB status code.</param>
        /// <param name="path">The file or directory path associated with the failure.</param>
        /// <returns>A <see cref="ShareException"/> subclass matching the failure type.</returns>
        private static ShareException ToException(NTStatus status, string path)
        {
            switch (status)
            {
                case NTStatus.STATUS_OBJECT_NAME_NOT_FOUND:
                    return new ShareFileNotFoundException(path ?? "unknown");

                case NTStatus.STATUS_OBJECT_PATH_NOT_FOUND:
                    return new ShareDirectoryNotFoundException(path ?? "unknown");

                case NTStatus.STATUS_ACCESS_DENIED:
                    return new ShareAccessDeniedException(path ?? "unknown");

                case NTStatus.STATUS_OBJECT_NAME_COLLISION:
                    return new ShareAlreadyExistsException(path ?? "unknown");

                case NTStatus.STATUS_LOGON_FAILURE:
                case NTStatus.STATUS_WRONG_PASSWORD:
                case NTStatus.STATUS_ACCOUNT_DISABLED:
                case NTStatus.STATUS_ACCOUNT_LOCKED_OUT:
                    return new ShareAuthenticationException(
                        $"Authentication failed: {status}");

                case NTStatus.STATUS_DIRECTORY_NOT_EMPTY:
                    return new ShareIOException(
                        $"Directory not empty: '{path ?? "unknown"}'");

                case NTStatus.STATUS_DISK_FULL:
                    return new ShareIOException(
                        $"Disk full while operating on: '{path ?? "unknown"}'");

                case NTStatus.STATUS_IO_TIMEOUT:
                    return new ShareIOException(
                        $"Operation timed out: '{path ?? "unknown"}'");

                case NTStatus.STATUS_SHARING_VIOLATION:
                    return new ShareIOException(
                        $"Sharing violation: '{path ?? "unknown"}'");

                case NTStatus.STATUS_NETWORK_NAME_DELETED:
                    return new ShareConnectionException(
                        $"Network connection lost: '{path ?? "unknown"}'");

                case NTStatus.STATUS_NO_SUCH_FILE:
                    return new ShareFileNotFoundException(path ?? "unknown");

                case NTStatus.STATUS_MEDIA_WRITE_PROTECTED:
                    return new ShareAccessDeniedException(path ?? "unknown");

                case NTStatus.STATUS_INSUFFICIENT_RESOURCES:
                    return new ShareIOException(
                        $"Insufficient resources: '{path ?? "unknown"}'");

                case NTStatus.STATUS_REQUEST_NOT_ACCEPTED:
                    return new ShareIOException(
                        $"Request not accepted: '{path ?? "unknown"}'");

                default:
                    return new ShareIOException(
                        $"Operation failed with status {status}: '{path ?? "unknown"}'");
            }
        }
    }
}
