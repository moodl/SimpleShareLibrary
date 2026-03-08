using System;

namespace SimpleShareLibrary
{
    public class ConnectionOptions
    {
        public string Host { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string Username { get; set; }
        public string Password { get; set; }
        public int Port { get; set; } = 445;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        public ResilienceOptions Resilience { get; set; } = new ResilienceOptions();
    }
}
