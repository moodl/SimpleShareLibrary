namespace SimpleShareLibrary.IntegrationTests;

/// <summary>
/// Integration tests for connecting to an SMB server and listing shares.
/// </summary>
[TestClass]
public class ConnectionTests
{
    #region Connect Tests

    [TestMethod]
    public async Task ConnectAsync_WithValidCredentials_ReturnsConnectedClient()
    {
        // Arrange & Act
        using var client = await SmbDockerFixture.CreateConnectedClientAsync();

        // Assert
        Assert.IsTrue(client.IsConnected);
    }

    [TestMethod]
    public void Connect_WithValidCredentials_ReturnsConnectedClient()
    {
        // Arrange
        var factory = ShareClientFactory.CreateSmb();
        var options = SmbDockerFixture.CreateConnectionOptions();

        // Act
        using var client = factory.Connect(options);

        // Assert
        Assert.IsTrue(client.IsConnected);
    }

    [TestMethod]
    public async Task ConnectAsync_WithInvalidPassword_ThrowsShareAuthenticationException()
    {
        // Arrange
        var factory = ShareClientFactory.CreateSmb();
        var options = SmbDockerFixture.CreateConnectionOptions();
        options.Password = "WrongPassword";

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ShareAuthenticationException>(
            () => factory.ConnectAsync(options));
    }

    [TestMethod]
    public async Task ConnectAsync_WithInvalidHost_ThrowsShareConnectionException()
    {
        // Arrange
        var factory = ShareClientFactory.CreateSmb();
        var options = SmbDockerFixture.CreateConnectionOptions();
        options.Host = "192.0.2.1"; // Non-routable TEST-NET address
        options.Resilience = new ResilienceOptions
        {
            MaxRetries = 0,
            OperationTimeout = TimeSpan.FromSeconds(5)
        };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ShareConnectionException>(
            () => factory.ConnectAsync(options));
    }

    #endregion

    #region ListShares Tests

    [TestMethod]
    public async Task ListSharesAsync_ReturnsConfiguredShares()
    {
        // Arrange
        using var client = await SmbDockerFixture.CreateConnectedClientAsync();

        // Act
        var shares = await client.ListSharesAsync();

        // Assert
        Assert.IsTrue(shares.Count >= 2, $"Expected at least 2 shares, got {shares.Count}");
        CollectionAssert.Contains(shares.ToList(), SmbDockerFixture.ShareName);
        CollectionAssert.Contains(shares.ToList(), SmbDockerFixture.ReadOnlyShareName);
    }

    [TestMethod]
    public void ListShares_ReturnsConfiguredShares()
    {
        // Arrange
        using var client = SmbDockerFixture.CreateConnectedClientAsync().GetAwaiter().GetResult();

        // Act
        var shares = client.ListShares();

        // Assert
        Assert.IsTrue(shares.Count >= 2);
        CollectionAssert.Contains(shares.ToList(), SmbDockerFixture.ShareName);
    }

    #endregion

    #region OpenShare Tests

    [TestMethod]
    public async Task OpenShareAsync_WithValidShareName_ReturnsShare()
    {
        // Arrange
        using var client = await SmbDockerFixture.CreateConnectedClientAsync();

        // Act
        using var share = await client.OpenShareAsync(SmbDockerFixture.ShareName);

        // Assert
        Assert.IsNotNull(share);
    }

    [TestMethod]
    public async Task OpenShareAsync_WithInvalidShareName_ThrowsException()
    {
        // Arrange
        using var client = await SmbDockerFixture.CreateConnectedClientAsync();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ShareException>(
            () => client.OpenShareAsync("nonexistent_share"));
    }

    #endregion

    #region Dispose Tests

    [TestMethod]
    public async Task Dispose_SetsIsConnectedToFalse()
    {
        // Arrange
        var client = await SmbDockerFixture.CreateConnectedClientAsync();
        Assert.IsTrue(client.IsConnected);

        // Act
        client.Dispose();

        // Assert
        Assert.IsFalse(client.IsConnected);
    }

    #endregion
}
