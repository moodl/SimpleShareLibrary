using System.Text;

namespace SimpleShareLibrary.IntegrationTests;

/// <summary>
/// Integration tests for file read and write operations against a real SMB share.
/// </summary>
[TestClass]
public class FileReadWriteTests
{
    #region WriteAllText / ReadAllText

    [TestMethod]
    public async Task WriteAllTextAsync_ThenReadAllTextAsync_RoundTrips()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-write-read-{Guid.NewGuid()}.txt";
        var content = "Hello, SimpleShareLibrary!";

        try
        {
            // Act
            await share.WriteAllTextAsync(path, content);
            var result = await share.ReadAllTextAsync(path);

            // Assert
            Assert.AreEqual(content, result);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    [TestMethod]
    public void WriteAllText_ThenReadAllText_RoundTrips()
    {
        // Arrange
        var (client, share) = SmbDockerFixture.CreateConnectedShareAsync().GetAwaiter().GetResult();
        using var _ = client;
        using var __ = share;
        var path = $"test-sync-{Guid.NewGuid()}.txt";
        var content = "Sync round-trip test";

        try
        {
            // Act
            share.WriteAllText(path, content);
            var result = share.ReadAllText(path);

            // Assert
            Assert.AreEqual(content, result);
        }
        finally
        {
            CleanupFile(share, path);
        }
    }

    [TestMethod]
    public async Task WriteAllTextAsync_WithUtf8Encoding_PreservesUnicode()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-unicode-{Guid.NewGuid()}.txt";
        var content = "Héllo Wörld! 日本語テスト 🎉";

        try
        {
            // Act
            await share.WriteAllTextAsync(path, content, Encoding.UTF8);
            var result = await share.ReadAllTextAsync(path, Encoding.UTF8);

            // Assert
            Assert.AreEqual(content, result);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    [TestMethod]
    public async Task WriteAllTextAsync_Overwrite_ReplacesContent()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-overwrite-{Guid.NewGuid()}.txt";

        try
        {
            await share.WriteAllTextAsync(path, "original");

            // Act
            await share.WriteAllTextAsync(path, "replaced", overwrite: true);
            var result = await share.ReadAllTextAsync(path);

            // Assert
            Assert.AreEqual("replaced", result);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    [TestMethod]
    public async Task WriteAllTextAsync_NoOverwrite_ThrowsWhenFileExists()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-no-overwrite-{Guid.NewGuid()}.txt";

        try
        {
            await share.WriteAllTextAsync(path, "original");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ShareAlreadyExistsException>(
                () => share.WriteAllTextAsync(path, "should fail", overwrite: false));
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    #endregion

    #region WriteAllBytes / ReadAllBytes

    [TestMethod]
    public async Task WriteAllBytesAsync_ThenReadAllBytesAsync_RoundTrips()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-bytes-{Guid.NewGuid()}.bin";
        var data = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD };

        try
        {
            // Act
            await share.WriteAllBytesAsync(path, data);
            var result = await share.ReadAllBytesAsync(path);

            // Assert
            CollectionAssert.AreEqual(data, result);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    [TestMethod]
    public async Task WriteAllBytesAsync_LargeFile_RoundTrips()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-large-{Guid.NewGuid()}.bin";
        var data = new byte[1024 * 1024]; // 1 MB
        new Random(42).NextBytes(data);

        try
        {
            // Act
            await share.WriteAllBytesAsync(path, data);
            var result = await share.ReadAllBytesAsync(path);

            // Assert
            CollectionAssert.AreEqual(data, result);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    #endregion

    #region OpenRead / OpenWrite (Stream)

    [TestMethod]
    public async Task OpenWriteAsync_ThenOpenReadAsync_StreamRoundTrips()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-stream-{Guid.NewGuid()}.bin";
        var data = Encoding.UTF8.GetBytes("Stream test content");

        try
        {
            // Act - Write
            using (var writeStream = await share.OpenWriteAsync(path))
            {
                await writeStream.WriteAsync(data, 0, data.Length);
            }

            // Act - Read
            byte[] result;
            using (var readStream = await share.OpenReadAsync(path))
            using (var ms = new MemoryStream())
            {
                await readStream.CopyToAsync(ms);
                result = ms.ToArray();
            }

            // Assert
            CollectionAssert.AreEqual(data, result);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    #endregion

    #region ReadAllText Non-Existent

    [TestMethod]
    public async Task ReadAllTextAsync_NonExistentFile_ThrowsShareFileNotFoundException()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ShareFileNotFoundException>(
            () => share.ReadAllTextAsync("does-not-exist.txt"));
    }

    #endregion

    #region Helpers

    private static async Task CleanupFileAsync(IShare share, string path)
    {
        try { await share.DeleteFileAsync(path); }
        catch { /* best effort */ }
    }

    private static void CleanupFile(IShare share, string path)
    {
        try { share.DeleteFile(path); }
        catch { /* best effort */ }
    }

    #endregion
}
