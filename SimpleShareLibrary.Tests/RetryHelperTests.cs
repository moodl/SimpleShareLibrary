using SimpleShareLibrary.Exceptions;
using SimpleShareLibrary.Providers.Smb;

namespace SimpleShareLibrary.Tests;

[TestClass]
public class RetryHelperTests
{
    private readonly ResilienceOptions _fastOptions = new()
    {
        MaxRetries = 3,
        RetryDelay = TimeSpan.FromMilliseconds(1)
    };

    [TestMethod]
    public async Task ExecuteAsync_SucceedsFirstAttempt_ReturnsResult()
    {
        var result = await RetryHelper.ExecuteAsync(
            () => Task.FromResult(42), _fastOptions);

        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransientConnectionException_RetriesAndSucceeds()
    {
        int attempts = 0;
        var result = await RetryHelper.ExecuteAsync(() =>
        {
            attempts++;
            if (attempts < 3)
                throw new ShareConnectionException("transient");
            return Task.FromResult("ok");
        }, _fastOptions);

        Assert.AreEqual("ok", result);
        Assert.AreEqual(3, attempts);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransientIOException_RetriesAndSucceeds()
    {
        int attempts = 0;
        var result = await RetryHelper.ExecuteAsync(() =>
        {
            attempts++;
            if (attempts < 2)
                throw new ShareIOException("transient io");
            return Task.FromResult("ok");
        }, _fastOptions);

        Assert.AreEqual("ok", result);
        Assert.AreEqual(2, attempts);
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthException_DoesNotRetry()
    {
        int attempts = 0;
        await Assert.ThrowsExceptionAsync<ShareAuthenticationException>(async () =>
        {
            await RetryHelper.ExecuteAsync<int>(() =>
            {
                attempts++;
                throw new ShareAuthenticationException("bad auth");
            }, _fastOptions);
        });

        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public async Task ExecuteAsync_AccessDeniedException_DoesNotRetry()
    {
        int attempts = 0;
        await Assert.ThrowsExceptionAsync<ShareAccessDeniedException>(async () =>
        {
            await RetryHelper.ExecuteAsync<int>(() =>
            {
                attempts++;
                throw new ShareAccessDeniedException("secret");
            }, _fastOptions);
        });

        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public async Task ExecuteAsync_FileNotFoundException_DoesNotRetry()
    {
        int attempts = 0;
        await Assert.ThrowsExceptionAsync<ShareFileNotFoundException>(async () =>
        {
            await RetryHelper.ExecuteAsync<int>(() =>
            {
                attempts++;
                throw new ShareFileNotFoundException("missing");
            }, _fastOptions);
        });

        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public async Task ExecuteAsync_AlreadyExistsException_DoesNotRetry()
    {
        int attempts = 0;
        await Assert.ThrowsExceptionAsync<ShareAlreadyExistsException>(async () =>
        {
            await RetryHelper.ExecuteAsync<int>(() =>
            {
                attempts++;
                throw new ShareAlreadyExistsException("dup");
            }, _fastOptions);
        });

        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public async Task ExecuteAsync_AllRetriesExhausted_ThrowsLastException()
    {
        int attempts = 0;
        var ex = await Assert.ThrowsExceptionAsync<ShareConnectionException>(async () =>
        {
            await RetryHelper.ExecuteAsync<int>(() =>
            {
                attempts++;
                throw new ShareConnectionException($"attempt {attempts}");
            }, _fastOptions);
        });

        // MaxRetries=3 means 1 initial + 3 retries = 4 total attempts
        Assert.AreEqual(4, attempts);
    }

    [TestMethod]
    public async Task ExecuteAsync_VoidOverload_SucceedsFirstAttempt()
    {
        bool called = false;
        await RetryHelper.ExecuteAsync(() =>
        {
            called = true;
            return Task.CompletedTask;
        }, _fastOptions);

        Assert.IsTrue(called);
    }

    [TestMethod]
    public async Task ExecuteAsync_VoidOverload_RetriesOnTransient()
    {
        int attempts = 0;
        await RetryHelper.ExecuteAsync(() =>
        {
            attempts++;
            if (attempts < 2)
                throw new ShareIOException("transient");
            return Task.CompletedTask;
        }, _fastOptions);

        Assert.AreEqual(2, attempts);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullOptions_UsesDefaults()
    {
        var result = await RetryHelper.ExecuteAsync(
            () => Task.FromResult(99), null!);

        Assert.AreEqual(99, result);
    }
}
