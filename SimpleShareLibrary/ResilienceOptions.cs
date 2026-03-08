using System;

namespace SimpleShareLibrary
{
    /// <summary>
    /// Options for retry and timeout resilience applied to share operations.
    /// </summary>
    public class ResilienceOptions
    {
        /// <summary>Maximum number of retry attempts before giving up. Defaults to 3.</summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>Delay between retry attempts. Defaults to 500 ms.</summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>Timeout for a single operation. Defaults to 30 seconds.</summary>
        public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
