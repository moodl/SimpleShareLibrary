using SimpleShareLibrary.Providers.Smb;

namespace SimpleShareLibrary.Tests;

[TestClass]
public class PathHelperTests
{
    // ── Normalize ────────────────────────────────────────

    [TestMethod]
    public void Normalize_ForwardSlashes_ReplacedWithBackslashes()
    {
        Assert.AreEqual(@"folder\subfolder\file.txt", PathHelper.Normalize("folder/subfolder/file.txt"));
    }

    [TestMethod]
    public void Normalize_MixedSlashes_NormalizedToBackslashes()
    {
        Assert.AreEqual(@"a\b\c", PathHelper.Normalize(@"a/b\c"));
    }

    [TestMethod]
    public void Normalize_LeadingAndTrailingSlashes_Trimmed()
    {
        Assert.AreEqual(@"folder\file", PathHelper.Normalize(@"\folder\file\"));
        Assert.AreEqual(@"folder\file", PathHelper.Normalize(@"/folder/file/"));
    }

    [TestMethod]
    public void Normalize_NullOrEmpty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, PathHelper.Normalize(null));
        Assert.AreEqual(string.Empty, PathHelper.Normalize(""));
    }

    [TestMethod]
    public void Normalize_SingleSegment_ReturnsAsIs()
    {
        Assert.AreEqual("file.txt", PathHelper.Normalize("file.txt"));
    }

    // ── Combine ──────────────────────────────────────────

    [TestMethod]
    public void Combine_TwoSegments_JoinedWithBackslash()
    {
        Assert.AreEqual(@"folder\file.txt", PathHelper.Combine("folder", "file.txt"));
    }

    [TestMethod]
    public void Combine_EmptyBase_ReturnsRelative()
    {
        Assert.AreEqual("file.txt", PathHelper.Combine("", "file.txt"));
    }

    [TestMethod]
    public void Combine_EmptyRelative_ReturnsBase()
    {
        Assert.AreEqual("folder", PathHelper.Combine("folder", ""));
    }

    [TestMethod]
    public void Combine_MixedSlashInputs_NormalizesBeforeJoining()
    {
        Assert.AreEqual(@"a\b\c\d", PathHelper.Combine("a/b", "c/d"));
    }

    [TestMethod]
    public void Combine_BothEmpty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, PathHelper.Combine("", ""));
    }

    // ── GetParent ────────────────────────────────────────

    [TestMethod]
    public void GetParent_NestedPath_ReturnsParent()
    {
        Assert.AreEqual(@"folder\subfolder", PathHelper.GetParent(@"folder\subfolder\file.txt"));
    }

    [TestMethod]
    public void GetParent_SingleSegment_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, PathHelper.GetParent("file.txt"));
    }

    [TestMethod]
    public void GetParent_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, PathHelper.GetParent(""));
    }

    [TestMethod]
    public void GetParent_ForwardSlashPath_NormalizesAndReturnsParent()
    {
        Assert.AreEqual("folder", PathHelper.GetParent("folder/file.txt"));
    }

    // ── GetName ──────────────────────────────────────────

    [TestMethod]
    public void GetName_WithPath_ReturnsLastSegment()
    {
        Assert.AreEqual("file.txt", PathHelper.GetName(@"folder\subfolder\file.txt"));
    }

    [TestMethod]
    public void GetName_SingleSegment_ReturnsSame()
    {
        Assert.AreEqual("file.txt", PathHelper.GetName("file.txt"));
    }

    [TestMethod]
    public void GetName_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, PathHelper.GetName(""));
    }

    [TestMethod]
    public void GetName_ForwardSlashPath_NormalizesAndReturnsName()
    {
        Assert.AreEqual("file.txt", PathHelper.GetName("folder/file.txt"));
    }
}
