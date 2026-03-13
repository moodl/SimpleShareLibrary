# API Reference

## Entry Point

### `ShareClientFactory`

Static factory for creating protocol-specific client factories.

```csharp
public static class ShareClientFactory
{
    // Creates an SMB-backed factory (SMB2/SMB3 via SMBLibrary)
    public static IShareClientFactory CreateSmb();
}
```

---

## Interfaces

### `IShareClientFactory`

Creates authenticated connections to remote file share servers.

```csharp
public interface IShareClientFactory
{
    Task<IShareClient> ConnectAsync(ConnectionOptions options, CancellationToken ct = default);
    IShareClient Connect(ConnectionOptions options);
}
```

### `IShareClient`

Represents an authenticated session. Use it to list and open shares.

```csharp
public interface IShareClient : IDisposable
{
    bool IsConnected { get; }

    Task<IReadOnlyList<string>> ListSharesAsync(CancellationToken ct = default);
    IReadOnlyList<string> ListShares();

    Task<IShare> OpenShareAsync(string shareName, CancellationToken ct = default);
    IShare OpenShare(string shareName);
}
```

### `IShare`

The main interface for file and directory operations on an opened share. Every method has both an async and sync overload.

#### Metadata & Listing

| Method | Description |
|---|---|
| `Exists(path)` | Check if a file or directory exists |
| `GetInfo(path)` | Get metadata (size, timestamps, attributes) |
| `List(path, pattern)` | List entries matching a glob pattern |
| `ListRecursive(path, pattern)` | Recursively list all matching entries |

#### Read

| Method | Description |
|---|---|
| `ReadAllBytes(path)` | Read file contents as byte array |
| `ReadAllText(path, encoding?)` | Read file contents as string (default: UTF-8) |
| `OpenRead(path)` | Open a readable stream |

#### Write

| Method | Description |
|---|---|
| `WriteAllBytes(path, data, overwrite?)` | Write byte array to file |
| `WriteAllText(path, text, encoding?, overwrite?)` | Write string to file |
| `OpenWrite(path, overwrite?)` | Open a writable stream |

#### Copy

| Method | Description |
|---|---|
| `CopyFile(src, dst, options?)` | Copy a single file |
| `CopyDirectory(src, dst, options?)` | Copy a directory and its contents |

#### Move

| Method | Description |
|---|---|
| `MoveFile(src, dst, options?)` | Move a file (safe copy-then-delete by default) |
| `MoveDirectory(src, dst, options?)` | Move a directory |

#### Delete

| Method | Description |
|---|---|
| `DeleteFile(path)` | Delete a single file |
| `DeleteDirectory(path, recursive?)` | Delete a directory (optionally with contents) |
| `DeleteAll(path)` | Delete all contents within a directory |

#### Directories

| Method | Description |
|---|---|
| `CreateDirectory(path, createParents?)` | Create a directory (with parent creation by default) |
| `EnsureDirectoryExists(path)` | Create directory only if it doesn't exist |

#### Rename

| Method | Description |
|---|---|
| `Rename(path, newName)` | Rename a file or directory |

---

## Options Classes

### `ConnectionOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `Host` | `string` | *(required)* | Hostname or IP address |
| `Username` | `string?` | `null` | Username (null for anonymous) |
| `Password` | `string?` | `null` | Password (null for anonymous) |
| `Domain` | `string` | `""` | Authentication domain |
| `Port` | `int` | `445` | Connection port |
| `Resilience` | `ResilienceOptions` | *(defaults)* | Retry and timeout settings |

### `ResilienceOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxRetries` | `int` | `3` | Maximum retry attempts |
| `RetryDelay` | `TimeSpan` | `500ms` | Base delay (exponential backoff) |
| `OperationTimeout` | `TimeSpan` | `30s` | Per-operation timeout |

### `CopyOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `Overwrite` | `bool` | `false` | Overwrite existing files |
| `Recursive` | `bool` | `true` | Copy subdirectories |

### `MoveOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `Safe` | `bool` | `true` | Use copy-then-delete (safer, slower) |
| `Overwrite` | `bool` | `false` | Overwrite existing files |
| `Recursive` | `bool` | `true` | Move subdirectories |

---

## Data Types

### `ShareFileInfo`

Metadata returned by `GetInfo`, `List`, and `ListRecursive`.

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | File or directory name (without path) |
| `FullPath` | `string` | Full path relative to share root |
| `IsDirectory` | `bool` | Whether this entry is a directory |
| `Size` | `long` | File size in bytes (0 for directories) |
| `CreatedUtc` | `DateTime` | UTC creation time |
| `LastWriteUtc` | `DateTime` | UTC last write time |
| `LastAccessUtc` | `DateTime` | UTC last access time |
| `IsReadOnly` | `bool` | Read-only attribute |
| `IsHidden` | `bool` | Hidden attribute |

---

## Exceptions

All exceptions inherit from `ShareException`.

| Exception | Properties | Thrown When |
|---|---|---|
| `ShareException` | — | Base class for all share errors |
| `ShareAuthenticationException` | — | Invalid credentials |
| `ShareConnectionException` | — | Connection failure or loss |
| `ShareFileNotFoundException` | `Path` | File does not exist |
| `ShareDirectoryNotFoundException` | `Path` | Directory does not exist |
| `ShareAccessDeniedException` | `Path` | Insufficient permissions |
| `ShareAlreadyExistsException` | `Path` | Target already exists |
| `ShareIOException` | — | Timeout, sharing violation, I/O error |
