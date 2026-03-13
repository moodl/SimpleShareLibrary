using System.Text;

namespace SimpleShareLibrary.IntegrationTests;

/// <summary>
/// Integration tests for edge cases, error scenarios, and path normalization
/// against a real SMB share.
/// </summary>
[TestClass]
public class EdgeCaseTests
{
    #region Path Normalization

    [TestMethod]
    public async Task WriteAndRead_WithForwardSlashPath_Works()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var dirName = $"test-fwdslash-{Guid.NewGuid()}";

        try
        {
            await share.CreateDirectoryAsync(dirName);

            // Act — use forward slashes
            await share.WriteAllTextAsync($"{dirName}/file.txt", "forward slash");
            var result = await share.ReadAllTextAsync($"{dirName}/file.txt");

            // Assert
            Assert.AreEqual("forward slash", result);
        }
        finally
        {
            await CleanupDirectoryAsync(share, dirName);
        }
    }

    [TestMethod]
    public async Task WriteAndRead_WithBackslashPath_Works()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var dirName = $"test-backslash-{Guid.NewGuid()}";

        try
        {
            await share.CreateDirectoryAsync(dirName);

            // Act — use backslashes
            await share.WriteAllTextAsync($"{dirName}\\file.txt", "backslash");
            var result = await share.ReadAllTextAsync($"{dirName}\\file.txt");

            // Assert
            Assert.AreEqual("backslash", result);
        }
        finally
        {
            await CleanupDirectoryAsync(share, dirName);
        }
    }

    [TestMethod]
    public async Task WriteWithForwardSlash_ReadWithBackslash_Works()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var dirName = $"test-mixpath-{Guid.NewGuid()}";

        try
        {
            await share.CreateDirectoryAsync(dirName);

            // Act — write with forward slash, read with backslash
            await share.WriteAllTextAsync($"{dirName}/mixed.txt", "mixed paths");
            var result = await share.ReadAllTextAsync($"{dirName}\\mixed.txt");

            // Assert
            Assert.AreEqual("mixed paths", result);
        }
        finally
        {
            await CleanupDirectoryAsync(share, dirName);
        }
    }

    #endregion

    #region Empty Files

    [TestMethod]
    public async Task WriteAllTextAsync_EmptyString_CreatesEmptyFile()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-empty-{Guid.NewGuid()}.txt";

        try
        {
            // Act
            await share.WriteAllTextAsync(path, string.Empty);
            var result = await share.ReadAllTextAsync(path);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    [TestMethod]
    public async Task WriteAllBytesAsync_EmptyArray_CreatesEmptyFile()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-empty-bytes-{Guid.NewGuid()}.bin";

        try
        {
            // Act
            await share.WriteAllBytesAsync(path, Array.Empty<byte>());
            var result = await share.ReadAllBytesAsync(path);

            // Assert
            Assert.AreEqual(0, result.Length);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    #endregion

    #region Special Characters in File Names

    [TestMethod]
    public async Task WriteAllTextAsync_WithSpacesInName_Works()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test file with spaces {Guid.NewGuid()}.txt";

        try
        {
            // Act
            await share.WriteAllTextAsync(path, "spaces work");
            var result = await share.ReadAllTextAsync(path);

            // Assert
            Assert.AreEqual("spaces work", result);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    [TestMethod]
    public async Task WriteAllTextAsync_WithHyphenAndUnderscore_Works()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-file_name-{Guid.NewGuid()}.txt";

        try
        {
            // Act
            await share.WriteAllTextAsync(path, "special chars");
            var result = await share.ReadAllTextAsync(path);

            // Assert
            Assert.AreEqual("special chars", result);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    #endregion

    #region GetInfo on Non-Existent

    [TestMethod]
    public async Task GetInfoAsync_NonExistentPath_ThrowsException()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ShareException>(
            () => share.GetInfoAsync($"nonexistent-{Guid.NewGuid()}"));
    }

    #endregion

    #region Operations on Root Path

    [TestMethod]
    public async Task ListAsync_RootPath_ReturnsEntries()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-root-list-{Guid.NewGuid()}.txt";

        try
        {
            await share.WriteAllTextAsync(path, "root file");

            // Act — list root
            var entries = await share.ListAsync("");

            // Assert
            Assert.IsTrue(entries.Count > 0);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    #endregion

    #region Multiple Sequential Operations

    [TestMethod]
    public async Task MultipleSequentialOperations_AllSucceed()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var baseName = $"test-multi-{Guid.NewGuid()}";

        try
        {
            // Create directory structure
            await share.CreateDirectoryAsync($"{baseName}/data", createParents: true);

            // Write several files
            for (int i = 0; i < 5; i++)
            {
                await share.WriteAllTextAsync($"{baseName}/data/file{i}.txt", $"content {i}");
            }

            // Verify all exist
            var entries = await share.ListAsync($"{baseName}/data");
            Assert.AreEqual(5, entries.Count);

            // Read them back
            for (int i = 0; i < 5; i++)
            {
                var content = await share.ReadAllTextAsync($"{baseName}/data/file{i}.txt");
                Assert.AreEqual($"content {i}", content);
            }

            // Copy directory
            await share.CopyDirectoryAsync($"{baseName}/data", $"{baseName}/backup");
            var backupEntries = await share.ListAsync($"{baseName}/backup");
            Assert.AreEqual(5, backupEntries.Count);

            // Delete originals
            await share.DeleteDirectoryAsync($"{baseName}/data", recursive: true);
            Assert.IsFalse(await share.ExistsAsync($"{baseName}/data"));

            // Backup still exists
            Assert.IsTrue(await share.ExistsAsync($"{baseName}/backup"));
        }
        finally
        {
            await CleanupDirectoryAsync(share, baseName);
        }
    }

    #endregion

    #region Encoding Variants

    [TestMethod]
    public async Task WriteAllTextAsync_WithAsciiEncoding_RoundTrips()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-ascii-{Guid.NewGuid()}.txt";

        try
        {
            var content = "Plain ASCII content 123!@#";

            // Act
            await share.WriteAllTextAsync(path, content, Encoding.ASCII);
            var result = await share.ReadAllTextAsync(path, Encoding.ASCII);

            // Assert
            Assert.AreEqual(content, result);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    #endregion

    #region Helpers

    private static async Task CleanupFileAsync(IShare share, string path)
    {
        try { await share.DeleteFileAsync(path); }
        catch { /* best effort */ }
    }

    private static async Task CleanupDirectoryAsync(IShare share, string path)
    {
        try { await share.DeleteDirectoryAsync(path, recursive: true); }
        catch { /* best effort */ }
    }

    #endregion
}
