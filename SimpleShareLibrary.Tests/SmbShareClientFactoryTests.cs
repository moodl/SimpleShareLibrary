using Moq;
using SimpleShareLibrary.Exceptions;
using SimpleShareLibrary.Providers.Smb;
using SMBLibrary;
using SMBLibrary.Client;

namespace SimpleShareLibrary.Tests;

[TestClass]
public class SmbShareClientFactoryTests
{
    #region ConnectAsync

    [TestMethod]
    public async Task ConnectAsync_SuccessfulConnection_ReturnsIShareClient()
    {
        var mockClient = new Mock<ISMBClient>();
        mockClient.Setup(c => c.Connect(It.IsAny<string>(), SMBTransportType.DirectTCPTransport)).Returns(true);
        mockClient.Setup(c => c.Login(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(NTStatus.STATUS_SUCCESS);
        mockClient.Setup(c => c.IsConnected).Returns(true);

        var factory = new SmbShareClientFactory(() => mockClient.Object);
        var options = new ConnectionOptions
        {
            Host = "server1",
            Username = "user",
            Password = "pass",
            Resilience = new ResilienceOptions { MaxRetries = 0 }
        };

        var client = await factory.ConnectAsync(options);

        Assert.IsNotNull(client);
        Assert.IsTrue(client.IsConnected);
    }

    [TestMethod]
    public async Task ConnectAsync_WithIPAddress_ConnectsViaIP()
    {
        var mockClient = new Mock<ISMBClient>();
        mockClient.Setup(c => c.Connect(It.IsAny<System.Net.IPAddress>(), SMBTransportType.DirectTCPTransport)).Returns(true);
        mockClient.Setup(c => c.Login(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(NTStatus.STATUS_SUCCESS);

        var factory = new SmbShareClientFactory(() => mockClient.Object);
        var options = new ConnectionOptions
        {
            Host = "192.168.1.100",
            Username = "user",
            Password = "pass",
            Resilience = new ResilienceOptions { MaxRetries = 0 }
        };

        var client = await factory.ConnectAsync(options);

        Assert.IsNotNull(client);
        mockClient.Verify(c => c.Connect(It.IsAny<System.Net.IPAddress>(), SMBTransportType.DirectTCPTransport), Times.Once);
    }

    [TestMethod]
    public async Task ConnectAsync_ConnectionFails_ThrowsShareConnectionException()
    {
        var mockClient = new Mock<ISMBClient>();
        mockClient.Setup(c => c.Connect(It.IsAny<string>(), SMBTransportType.DirectTCPTransport)).Returns(false);

        var factory = new SmbShareClientFactory(() => mockClient.Object);
        var options = new ConnectionOptions
        {
            Host = "badserver",
            Resilience = new ResilienceOptions { MaxRetries = 0 }
        };

        await Assert.ThrowsExceptionAsync<ShareConnectionException>(
            () => factory.ConnectAsync(options));
    }

    [TestMethod]
    public async Task ConnectAsync_LoginFails_ThrowsShareAuthenticationException()
    {
        var mockClient = new Mock<ISMBClient>();
        mockClient.Setup(c => c.Connect(It.IsAny<string>(), SMBTransportType.DirectTCPTransport)).Returns(true);
        mockClient.Setup(c => c.Login(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(NTStatus.STATUS_LOGON_FAILURE);

        var factory = new SmbShareClientFactory(() => mockClient.Object);
        var options = new ConnectionOptions
        {
            Host = "server1",
            Username = "baduser",
            Password = "badpass",
            Resilience = new ResilienceOptions { MaxRetries = 0 }
        };

        await Assert.ThrowsExceptionAsync<ShareAuthenticationException>(
            () => factory.ConnectAsync(options));

        // Should disconnect on auth failure
        mockClient.Verify(c => c.Disconnect(), Times.Once);
    }

    [TestMethod]
    public async Task ConnectAsync_NullOptions_ThrowsArgumentNullException()
    {
        var factory = new SmbShareClientFactory(() => new Mock<ISMBClient>().Object);

        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            () => factory.ConnectAsync(null!));
    }

    [TestMethod]
    public async Task ConnectAsync_EmptyHost_ThrowsArgumentException()
    {
        var factory = new SmbShareClientFactory(() => new Mock<ISMBClient>().Object);
        var options = new ConnectionOptions { Host = "" };

        await Assert.ThrowsExceptionAsync<ArgumentException>(
            () => factory.ConnectAsync(options));
    }

    [TestMethod]
    public async Task ConnectAsync_Cancelled_ThrowsOperationCanceledException()
    {
        var factory = new SmbShareClientFactory(() => new Mock<ISMBClient>().Object);
        var options = new ConnectionOptions { Host = "server1" };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => factory.ConnectAsync(options, cts.Token));
    }

    [TestMethod]
    public async Task ConnectAsync_TransientFailureThenSuccess_Retries()
    {
        int attempt = 0;
        var factory = new SmbShareClientFactory(() =>
        {
            attempt++;
            var mock = new Mock<ISMBClient>();

            if (attempt < 2)
            {
                mock.Setup(c => c.Connect(It.IsAny<string>(), SMBTransportType.DirectTCPTransport)).Returns(false);
            }
            else
            {
                mock.Setup(c => c.Connect(It.IsAny<string>(), SMBTransportType.DirectTCPTransport)).Returns(true);
                mock.Setup(c => c.Login(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(NTStatus.STATUS_SUCCESS);
                mock.Setup(c => c.IsConnected).Returns(true);
            }

            return mock.Object;
        });

        var options = new ConnectionOptions
        {
            Host = "server1",
            Username = "user",
            Password = "pass",
            Resilience = new ResilienceOptions { MaxRetries = 3, RetryDelay = TimeSpan.FromMilliseconds(1) }
        };

        var client = await factory.ConnectAsync(options);

        Assert.IsNotNull(client);
        Assert.AreEqual(2, attempt);
    }

    #endregion
}
