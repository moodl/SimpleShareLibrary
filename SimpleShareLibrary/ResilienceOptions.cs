using System;

namespace SimpleShareLibrary
{
    public class ResilienceOptions
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
