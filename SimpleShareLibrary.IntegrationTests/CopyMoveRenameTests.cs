namespace SimpleShareLibrary.IntegrationTests;

/// <summary>
/// Integration tests for copy, move, and rename operations against a real SMB share.
/// </summary>
[TestClass]
public class CopyMoveRenameTests
{
    #region CopyFile

    [TestMethod]
    public async Task CopyFileAsync_CopiesFileToNewLocation()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var src = $"test-copy-src-{Guid.NewGuid()}.txt";
        var dst = $"test-copy-dst-{Guid.NewGuid()}.txt";

        try
        {
            await share.WriteAllTextAsync(src, "copy me");

            // Act
            await share.CopyFileAsync(src, dst);

            // Assert — both files exist with same content
            Assert.IsTrue(await share.ExistsAsync(src));
            Assert.IsTrue(await share.ExistsAsync(dst));
            Assert.AreEqual("copy me", await share.ReadAllTextAsync(dst));
        }
        finally
        {
            await CleanupFileAsync(share, src);
            await CleanupFileAsync(share, dst);
        }
    }

    [TestMethod]
    public async Task CopyFileAsync_WithOverwriteFalse_ThrowsWhenDestExists()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var src = $"test-copy-noow-src-{Guid.NewGuid()}.txt";
        var dst = $"test-copy-noow-dst-{Guid.NewGuid()}.txt";

        try
        {
            await share.WriteAllTextAsync(src, "source");
            await share.WriteAllTextAsync(dst, "existing");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ShareAlreadyExistsException>(
                () => share.CopyFileAsync(src, dst, new CopyOptions { Overwrite = false }));
        }
        finally
        {
            await CleanupFileAsync(share, src);
            await CleanupFileAsync(share, dst);
        }
    }

    [TestMethod]
    public async Task CopyFileAsync_WithOverwriteTrue_ReplacesDestination()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var src = $"test-copy-ow-src-{Guid.NewGuid()}.txt";
        var dst = $"test-copy-ow-dst-{Guid.NewGuid()}.txt";

        try
        {
            await share.WriteAllTextAsync(src, "new content");
            await share.WriteAllTextAsync(dst, "old content");

            // Act
            await share.CopyFileAsync(src, dst, new CopyOptions { Overwrite = true });

            // Assert
            Assert.AreEqual("new content", await share.ReadAllTextAsync(dst));
        }
        finally
        {
            await CleanupFileAsync(share, src);
            await CleanupFileAsync(share, dst);
        }
    }

    #endregion

    #region CopyDirectory

    [TestMethod]
    public async Task CopyDirectoryAsync_CopiesDirectoryRecursively()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var srcDir = $"test-copydir-src-{Guid.NewGuid()}";
        var dstDir = $"test-copydir-dst-{Guid.NewGuid()}";

        try
        {
            await share.CreateDirectoryAsync($"{srcDir}/sub", createParents: true);
            await share.WriteAllTextAsync($"{srcDir}/root.txt", "root");
            await share.WriteAllTextAsync($"{srcDir}/sub/nested.txt", "nested");

            // Act
            await share.CopyDirectoryAsync(srcDir, dstDir);

            // Assert
            Assert.IsTrue(await share.ExistsAsync(srcDir));
            Assert.IsTrue(await share.ExistsAsync(dstDir));
            Assert.AreEqual("root", await share.ReadAllTextAsync($"{dstDir}/root.txt"));
            Assert.AreEqual("nested", await share.ReadAllTextAsync($"{dstDir}/sub/nested.txt"));
        }
        finally
        {
            await CleanupDirectoryAsync(share, srcDir);
            await CleanupDirectoryAsync(share, dstDir);
        }
    }

    #endregion

    #region MoveFile

    [TestMethod]
    public async Task MoveFileAsync_SafeMode_MovesFileViaCopyThenDelete()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var src = $"test-move-safe-src-{Guid.NewGuid()}.txt";
        var dst = $"test-move-safe-dst-{Guid.NewGuid()}.txt";

        try
        {
            await share.WriteAllTextAsync(src, "move me safely");

            // Act
            await share.MoveFileAsync(src, dst, new MoveOptions { Safe = true });

            // Assert
            Assert.IsFalse(await share.ExistsAsync(src));
            Assert.IsTrue(await share.ExistsAsync(dst));
            Assert.AreEqual("move me safely", await share.ReadAllTextAsync(dst));
        }
        finally
        {
            await CleanupFileAsync(share, src);
            await CleanupFileAsync(share, dst);
        }
    }

    [TestMethod]
    public async Task MoveFileAsync_UnsafeMode_MovesFileDirectly()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var src = $"test-move-unsafe-src-{Guid.NewGuid()}.txt";
        var dst = $"test-move-unsafe-dst-{Guid.NewGuid()}.txt";

        try
        {
            await share.WriteAllTextAsync(src, "move me directly");

            // Act
            await share.MoveFileAsync(src, dst, new MoveOptions { Safe = false });

            // Assert
            Assert.IsFalse(await share.ExistsAsync(src));
            Assert.IsTrue(await share.ExistsAsync(dst));
            Assert.AreEqual("move me directly", await share.ReadAllTextAsync(dst));
        }
        finally
        {
            await CleanupFileAsync(share, src);
            await CleanupFileAsync(share, dst);
        }
    }

    [TestMethod]
    public async Task MoveFileAsync_DefaultOptions_UsesSafeMode()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var src = $"test-move-default-src-{Guid.NewGuid()}.txt";
        var dst = $"test-move-default-dst-{Guid.NewGuid()}.txt";

        try
        {
            await share.WriteAllTextAsync(src, "default move");

            // Act — null options should default to safe mode
            await share.MoveFileAsync(src, dst);

            // Assert
            Assert.IsFalse(await share.ExistsAsync(src));
            Assert.IsTrue(await share.ExistsAsync(dst));
            Assert.AreEqual("default move", await share.ReadAllTextAsync(dst));
        }
        finally
        {
            await CleanupFileAsync(share, src);
            await CleanupFileAsync(share, dst);
        }
    }

    #endregion

    #region MoveDirectory

    [TestMethod]
    public async Task MoveDirectoryAsync_MovesEntireDirectoryTree()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var srcDir = $"test-movedir-src-{Guid.NewGuid()}";
        var dstDir = $"test-movedir-dst-{Guid.NewGuid()}";

        try
        {
            await share.CreateDirectoryAsync($"{srcDir}/sub", createParents: true);
            await share.WriteAllTextAsync($"{srcDir}/file.txt", "content");
            await share.WriteAllTextAsync($"{srcDir}/sub/nested.txt", "nested");

            // Act
            await share.MoveDirectoryAsync(srcDir, dstDir);

            // Assert
            Assert.IsFalse(await share.ExistsAsync(srcDir));
            Assert.IsTrue(await share.ExistsAsync(dstDir));
            Assert.AreEqual("content", await share.ReadAllTextAsync($"{dstDir}/file.txt"));
            Assert.AreEqual("nested", await share.ReadAllTextAsync($"{dstDir}/sub/nested.txt"));
        }
        finally
        {
            await CleanupDirectoryAsync(share, srcDir);
            await CleanupDirectoryAsync(share, dstDir);
        }
    }

    #endregion

    #region Rename

    [TestMethod]
    public async Task RenameAsync_RenamesFile()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var id = Guid.NewGuid();
        var dirName = $"test-rename-{id}";
        var originalPath = $"{dirName}/original.txt";
        var newName = "renamed.txt";

        try
        {
            await share.CreateDirectoryAsync(dirName);
            await share.WriteAllTextAsync(originalPath, "rename me");

            // Act
            await share.RenameAsync(originalPath, newName);

            // Assert
            Assert.IsFalse(await share.ExistsAsync(originalPath));
            Assert.IsTrue(await share.ExistsAsync($"{dirName}/{newName}"));
            Assert.AreEqual("rename me", await share.ReadAllTextAsync($"{dirName}/{newName}"));
        }
        finally
        {
            await CleanupDirectoryAsync(share, dirName);
        }
    }

    [TestMethod]
    public async Task RenameAsync_RenamesDirectory()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var id = Guid.NewGuid();
        var parentDir = $"test-renamedir-{id}";
        var originalPath = $"{parentDir}/oldname";

        try
        {
            await share.CreateDirectoryAsync(originalPath, createParents: true);
            await share.WriteAllTextAsync($"{originalPath}/file.txt", "inside");

            // Act
            await share.RenameAsync(originalPath, "newname");

            // Assert
            Assert.IsFalse(await share.ExistsAsync(originalPath));
            Assert.IsTrue(await share.ExistsAsync($"{parentDir}/newname"));
            Assert.AreEqual("inside", await share.ReadAllTextAsync($"{parentDir}/newname/file.txt"));
        }
        finally
        {
            await CleanupDirectoryAsync(share, parentDir);
        }
    }

    #endregion

    #region DeleteFile

    [TestMethod]
    public async Task DeleteFileAsync_DeletesExistingFile()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;
        var path = $"test-delete-{Guid.NewGuid()}.txt";

        await share.WriteAllTextAsync(path, "delete me");

        // Act
        await share.DeleteFileAsync(path);

        // Assert
        Assert.IsFalse(await share.ExistsAsync(path));
    }

    [TestMethod]
    public async Task DeleteFileAsync_NonExistentFile_ThrowsShareFileNotFoundException()
    {
        // Arrange
        var (client, share) = await SmbDockerFixture.CreateConnectedShareAsync();
        using var _ = client;
        using var __ = share;

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ShareFileNotFoundException>(
            () => share.DeleteFileAsync($"nonexistent-{Guid.NewGuid()}.txt"));
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
