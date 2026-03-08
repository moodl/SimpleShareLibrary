using System;

namespace SimpleShareLibrary
{
    /// <summary>
    /// Options for establishing a connection to a remote file share.
    /// </summary>
    public class ConnectionOptions
    {
        /// <summary>The hostname or IP address of the remote server.</summary>
        public string Host { get; set; }

        /// <summary>The authentication domain. Defaults to empty (no domain).</summary>
        public string Domain { get; set; } = string.Empty;

        /// <summary>The username for authentication.</summary>
        public string Username { get; set; }

        /// <summary>The password for authentication.</summary>
        public string Password { get; set; }

        /// <summary>The port number to connect on. Defaults to 445 (SMB).</summary>
        public int Port { get; set; } = 445;

        /// <summary>The connection timeout. Defaults to 30 seconds.</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Retry and timeout resilience settings applied to all operations.</summary>
        public ResilienceOptions Resilience { get; set; } = new ResilienceOptions();
    }
}
