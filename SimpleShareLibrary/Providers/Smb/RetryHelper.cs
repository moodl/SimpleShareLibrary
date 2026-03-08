using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Polly;
using Polly.Timeout;
using SimpleShareLibrary.Exceptions;

namespace SimpleShareLibrary.Providers.Smb
{
    /// <summary>
    /// Retry helper using Polly with async exponential backoff and optional timeout.
    /// Retries only on transient exceptions (connection, I/O).
    /// Void policies are cached per unique (MaxRetries, RetryDelay, OperationTimeout) combination.
    /// </summary>
    internal static class RetryHelper
    {
        private static readonly ConcurrentDictionary<string, IAsyncPolicy> _voidPolicyCache
            = new ConcurrentDictionary<string, IAsyncPolicy>();

        internal static Task<T> ExecuteAsync<T>(Func<Task<T>> action, ResilienceOptions options)
        {
            var policy = BuildTypedPolicy<T>(options);
            return policy.ExecuteAsync(action);
        }

        internal static Task ExecuteAsync(Func<Task> action, ResilienceOptions options)
        {
            var policy = GetOrCreateVoidPolicy(options);
            return policy.ExecuteAsync(action);
        }

        private static IAsyncPolicy<T> BuildTypedPolicy<T>(ResilienceOptions options)
        {
            var maxRetries = options?.MaxRetries ?? 3;
            var baseDelay = options?.RetryDelay ?? TimeSpan.FromMilliseconds(500);
            var timeout = options?.OperationTimeout ?? TimeSpan.Zero;

            var retryPolicy = Policy<T>
                .Handle<ShareConnectionException>()
                .Or<ShareIOException>()
                .WaitAndRetryAsync(
                    maxRetries,
                    attempt => TimeSpan.FromMilliseconds(
                        baseDelay.TotalMilliseconds * Math.Pow(2, attempt)));

            if (timeout > TimeSpan.Zero)
            {
                var timeoutPolicy = Policy.TimeoutAsync<T>(timeout, TimeoutStrategy.Optimistic);
                return Policy.WrapAsync(timeoutPolicy, retryPolicy);
            }

            return retryPolicy;
        }

        private static IAsyncPolicy GetOrCreateVoidPolicy(ResilienceOptions options)
        {
            var maxRetries = options?.MaxRetries ?? 3;
            var baseDelay = options?.RetryDelay ?? TimeSpan.FromMilliseconds(500);
            var timeout = options?.OperationTimeout ?? TimeSpan.Zero;

            var key = $"{maxRetries}_{baseDelay.TotalMilliseconds}_{timeout.TotalMilliseconds}";

            return _voidPolicyCache.GetOrAdd(key, _ =>
            {
                var retryPolicy = Policy
                    .Handle<ShareConnectionException>()
                    .Or<ShareIOException>()
                    .WaitAndRetryAsync(
                        maxRetries,
                        attempt => TimeSpan.FromMilliseconds(
                            baseDelay.TotalMilliseconds * Math.Pow(2, attempt)));

                if (timeout > TimeSpan.Zero)
                {
                    var timeoutPolicy = Policy.TimeoutAsync(timeout, TimeoutStrategy.Optimistic);
                    return Policy.WrapAsync(timeoutPolicy, retryPolicy);
                }

                return retryPolicy;
            });
        }
    }
}
