using Moq;
using SimpleShareLibrary.Exceptions;
using SimpleShareLibrary.Providers.Smb;
using SMBLibrary;
using SMBLibrary.Client;

namespace SimpleShareLibrary.Tests;

[TestClass]
public class SmbShareClientTests
{
    private Mock<ISMBClient> _mockClient = null!;
    private SmbShareClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockClient = new Mock<ISMBClient>();
        _mockClient.Setup(c => c.IsConnected).Returns(true);
        _client = new SmbShareClient(_mockClient.Object);
    }

    // ── IsConnected ──────────────────────────────────────

    [TestMethod]
    public void IsConnected_DelegatesToClient()
    {
        Assert.IsTrue(_client.IsConnected);

        _mockClient.Setup(c => c.IsConnected).Returns(false);
        Assert.IsFalse(_client.IsConnected);
    }

    // ── ListSharesAsync ──────────────────────────────────

    [TestMethod]
    public async Task ListSharesAsync_ReturnsShareNames()
    {
        var status = NTStatus.STATUS_SUCCESS;
        var shares = new List<string> { "Share1", "Share2", "Documents" };
        _mockClient.Setup(c => c.ListShares(out status)).Returns(shares);

        var result = await _client.ListSharesAsync();

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("Share1", result[0]);
        Assert.AreEqual("Share2", result[1]);
        Assert.AreEqual("Documents", result[2]);
    }

    [TestMethod]
    public async Task ListSharesAsync_FailureStatus_Throws()
    {
        var status = NTStatus.STATUS_ACCESS_DENIED;
        _mockClient.Setup(c => c.ListShares(out status)).Returns(new List<string>());

        await Assert.ThrowsExceptionAsync<ShareAccessDeniedException>(
            () => _client.ListSharesAsync());
    }

    // ── OpenShareAsync ───────────────────────────────────

    [TestMethod]
    public async Task OpenShareAsync_Success_ReturnsIShare()
    {
        var status = NTStatus.STATUS_SUCCESS;
        var mockFileStore = new Mock<ISMBFileStore>();
        _mockClient.Setup(c => c.TreeConnect("MyShare", out status))
            .Returns(mockFileStore.Object);

        var share = await _client.OpenShareAsync("MyShare");

        Assert.IsNotNull(share);
        Assert.IsInstanceOfType(share, typeof(IShare));
    }

    [TestMethod]
    public async Task OpenShareAsync_NullShareName_ThrowsArgumentException()
    {
        await Assert.ThrowsExceptionAsync<ArgumentException>(
            () => _client.OpenShareAsync(null!));
    }

    [TestMethod]
    public async Task OpenShareAsync_EmptyShareName_ThrowsArgumentException()
    {
        await Assert.ThrowsExceptionAsync<ArgumentException>(
            () => _client.OpenShareAsync(""));
    }

    [TestMethod]
    public async Task OpenShareAsync_FailureStatus_Throws()
    {
        var status = NTStatus.STATUS_OBJECT_NAME_NOT_FOUND;
        _mockClient.Setup(c => c.TreeConnect("BadShare", out status))
            .Returns((ISMBFileStore)null!);

        await Assert.ThrowsExceptionAsync<ShareFileNotFoundException>(
            () => _client.OpenShareAsync("BadShare"));
    }

    // ── Dispose ──────────────────────────────────────────

    [TestMethod]
    public void Dispose_CallsLogoffAndDisconnect()
    {
        _client.Dispose();

        _mockClient.Verify(c => c.Logoff(), Times.Once);
        _mockClient.Verify(c => c.Disconnect(), Times.Once);
    }

    [TestMethod]
    public void Dispose_NotConnected_SkipsLogoffAndDisconnect()
    {
        _mockClient.Setup(c => c.IsConnected).Returns(false);
        _client.Dispose();

        _mockClient.Verify(c => c.Logoff(), Times.Never);
        _mockClient.Verify(c => c.Disconnect(), Times.Never);
    }

    [TestMethod]
    public void Dispose_DoubleDispose_OnlyDisconnectsOnce()
    {
        _client.Dispose();
        _client.Dispose();

        _mockClient.Verify(c => c.Logoff(), Times.Once);
        _mockClient.Verify(c => c.Disconnect(), Times.Once);
    }

    [TestMethod]
    public async Task ListSharesAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        _client.Dispose();

        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
            () => _client.ListSharesAsync());
    }

    [TestMethod]
    public async Task OpenShareAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        _client.Dispose();

        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
            () => _client.OpenShareAsync("test"));
    }

    // ── CancellationToken ────────────────────────────────

    [TestMethod]
    public async Task ListSharesAsync_Cancelled_ThrowsTaskCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExceptionAsync<TaskCanceledException>(
            () => _client.ListSharesAsync(cts.Token));
    }
}
