using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Polly;
using Polly.Timeout;
using SimpleShareLibrary.Exceptions;

namespace SimpleShareLibrary.Providers.Smb
{
    /// <summary>
    /// Retry helper using Polly with exponential backoff and optional timeout.
    /// Provides both async and sync execution paths.
    /// Retries only on transient exceptions (connection, I/O).
    /// Void policies are cached per unique (MaxRetries, RetryDelay, OperationTimeout) combination.
    /// </summary>
    internal static class RetryHelper
    {
        private static readonly ConcurrentDictionary<string, IAsyncPolicy> _voidAsyncPolicyCache
            = new ConcurrentDictionary<string, IAsyncPolicy>();

        private static readonly ConcurrentDictionary<string, ISyncPolicy> _voidSyncPolicyCache
            = new ConcurrentDictionary<string, ISyncPolicy>();

        /// <summary>
        /// Executes an async action with retry and optional timeout, returning a result.
        /// Retries on <see cref="ShareConnectionException"/> and <see cref="ShareIOException"/>.
        /// </summary>
        /// <typeparam name="T">The return type of the action.</typeparam>
        /// <param name="action">The async action to execute.</param>
        /// <param name="options">Resilience settings controlling retry count, delay, and timeout.</param>
        /// <returns>The result of the action.</returns>
        internal static Task<T> ExecuteAsync<T>(Func<Task<T>> action, ResilienceOptions options)
        {
            var policy = BuildTypedPolicy<T>(options);
            return policy.ExecuteAsync(action);
        }

        /// <summary>
        /// Executes an async action with retry and optional timeout (no return value).
        /// Retries on <see cref="ShareConnectionException"/> and <see cref="ShareIOException"/>.
        /// </summary>
        /// <param name="action">The async action to execute.</param>
        /// <param name="options">Resilience settings controlling retry count, delay, and timeout.</param>
        internal static Task ExecuteAsync(Func<Task> action, ResilienceOptions options)
        {
            var policy = GetOrCreateVoidPolicy(options);
            return policy.ExecuteAsync(action);
        }

        #region Sync

        /// <summary>
        /// Executes a synchronous action with retry and optional timeout, returning a result.
        /// Retries on <see cref="ShareConnectionException"/> and <see cref="ShareIOException"/>.
        /// </summary>
        /// <typeparam name="T">The return type of the action.</typeparam>
        /// <param name="action">The synchronous action to execute.</param>
        /// <param name="options">Resilience settings controlling retry count, delay, and timeout.</param>
        /// <returns>The result of the action.</returns>
        internal static T Execute<T>(Func<T> action, ResilienceOptions options)
        {
            var policy = BuildTypedSyncPolicy<T>(options);
            return policy.Execute(action);
        }

        /// <summary>
        /// Executes a synchronous action with retry and optional timeout (no return value).
        /// Retries on <see cref="ShareConnectionException"/> and <see cref="ShareIOException"/>.
        /// </summary>
        /// <param name="action">The synchronous action to execute.</param>
        /// <param name="options">Resilience settings controlling retry count, delay, and timeout.</param>
        internal static void Execute(Action action, ResilienceOptions options)
        {
            var policy = GetOrCreateVoidSyncPolicy(options);
            policy.Execute(action);
        }

        #endregion

        #region Async Policy Builders

        /// <summary>
        /// Builds a fresh typed Polly policy with exponential backoff retry and optional timeout.
        /// </summary>
        /// <typeparam name="T">The return type of the policy.</typeparam>
        /// <param name="options">Resilience settings; uses defaults if <c>null</c>.</param>
        /// <returns>A composed <see cref="IAsyncPolicy{T}"/>.</returns>
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

        /// <summary>
        /// Gets or creates a cached async void Polly policy with exponential backoff retry and optional timeout.
        /// Policies are cached by their (MaxRetries, RetryDelay, OperationTimeout) combination.
        /// </summary>
        /// <param name="options">Resilience settings; uses defaults if <c>null</c>.</param>
        /// <returns>A composed <see cref="IAsyncPolicy"/>.</returns>
        private static IAsyncPolicy GetOrCreateVoidPolicy(ResilienceOptions options)
        {
            var maxRetries = options?.MaxRetries ?? 3;
            var baseDelay = options?.RetryDelay ?? TimeSpan.FromMilliseconds(500);
            var timeout = options?.OperationTimeout ?? TimeSpan.Zero;

            var key = $"{maxRetries}_{baseDelay.TotalMilliseconds}_{timeout.TotalMilliseconds}";

            return _voidAsyncPolicyCache.GetOrAdd(key, _ =>
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

        #endregion

        #region Sync Policy Builders

        /// <summary>
        /// Builds a fresh typed synchronous Polly policy with exponential backoff retry and optional timeout.
        /// </summary>
        /// <typeparam name="T">The return type of the policy.</typeparam>
        /// <param name="options">Resilience settings; uses defaults if <c>null</c>.</param>
        /// <returns>A composed <see cref="ISyncPolicy{T}"/>.</returns>
        private static ISyncPolicy<T> BuildTypedSyncPolicy<T>(ResilienceOptions options)
        {
            var maxRetries = options?.MaxRetries ?? 3;
            var baseDelay = options?.RetryDelay ?? TimeSpan.FromMilliseconds(500);
            var timeout = options?.OperationTimeout ?? TimeSpan.Zero;

            var retryPolicy = Policy<T>
                .Handle<ShareConnectionException>()
                .Or<ShareIOException>()
                .WaitAndRetry(
                    maxRetries,
                    attempt => TimeSpan.FromMilliseconds(
                        baseDelay.TotalMilliseconds * Math.Pow(2, attempt)));

            if (timeout > TimeSpan.Zero)
            {
                var timeoutPolicy = Policy.Timeout<T>(timeout, TimeoutStrategy.Optimistic);
                return Policy.Wrap(timeoutPolicy, retryPolicy);
            }

            return retryPolicy;
        }

        /// <summary>
        /// Gets or creates a cached synchronous void Polly policy with exponential backoff retry and optional timeout.
        /// Policies are cached by their (MaxRetries, RetryDelay, OperationTimeout) combination.
        /// </summary>
        /// <param name="options">Resilience settings; uses defaults if <c>null</c>.</param>
        /// <returns>A composed <see cref="ISyncPolicy"/>.</returns>
        private static ISyncPolicy GetOrCreateVoidSyncPolicy(ResilienceOptions options)
        {
            var maxRetries = options?.MaxRetries ?? 3;
            var baseDelay = options?.RetryDelay ?? TimeSpan.FromMilliseconds(500);
            var timeout = options?.OperationTimeout ?? TimeSpan.Zero;

            var key = $"{maxRetries}_{baseDelay.TotalMilliseconds}_{timeout.TotalMilliseconds}";

            return _voidSyncPolicyCache.GetOrAdd(key, _ =>
            {
                var retryPolicy = Policy
                    .Handle<ShareConnectionException>()
                    .Or<ShareIOException>()
                    .WaitAndRetry(
                        maxRetries,
                        attempt => TimeSpan.FromMilliseconds(
                            baseDelay.TotalMilliseconds * Math.Pow(2, attempt)));

                if (timeout > TimeSpan.Zero)
                {
                    var timeoutPolicy = Policy.Timeout(timeout, TimeoutStrategy.Optimistic);
                    return Policy.Wrap(timeoutPolicy, retryPolicy);
                }

                return retryPolicy;
            });
        }

        #endregion
    }
}
