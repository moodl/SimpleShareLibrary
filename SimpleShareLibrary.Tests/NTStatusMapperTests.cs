using SimpleShareLibrary.Exceptions;
using SimpleShareLibrary.Providers.Smb;
using SMBLibrary;

namespace SimpleShareLibrary.Tests;

[TestClass]
public class NTStatusMapperTests
{
    // ── ThrowOnFailure ───────────────────────────────────

    [TestMethod]
    public void ThrowOnFailure_Success_DoesNotThrow()
    {
        NTStatusMapper.ThrowOnFailure(NTStatus.STATUS_SUCCESS, "test");
    }

    [TestMethod]
    public void ThrowOnFailure_ObjectNameNotFound_ThrowsShareFileNotFoundException()
    {
        var ex = Assert.ThrowsException<ShareFileNotFoundException>(
            () => NTStatusMapper.ThrowOnFailure(NTStatus.STATUS_OBJECT_NAME_NOT_FOUND, "test.txt"));
        Assert.AreEqual("test.txt", ex.Path);
    }

    [TestMethod]
    public void ThrowOnFailure_ObjectPathNotFound_ThrowsShareDirectoryNotFoundException()
    {
        var ex = Assert.ThrowsException<ShareDirectoryNotFoundException>(
            () => NTStatusMapper.ThrowOnFailure(NTStatus.STATUS_OBJECT_PATH_NOT_FOUND, "mydir"));
        Assert.AreEqual("mydir", ex.Path);
    }

    [TestMethod]
    public void ThrowOnFailure_AccessDenied_ThrowsShareAccessDeniedException()
    {
        var ex = Assert.ThrowsException<ShareAccessDeniedException>(
            () => NTStatusMapper.ThrowOnFailure(NTStatus.STATUS_ACCESS_DENIED, "secret.txt"));
        Assert.AreEqual("secret.txt", ex.Path);
    }

    [TestMethod]
    public void ThrowOnFailure_ObjectNameCollision_ThrowsShareAlreadyExistsException()
    {
        var ex = Assert.ThrowsException<ShareAlreadyExistsException>(
            () => NTStatusMapper.ThrowOnFailure(NTStatus.STATUS_OBJECT_NAME_COLLISION, "dup.txt"));
        Assert.AreEqual("dup.txt", ex.Path);
    }

    [TestMethod]
    [DataRow(NTStatus.STATUS_LOGON_FAILURE)]
    [DataRow(NTStatus.STATUS_WRONG_PASSWORD)]
    [DataRow(NTStatus.STATUS_ACCOUNT_DISABLED)]
    [DataRow(NTStatus.STATUS_ACCOUNT_LOCKED_OUT)]
    public void ThrowOnFailure_AuthStatuses_ThrowShareAuthenticationException(NTStatus status)
    {
        Assert.ThrowsException<ShareAuthenticationException>(
            () => NTStatusMapper.ThrowOnFailure(status));
    }

    [TestMethod]
    [DataRow(NTStatus.STATUS_DIRECTORY_NOT_EMPTY)]
    [DataRow(NTStatus.STATUS_DISK_FULL)]
    [DataRow(NTStatus.STATUS_IO_TIMEOUT)]
    [DataRow(NTStatus.STATUS_SHARING_VIOLATION)]
    [DataRow(NTStatus.STATUS_INSUFFICIENT_RESOURCES)]
    [DataRow(NTStatus.STATUS_REQUEST_NOT_ACCEPTED)]
    public void ThrowOnFailure_IOStatuses_ThrowShareIOException(NTStatus status)
    {
        Assert.ThrowsException<ShareIOException>(
            () => NTStatusMapper.ThrowOnFailure(status, "file"));
    }

    [TestMethod]
    public void ThrowOnFailure_NetworkNameDeleted_ThrowsShareConnectionException()
    {
        Assert.ThrowsException<ShareConnectionException>(
            () => NTStatusMapper.ThrowOnFailure(NTStatus.STATUS_NETWORK_NAME_DELETED, "file"));
    }

    [TestMethod]
    public void ThrowOnFailure_NoSuchFile_ThrowsShareFileNotFoundException()
    {
        var ex = Assert.ThrowsException<ShareFileNotFoundException>(
            () => NTStatusMapper.ThrowOnFailure(NTStatus.STATUS_NO_SUCH_FILE, "gone.txt"));
        Assert.AreEqual("gone.txt", ex.Path);
    }

    [TestMethod]
    public void ThrowOnFailure_MediaWriteProtected_ThrowsShareAccessDeniedException()
    {
        var ex = Assert.ThrowsException<ShareAccessDeniedException>(
            () => NTStatusMapper.ThrowOnFailure(NTStatus.STATUS_MEDIA_WRITE_PROTECTED, "readonly.txt"));
        Assert.AreEqual("readonly.txt", ex.Path);
    }

    [TestMethod]
    public void ThrowOnFailure_UnmappedStatus_ThrowsShareIOException()
    {
        Assert.ThrowsException<ShareIOException>(
            () => NTStatusMapper.ThrowOnFailure(NTStatus.STATUS_INVALID_PARAMETER, "file"));
    }

    [TestMethod]
    public void ThrowOnFailure_NullPath_UsesUnknown()
    {
        var ex = Assert.ThrowsException<ShareFileNotFoundException>(
            () => NTStatusMapper.ThrowOnFailure(NTStatus.STATUS_OBJECT_NAME_NOT_FOUND));
        Assert.AreEqual("unknown", ex.Path);
    }

    // ── IsTransient ──────────────────────────────────────

    [TestMethod]
    [DataRow(NTStatus.STATUS_IO_TIMEOUT)]
    [DataRow(NTStatus.STATUS_SHARING_VIOLATION)]
    [DataRow(NTStatus.STATUS_NETWORK_NAME_DELETED)]
    [DataRow(NTStatus.STATUS_INSUFFICIENT_RESOURCES)]
    [DataRow(NTStatus.STATUS_REQUEST_NOT_ACCEPTED)]
    public void IsTransient_TransientStatuses_ReturnsTrue(NTStatus status)
    {
        Assert.IsTrue(NTStatusMapper.IsTransient(status));
    }

    [TestMethod]
    [DataRow(NTStatus.STATUS_ACCESS_DENIED)]
    [DataRow(NTStatus.STATUS_OBJECT_NAME_NOT_FOUND)]
    [DataRow(NTStatus.STATUS_LOGON_FAILURE)]
    [DataRow(NTStatus.STATUS_SUCCESS)]
    public void IsTransient_NonTransientStatuses_ReturnsFalse(NTStatus status)
    {
        Assert.IsFalse(NTStatusMapper.IsTransient(status));
    }
}
