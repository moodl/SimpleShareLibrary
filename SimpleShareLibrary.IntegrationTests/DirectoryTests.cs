namespace SimpleShareLibrary.IntegrationTests;

/// <summary>
/// Integration tests for directory operations against a real SMB share.
/// </summary>
[TestClass]
public class DirectoryTests
{
    #region CreateDirectory

    [TestMethod]
    public async Task CreateDirectoryAsync_CreatesDirectory()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var dirName = $"test-dir-{Guid.NewGuid()}";

        try
        {
            // Act
            await share.CreateDirectoryAsync(dirName);

            // Assert
            var exists = await share.ExistsAsync(dirName);
            Assert.IsTrue(exists);
        }
        finally
        {
            await CleanupDirectoryAsync(share, dirName);
        }
    }

    [TestMethod]
    public async Task CreateDirectoryAsync_WithCreateParents_CreatesNestedDirectories()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var baseName = $"test-nested-{Guid.NewGuid()}";
        var nestedPath = $"{baseName}/level1/level2";

        try
        {
            // Act
            await share.CreateDirectoryAsync(nestedPath, createParents: true);

            // Assert
            Assert.IsTrue(await share.ExistsAsync(nestedPath));
            Assert.IsTrue(await share.ExistsAsync($"{baseName}/level1"));
            Assert.IsTrue(await share.ExistsAsync(baseName));
        }
        finally
        {
            await CleanupDirectoryAsync(share, baseName);
        }
    }

    #endregion

    #region EnsureDirectoryExists

    [TestMethod]
    public async Task EnsureDirectoryExistsAsync_CreatesIfNotExists()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var dirName = $"test-ensure-{Guid.NewGuid()}";

        try
        {
            // Act
            await share.EnsureDirectoryExistsAsync(dirName);

            // Assert
            Assert.IsTrue(await share.ExistsAsync(dirName));
        }
        finally
        {
            await CleanupDirectoryAsync(share, dirName);
        }
    }

    [TestMethod]
    public async Task EnsureDirectoryExistsAsync_DoesNotThrowWhenAlreadyExists()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var dirName = $"test-ensure-exists-{Guid.NewGuid()}";

        try
        {
            await share.CreateDirectoryAsync(dirName);

            // Act — should not throw
            await share.EnsureDirectoryExistsAsync(dirName);

            // Assert
            Assert.IsTrue(await share.ExistsAsync(dirName));
        }
        finally
        {
            await CleanupDirectoryAsync(share, dirName);
        }
    }

    #endregion

    #region Exists / GetInfo

    [TestMethod]
    public async Task ExistsAsync_ReturnsFalseForNonExistent()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;

        // Act
        var exists = await share.ExistsAsync($"nonexistent-{Guid.NewGuid()}");

        // Assert
        Assert.IsFalse(exists);
    }

    [TestMethod]
    public async Task GetInfoAsync_ReturnsCorrectMetadataForFile()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-info-{Guid.NewGuid()}.txt";
        var content = "metadata test content";

        try
        {
            await share.WriteAllTextAsync(path, content);

            // Act
            var info = await share.GetInfoAsync(path);

            // Assert
            Assert.AreEqual(path, info.Name);
            Assert.IsFalse(info.IsDirectory);
            Assert.IsTrue(info.Size > 0);
        }
        finally
        {
            await CleanupFileAsync(share, path);
        }
    }

    [TestMethod]
    public async Task GetInfoAsync_ReturnsIsDirectoryTrue_ForDirectory()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var dirName = $"test-info-dir-{Guid.NewGuid()}";

        try
        {
            await share.CreateDirectoryAsync(dirName);

            // Act
            var info = await share.GetInfoAsync(dirName);

            // Assert
            Assert.IsTrue(info.IsDirectory);
        }
        finally
        {
            await CleanupDirectoryAsync(share, dirName);
        }
    }

    #endregion

    #region List / ListRecursive

    [TestMethod]
    public async Task ListAsync_ReturnsFilesInDirectory()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var dirName = $"test-list-{Guid.NewGuid()}";

        try
        {
            await share.CreateDirectoryAsync(dirName);
            await share.WriteAllTextAsync($"{dirName}/file1.txt", "content1");
            await share.WriteAllTextAsync($"{dirName}/file2.txt", "content2");

            // Act
            var entries = await share.ListAsync(dirName);

            // Assert
            Assert.AreEqual(2, entries.Count);
            var names = entries.Select(e => e.Name).OrderBy(n => n).ToList();
            CollectionAssert.AreEqual(new[] { "file1.txt", "file2.txt" }, names);
        }
        finally
        {
            await CleanupDirectoryAsync(share, dirName);
        }
    }

    [TestMethod]
    public async Task ListAsync_WithPattern_FiltersResults()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var dirName = $"test-list-pattern-{Guid.NewGuid()}";

        try
        {
            await share.CreateDirectoryAsync(dirName);
            await share.WriteAllTextAsync($"{dirName}/data.txt", "text");
            await share.WriteAllTextAsync($"{dirName}/data.csv", "csv");
            await share.WriteAllTextAsync($"{dirName}/image.png", "png");

            // Act
            var txtFiles = await share.ListAsync(dirName, "*.txt");

            // Assert
            Assert.AreEqual(1, txtFiles.Count);
            Assert.AreEqual("data.txt", txtFiles[0].Name);
        }
        finally
        {
            await CleanupDirectoryAsync(share, dirName);
        }
    }

    [TestMethod]
    public async Task ListRecursiveAsync_ReturnsNestedEntries()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var baseName = $"test-recursive-{Guid.NewGuid()}";

        try
        {
            await share.CreateDirectoryAsync($"{baseName}/sub1", createParents: true);
            await share.CreateDirectoryAsync($"{baseName}/sub2", createParents: true);
            await share.WriteAllTextAsync($"{baseName}/root.txt", "root");
            await share.WriteAllTextAsync($"{baseName}/sub1/nested.txt", "nested");
            await share.WriteAllTextAsync($"{baseName}/sub2/deep.txt", "deep");

            // Act
            var entries = await share.ListRecursiveAsync(baseName);

            // Assert — should contain dirs and files
            var fileNames = entries.Where(e => !e.IsDirectory).Select(e => e.Name).OrderBy(n => n).ToList();
            CollectionAssert.Contains(fileNames, "root.txt");
            CollectionAssert.Contains(fileNames, "nested.txt");
            CollectionAssert.Contains(fileNames, "deep.txt");
        }
        finally
        {
            await CleanupDirectoryAsync(share, baseName);
        }
    }

    #endregion

    #region DeleteDirectory

    [TestMethod]
    public async Task DeleteDirectoryAsync_EmptyDirectory_Succeeds()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var dirName = $"test-delete-empty-{Guid.NewGuid()}";

        await share.CreateDirectoryAsync(dirName);

        // Act
        await share.DeleteDirectoryAsync(dirName);

        // Assert
        Assert.IsFalse(await share.ExistsAsync(dirName));
    }

    [TestMethod]
    public async Task DeleteDirectoryAsync_Recursive_DeletesAllContents()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var dirName = $"test-delete-recursive-{Guid.NewGuid()}";

        await share.CreateDirectoryAsync($"{dirName}/sub", createParents: true);
        await share.WriteAllTextAsync($"{dirName}/file.txt", "content");
        await share.WriteAllTextAsync($"{dirName}/sub/nested.txt", "nested");

        // Act
        await share.DeleteDirectoryAsync(dirName, recursive: true);

        // Assert
        Assert.IsFalse(await share.ExistsAsync(dirName));
    }

    #endregion

    #region DeleteAll

    [TestMethod]
    public async Task DeleteAllAsync_ClearsDirectoryButKeepsIt()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var dirName = $"test-deleteall-{Guid.NewGuid()}";

        await share.CreateDirectoryAsync(dirName);
        await share.WriteAllTextAsync($"{dirName}/a.txt", "a");
        await share.WriteAllTextAsync($"{dirName}/b.txt", "b");

        // Act
        await share.DeleteAllAsync(dirName);

        // Assert — directory still exists, but is empty
        Assert.IsTrue(await share.ExistsAsync(dirName));
        var entries = await share.ListAsync(dirName);
        Assert.AreEqual(0, entries.Count);

        // Cleanup
        await CleanupDirectoryAsync(share, dirName);
    }

    #endregion

    #region Helpers

    private static async Task CleanupDirectoryAsync(IShare share, string path)
    {
        try { await share.DeleteDirectoryAsync(path, recursive: true); }
        catch { /* best effort */ }
    }

    private static async Task CleanupFileAsync(IShare share, string path)
    {
        try { await share.DeleteFileAsync(path); }
        catch { /* best effort */ }
    }

    #endregion
}
