using Moq;
using SimpleShareLibrary.Exceptions;
using SimpleShareLibrary.Providers.Smb;
using SMBLibrary;
using SMBLibrary.Client;

namespace SimpleShareLibrary.Tests;

[TestClass]
public class SmbShareTests
{
    private Mock<ISMBFileStore> _mockStore = null!;
    private SmbShare _share = null!;
    private readonly object _handle = new();

    [TestInitialize]
    public void Setup()
    {
        _mockStore = new Mock<ISMBFileStore>();
        _mockStore.Setup(s => s.MaxReadSize).Returns(65536);
        _mockStore.Setup(s => s.MaxWriteSize).Returns(65536);
        _share = new SmbShare(_mockStore.Object, new ResilienceOptions { MaxRetries = 0 });
    }

    [TestCleanup]
    public void Cleanup()
    {
        _share.Dispose();
    }

    // ── ExistsAsync ──────────────────────────────────────

    [TestMethod]
    public async Task ExistsAsync_FileExists_ReturnsTrue()
    {
        SetupCreateFileSuccess();

        var result = await _share.ExistsAsync("test.txt");

        Assert.IsTrue(result);
        _mockStore.Verify(s => s.CloseFile(_handle), Times.Once);
    }

    [TestMethod]
    public async Task ExistsAsync_FileNotFound_ReturnsFalse()
    {
        SetupCreateFile(NTStatus.STATUS_OBJECT_NAME_NOT_FOUND);

        var result = await _share.ExistsAsync("test.txt");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ExistsAsync_PathNotFound_ReturnsFalse()
    {
        SetupCreateFile(NTStatus.STATUS_OBJECT_PATH_NOT_FOUND);

        var result = await _share.ExistsAsync("deep/path/test.txt");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ExistsAsync_AccessDenied_Throws()
    {
        SetupCreateFile(NTStatus.STATUS_ACCESS_DENIED);

        await Assert.ThrowsExceptionAsync<ShareAccessDeniedException>(
            () => _share.ExistsAsync("secret.txt"));
    }

    // ── DeleteFileAsync ──────────────────────────────────

    [TestMethod]
    public async Task DeleteFileAsync_Success_ClosesHandle()
    {
        SetupCreateFileSuccess();

        await _share.DeleteFileAsync("file.txt");

        _mockStore.Verify(s => s.CloseFile(_handle), Times.Once);
    }

    [TestMethod]
    public async Task DeleteFileAsync_NotFound_Throws()
    {
        SetupCreateFile(NTStatus.STATUS_OBJECT_NAME_NOT_FOUND);

        await Assert.ThrowsExceptionAsync<ShareFileNotFoundException>(
            () => _share.DeleteFileAsync("missing.txt"));
    }

    // ── CreateDirectoryAsync ─────────────────────────────

    [TestMethod]
    public async Task CreateDirectoryAsync_Simple_CreatesDirectory()
    {
        SetupCreateFileSuccess();

        await _share.CreateDirectoryAsync("newdir", createParents: false);

        _mockStore.Verify(s => s.CloseFile(_handle), Times.Once);
    }

    [TestMethod]
    public async Task CreateDirectoryAsync_WithParents_CreatesRecursively()
    {
        SetupCreateFileSuccess();

        await _share.CreateDirectoryAsync("a/b/c", createParents: true);

        // Should have called CreateFile at least once for the path
        _mockStore.Verify(s => s.CreateFile(
            out It.Ref<object>.IsAny,
            out It.Ref<FileStatus>.IsAny,
            It.IsAny<string>(),
            It.IsAny<AccessMask>(),
            It.IsAny<SMBLibrary.FileAttributes>(),
            It.IsAny<ShareAccess>(),
            It.IsAny<CreateDisposition>(),
            It.IsAny<CreateOptions>(),
            It.IsAny<SecurityContext>()), Times.AtLeastOnce);
    }

    // ── RenameAsync ──────────────────────────────────────

    [TestMethod]
    public async Task RenameAsync_Success_SetsRenameInfo()
    {
        SetupCreateFileSuccess();
        _mockStore.Setup(s => s.SetFileInformation(_handle, It.IsAny<FileInformation>()))
            .Returns(NTStatus.STATUS_SUCCESS);

        await _share.RenameAsync("folder/old.txt", "new.txt");

        _mockStore.Verify(s => s.SetFileInformation(_handle, It.IsAny<FileRenameInformationType2>()), Times.Once);
        _mockStore.Verify(s => s.CloseFile(_handle), Times.Once);
    }

    // ── WriteAllBytesAsync ───────────────────────────────

    [TestMethod]
    public async Task WriteAllBytesAsync_WritesData()
    {
        var data = new byte[] { 10, 20, 30, 40, 50 };
        byte[]? writtenData = null;

        SetupCreateFileSuccess();
        int bw = data.Length;
        _mockStore.Setup(s => s.WriteFile(out bw, _handle, It.IsAny<long>(), It.IsAny<byte[]>()))
            .Callback(new WriteFileCallbackVoid((out int bytesWritten, object h, long pos, byte[] d) =>
            {
                writtenData = d;
                bytesWritten = d.Length;
            }))
            .Returns(NTStatus.STATUS_SUCCESS);

        await _share.WriteAllBytesAsync("data.bin", data);

        Assert.IsNotNull(writtenData);
        CollectionAssert.AreEqual(data, writtenData);
    }

    // ── ReadAllBytesAsync ────────────────────────────────

    [TestMethod]
    public async Task ReadAllBytesAsync_ReadsData()
    {
        var expected = new byte[] { 1, 2, 3, 4, 5 };
        SetupCreateFileSuccess();

        int readCall = 0;
        _mockStore.Setup(s => s.ReadFile(out It.Ref<byte[]>.IsAny, _handle, It.IsAny<long>(), It.IsAny<int>()))
            .Callback(new ReadFileCallbackVoid((out byte[] d, object h, long pos, int count) =>
            {
                readCall++;
                d = readCall == 1 ? expected : Array.Empty<byte>();
            }))
            .Returns(() => readCall == 0 ? NTStatus.STATUS_SUCCESS : NTStatus.STATUS_END_OF_FILE);

        // Re-setup with proper sequencing: first call returns data, second returns EOF
        readCall = 0;
        _mockStore.Setup(s => s.ReadFile(out It.Ref<byte[]>.IsAny, _handle, It.IsAny<long>(), It.IsAny<int>()))
            .Returns(new ReadFileReturnDelegate((out byte[] d, object h, long pos, int count) =>
            {
                readCall++;
                if (readCall == 1)
                {
                    d = expected;
                    return NTStatus.STATUS_SUCCESS;
                }
                d = Array.Empty<byte>();
                return NTStatus.STATUS_END_OF_FILE;
            }));

        var result = await _share.ReadAllBytesAsync("file.bin");

        CollectionAssert.AreEqual(expected, result);
    }

    // ── CopyFileAsync ────────────────────────────────────

    [TestMethod]
    public async Task CopyFileAsync_NoOverwriteAndDestExists_Throws()
    {
        SetupCreateFileSuccess();

        await Assert.ThrowsExceptionAsync<ShareAlreadyExistsException>(
            () => _share.CopyFileAsync("src.txt", "dst.txt", new CopyOptions { Overwrite = false }));
    }

    // ── MoveFileAsync ────────────────────────────────────

    [TestMethod]
    public async Task MoveFileAsync_UnsafeMode_CallsRename()
    {
        SetupCreateFileSuccess();
        _mockStore.Setup(s => s.SetFileInformation(_handle, It.IsAny<FileInformation>()))
            .Returns(NTStatus.STATUS_SUCCESS);

        await _share.MoveFileAsync("old.txt", "new.txt", new MoveOptions { Safe = false });

        _mockStore.Verify(s => s.SetFileInformation(_handle, It.IsAny<FileRenameInformationType2>()), Times.Once);
    }

    // ── Disposed ─────────────────────────────────────────

    [TestMethod]
    public async Task ExistsAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        _share.Dispose();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => _share.ExistsAsync("test.txt"));
    }

    [TestMethod]
    public async Task ReadAllBytesAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        _share.Dispose();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => _share.ReadAllBytesAsync("test.txt"));
    }

    [TestMethod]
    public async Task WriteAllBytesAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        _share.Dispose();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => _share.WriteAllBytesAsync("test.txt", new byte[] { 1 }));
    }

    [TestMethod]
    public async Task DeleteFileAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        _share.Dispose();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => _share.DeleteFileAsync("test.txt"));
    }

    [TestMethod]
    public async Task ListAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        _share.Dispose();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => _share.ListAsync("dir"));
    }

    [TestMethod]
    public async Task CreateDirectoryAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        _share.Dispose();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => _share.CreateDirectoryAsync("dir"));
    }

    [TestMethod]
    public async Task RenameAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        _share.Dispose();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => _share.RenameAsync("old", "new"));
    }

    // ── CancellationToken ────────────────────────────────

    [TestMethod]
    public async Task ExistsAsync_Cancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => _share.ExistsAsync("test.txt", cts.Token));
    }

    [TestMethod]
    public async Task ReadAllBytesAsync_Cancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => _share.ReadAllBytesAsync("test.txt", cts.Token));
    }

    // ── Path Normalization ───────────────────────────────

    [TestMethod]
    public async Task ExistsAsync_ForwardSlashPath_NormalizesBeforeCall()
    {
        SetupCreateFileSuccess();
        var result = await _share.ExistsAsync("folder/file.txt");
        Assert.IsTrue(result);
    }

    // ── Dispose disconnects file store ───────────────────

    [TestMethod]
    public void Dispose_DisconnectsFileStore()
    {
        var share = new SmbShare(_mockStore.Object);
        share.Dispose();
        _mockStore.Verify(s => s.Disconnect(), Times.Once);
    }

    [TestMethod]
    public void Dispose_DoubleDispose_DisconnectsOnce()
    {
        var share = new SmbShare(_mockStore.Object);
        share.Dispose();
        share.Dispose();
        _mockStore.Verify(s => s.Disconnect(), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────

    private void SetupCreateFileSuccess()
    {
        _mockStore.Setup(s => s.CreateFile(
                out It.Ref<object>.IsAny,
                out It.Ref<FileStatus>.IsAny,
                It.IsAny<string>(),
                It.IsAny<AccessMask>(),
                It.IsAny<SMBLibrary.FileAttributes>(),
                It.IsAny<ShareAccess>(),
                It.IsAny<CreateDisposition>(),
                It.IsAny<CreateOptions>(),
                It.IsAny<SecurityContext>()))
            .Callback(new CreateFileCallbackVoid((
                out object h, out FileStatus fs,
                string p, AccessMask am, SMBLibrary.FileAttributes fa,
                ShareAccess sa, CreateDisposition cd, CreateOptions co, SecurityContext sc) =>
            {
                h = _handle;
                fs = FileStatus.FILE_OPENED;
            }))
            .Returns(NTStatus.STATUS_SUCCESS);
    }

    private void SetupCreateFile(NTStatus status)
    {
        _mockStore.Setup(s => s.CreateFile(
                out It.Ref<object>.IsAny,
                out It.Ref<FileStatus>.IsAny,
                It.IsAny<string>(),
                It.IsAny<AccessMask>(),
                It.IsAny<SMBLibrary.FileAttributes>(),
                It.IsAny<ShareAccess>(),
                It.IsAny<CreateDisposition>(),
                It.IsAny<CreateOptions>(),
                It.IsAny<SecurityContext>()))
            .Callback(new CreateFileCallbackVoid((
                out object h, out FileStatus fs,
                string p, AccessMask am, SMBLibrary.FileAttributes fa,
                ShareAccess sa, CreateDisposition cd, CreateOptions co, SecurityContext sc) =>
            {
                h = new object();
                fs = FileStatus.FILE_DOES_NOT_EXIST;
            }))
            .Returns(status);
    }

    // ── Delegates for Moq out parameters ─────────────────

    private delegate void CreateFileCallbackVoid(
        out object handle, out FileStatus fileStatus,
        string path, AccessMask accessMask, SMBLibrary.FileAttributes fileAttributes,
        ShareAccess shareAccess, CreateDisposition createDisposition,
        CreateOptions createOptions, SecurityContext securityContext);

    private delegate void ReadFileCallbackVoid(out byte[] data, object handle, long offset, int count);
    private delegate void WriteFileCallbackVoid(out int bytesWritten, object handle, long offset, byte[] data);
    private delegate NTStatus ReadFileReturnDelegate(out byte[] data, object handle, long offset, int count);
}
