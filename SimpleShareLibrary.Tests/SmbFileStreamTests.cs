using Moq;
using SimpleShareLibrary.Providers.Smb;
using SMBLibrary;
using SMBLibrary.Client;

namespace SimpleShareLibrary.Tests;

[TestClass]
public class SmbFileStreamTests
{
    private Mock<ISMBFileStore> _mockStore = null!;
    private readonly object _handle = new();

    [TestInitialize]
    public void Setup()
    {
        _mockStore = new Mock<ISMBFileStore>();
        _mockStore.Setup(s => s.MaxReadSize).Returns(4096);
        _mockStore.Setup(s => s.MaxWriteSize).Returns(4096);
    }

    #region Read

    [TestMethod]
    public void Read_SingleChunk_ReturnsData()
    {
        var expected = new byte[] { 1, 2, 3, 4, 5 };
        _mockStore.Setup(s => s.ReadFile(out It.Ref<byte[]>.IsAny, _handle, 0, It.IsAny<int>()))
            .Callback(new ReadFileCallbackVoid((out byte[] data, object h, long pos, int count) =>
            {
                data = expected;
            }))
            .Returns(NTStatus.STATUS_SUCCESS);

        using var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        var buffer = new byte[10];
        int bytesRead = stream.Read(buffer, 0, 10);

        Assert.AreEqual(5, bytesRead);
        CollectionAssert.AreEqual(expected, buffer[..5]);
    }

    [TestMethod]
    public void Read_EndOfFile_ReturnsZero()
    {
        _mockStore.Setup(s => s.ReadFile(out It.Ref<byte[]>.IsAny, _handle, 0, It.IsAny<int>()))
            .Returns(NTStatus.STATUS_END_OF_FILE);

        using var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        var buffer = new byte[10];
        int bytesRead = stream.Read(buffer, 0, 10);

        Assert.AreEqual(0, bytesRead);
    }

    [TestMethod]
    public void Read_NotReadable_Throws()
    {
        using var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: false, canWrite: true);
        Assert.ThrowsException<NotSupportedException>(() => stream.Read(new byte[10], 0, 10));
    }

    #endregion

    #region Write

    [TestMethod]
    public void Write_SingleChunk_WritesData()
    {
        byte[]? writtenData = null;
        int bytesWritten = 5;
        _mockStore.Setup(s => s.WriteFile(out bytesWritten, _handle, 0, It.IsAny<byte[]>()))
            .Callback(new WriteFileCallbackVoid((out int bw, object h, long pos, byte[] data) =>
            {
                writtenData = data;
                bw = data.Length;
            }))
            .Returns(NTStatus.STATUS_SUCCESS);

        using var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: false, canWrite: true);
        var data = new byte[] { 10, 20, 30, 40, 50 };
        stream.Write(data, 0, data.Length);

        Assert.IsNotNull(writtenData);
        CollectionAssert.AreEqual(data, writtenData);
        Assert.AreEqual(5, stream.Position);
    }

    [TestMethod]
    public void Write_NotWritable_Throws()
    {
        using var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        Assert.ThrowsException<NotSupportedException>(() => stream.Write(new byte[5], 0, 5));
    }

    #endregion

    #region Position & Seek

    [TestMethod]
    public void Position_TracksReadsAndWrites()
    {
        var data = new byte[] { 1, 2, 3 };
        _mockStore.Setup(s => s.ReadFile(out It.Ref<byte[]>.IsAny, _handle, 0, It.IsAny<int>()))
            .Callback(new ReadFileCallbackVoid((out byte[] d, object h, long pos, int count) =>
            {
                d = data;
            }))
            .Returns(NTStatus.STATUS_SUCCESS);

        using var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        var buffer = new byte[10];
        int bytesRead = stream.Read(buffer, 0, 3);
        Assert.AreEqual(3, bytesRead);
        Assert.AreEqual(3, stream.Position);
    }

    [TestMethod]
    public void Seek_Begin_SetsPosition()
    {
        using var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        stream.Position = 100;
        var result = stream.Seek(50, SeekOrigin.Begin);
        Assert.AreEqual(50, result);
        Assert.AreEqual(50, stream.Position);
    }

    [TestMethod]
    public void Seek_Current_AddsToPosition()
    {
        using var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        stream.Position = 100;
        var result = stream.Seek(25, SeekOrigin.Current);
        Assert.AreEqual(125, result);
    }

    [TestMethod]
    public void Seek_End_Throws()
    {
        using var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        Assert.ThrowsException<NotSupportedException>(() => stream.Seek(0, SeekOrigin.End));
    }

    #endregion

    #region Dispose

    [TestMethod]
    public void Dispose_ClosesFileHandle()
    {
        var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        stream.Dispose();

        _mockStore.Verify(s => s.CloseFile(_handle), Times.Once);
    }

    [TestMethod]
    public void Dispose_DoubleDispose_ClosesOnce()
    {
        var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        stream.Dispose();
        stream.Dispose();

        _mockStore.Verify(s => s.CloseFile(_handle), Times.Once);
    }

    [TestMethod]
    public void Read_AfterDispose_ThrowsObjectDisposedException()
    {
        var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        stream.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => stream.Read(new byte[10], 0, 10));
    }

    #endregion

    #region Properties

    [TestMethod]
    public void CanSeek_ReturnsFalse()
    {
        using var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        Assert.IsFalse(stream.CanSeek);
    }

    [TestMethod]
    public void Length_ThrowsNotSupported()
    {
        using var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        Assert.ThrowsException<NotSupportedException>(() => _ = stream.Length);
    }

    [TestMethod]
    public void SetLength_ThrowsNotSupported()
    {
        using var stream = new SmbFileStream(_mockStore.Object, _handle, canRead: true, canWrite: false);
        Assert.ThrowsException<NotSupportedException>(() => stream.SetLength(100));
    }

    #endregion

    #region Delegates

    private delegate void ReadFileCallbackVoid(out byte[] data, object handle, long offset, int count);
    private delegate void WriteFileCallbackVoid(out int bytesWritten, object handle, long offset, byte[] data);

    #endregion
}
