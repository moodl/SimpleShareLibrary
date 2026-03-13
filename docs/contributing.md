# Contributing

Contributions are welcome! Whether it's bug fixes, improvements, or entirely new protocol providers — all contributions help make this library more useful.

## Adding a New Protocol Provider

The library is designed around a provider model. Adding support for a new protocol (e.g., NFS, SFTP, WebDAV) means implementing three internal interfaces:

### 1. Implement `IShareClientFactory`

Create a factory that connects and authenticates using `ConnectionOptions`:

```csharp
internal class SftpShareClientFactory : IShareClientFactory
{
    public Task<IShareClient> ConnectAsync(ConnectionOptions options, CancellationToken ct = default)
    {
        // Connect and authenticate using your protocol's client library
    }

    public IShareClient Connect(ConnectionOptions options)
    {
        // Synchronous version
    }
}
```

### 2. Implement `IShareClient`

Wrap an authenticated session. Provide share listing and opening:

```csharp
internal class SftpShareClient : IShareClient
{
    public bool IsConnected => /* ... */;

    public Task<IReadOnlyList<string>> ListSharesAsync(CancellationToken ct = default) { /* ... */ }
    public Task<IShare> OpenShareAsync(string shareName, CancellationToken ct = default) { /* ... */ }

    // + sync overloads
    // + IDisposable
}
```

### 3. Implement `IShare`

This is the bulk of the work — implement all file and directory operations:

```csharp
internal class SftpShare : IShare
{
    // Metadata: Exists, GetInfo, List, ListRecursive
    // Read: ReadAllBytes, ReadAllText, OpenRead
    // Write: WriteAllBytes, WriteAllText, OpenWrite
    // Copy: CopyFile, CopyDirectory
    // Move: MoveFile, MoveDirectory
    // Delete: DeleteFile, DeleteDirectory, DeleteAll
    // Directories: CreateDirectory, EnsureDirectoryExists
    // Rename: Rename
    // Each with async + sync overloads
}
```

### 4. Register in `ShareClientFactory`

Add a static factory method:

```csharp
public static class ShareClientFactory
{
    public static IShareClientFactory CreateSmb() => new SmbShareClientFactory();
    public static IShareClientFactory CreateSftp() => new SftpShareClientFactory(); // new
}
```

### Guidelines for Providers

- All provider classes should be `internal` — only interfaces are public
- Map protocol-specific errors to `ShareException` subtypes (see `NTStatusMapper` for an example)
- Support both async and sync overloads
- Normalize paths using `PathHelper` or your own equivalent
- Use `ResilienceOptions` for retry/timeout if applicable
- Add unit tests and integration tests (see `SimpleShareLibrary.Tests` and `SimpleShareLibrary.IntegrationTests`)

## General Contributions

### Bug Fixes & Improvements

1. Fork the repository
2. Create a branch (`fix/your-fix` or `feature/your-feature`)
3. Make your changes
4. Run all tests: `dotnet test`
5. Submit a pull request

### Code Style

- Follow [Microsoft C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Allman braces, explicit access modifiers, XML doc comments on public/internal members
- Nullable reference types enabled — annotate appropriately
- Keep methods short and focused

### Testing

- Unit tests use MSTest + Moq
- Integration tests use Testcontainers (Docker-based, run in CI)
- Run before submitting: `dotnet test`

## Dependencies

When adding a new provider, keep dependencies reasonable:

- Prefer well-maintained, permissive-licensed (MIT, Apache 2.0, BSD) packages
- No GPL/AGPL dependencies without prior discussion
- Don't add dependencies for trivial functionality
