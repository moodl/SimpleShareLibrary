using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace SimpleShareLibrary.IntegrationTests;

/// <summary>
/// Manages the lifecycle of a Samba Docker container for integration tests.
/// Uses MSTest's AssemblyInitialize/Cleanup to start/stop the container once per test run.
/// </summary>
[TestClass]
public static class SmbDockerFixture
{
    #region Constants

    /// <summary>The Samba container image to use.</summary>
    private const string SambaImage = "dperson/samba:latest";

    /// <summary>The SMB port inside the container.</summary>
    private const ushort ContainerSmbPort = 445;

    /// <summary>The test username configured on the Samba server.</summary>
    public const string Username = "testuser";

    /// <summary>The test password configured on the Samba server.</summary>
    public const string Password = "TestPass123";

    /// <summary>The writable share name.</summary>
    public const string ShareName = "testshare";

    /// <summary>The read-only share name.</summary>
    public const string ReadOnlyShareName = "readonly";

    #endregion

    #region Fields

    private static IContainer? _container;

    #endregion

    #region Properties

    /// <summary>Gets the hostname to connect to the Samba container.</summary>
    public static string Host => _container?.Hostname
        ?? throw new InvalidOperationException("Samba container is not running.");

    /// <summary>Gets the mapped host port for the SMB service.</summary>
    public static ushort Port => _container?.GetMappedPublicPort(ContainerSmbPort)
        ?? throw new InvalidOperationException("Samba container is not running.");

    /// <summary>Gets whether the container is running.</summary>
    public static bool IsRunning => _container is not null;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Starts the Samba Docker container before any tests in the assembly run.
    /// </summary>
    [AssemblyInitialize]
    public static async Task StartContainer(TestContext _)
    {
        _container = new ContainerBuilder()
            .WithImage(SambaImage)
            .WithPortBinding(ContainerSmbPort, true)
            .WithCommand(
                "-u", $"{Username};{Password}",
                "-s", $"{ShareName};/share;yes;no;no;{Username};{Username};{Username}",
                "-s", $"{ReadOnlyShareName};/readonly;yes;no;no;{Username};{Username};{Username}",
                "-p")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(ContainerSmbPort))
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync().ConfigureAwait(false);

        // Give Samba a moment to fully initialize its services after the port is open
        await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops and removes the Samba Docker container after all tests complete.
    /// </summary>
    [AssemblyCleanup]
    public static async Task StopContainer()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
            _container = null;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a <see cref="ConnectionOptions"/> configured to connect to the test Samba server.
    /// </summary>
    public static ConnectionOptions CreateConnectionOptions()
    {
        return new ConnectionOptions
        {
            Host = Host,
            Port = Port,
            Username = Username,
            Password = Password,
            Resilience = new ResilienceOptions
            {
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromMilliseconds(500),
                OperationTimeout = TimeSpan.FromSeconds(30)
            }
        };
    }

    /// <summary>
    /// Creates a connected <see cref="IShareClient"/> for use in tests.
    /// </summary>
    public static async Task<IShareClient> CreateConnectedClientAsync()
    {
        var factory = ShareClientFactory.CreateSmb();
        return await factory.ConnectAsync(CreateConnectionOptions()).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a connected <see cref="IShareClient"/> and opens the writable test share.
    /// </summary>
    public static async Task<(IShareClient Client, IShare Share)> CreateConnectedShareAsync()
    {
        var client = await CreateConnectedClientAsync().ConfigureAwait(false);
        var share = await client.OpenShareAsync(ShareName).ConfigureAwait(false);
        return (client, share);
    }

    #endregion
}
