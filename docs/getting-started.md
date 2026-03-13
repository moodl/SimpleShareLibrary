# Getting Started

## Prerequisites

- .NET Standard 2.0 compatible project (.NET Core 2.0+, .NET Framework 4.6.1+, .NET 5+)

## Installation

```
dotnet add package SimpleShareLibrary
```

## Quick Start

### Connect and read a file

```csharp
var factory = ShareClientFactory.CreateSmb();

var options = new ConnectionOptions
{
    Host = "192.168.1.100",
    Username = "user",
    Password = "password"
};

using var client = await factory.ConnectAsync(options);
using var share = await client.OpenShareAsync("Documents");

string content = await share.ReadAllTextAsync("report.txt");
```

### Write a file

```csharp
await share.WriteAllTextAsync("notes/hello.txt", "Hello, world!");
```

### List files

```csharp
var files = await share.ListAsync("reports", "*.csv");

foreach (var file in files)
{
    Console.WriteLine($"{file.Name} — {file.Size} bytes");
}
```

### Copy and move

```csharp
// Copy a file
await share.CopyFileAsync("source.txt", "backup/source.txt");

// Move with safe mode (copy-then-delete, default)
await share.MoveFileAsync("temp/data.csv", "archive/data.csv");

// Move with unsafe mode (direct rename, faster but no rollback)
await share.MoveFileAsync("old.txt", "new.txt", new MoveOptions { Safe = false });
```

### Directory operations

```csharp
// Create nested directories
await share.CreateDirectoryAsync("reports/2026/q1");

// Recursively list all files
var allFiles = await share.ListRecursiveAsync("reports");

// Delete a directory and all its contents
await share.DeleteDirectoryAsync("temp", recursive: true);
```

## Synchronous API

Every async method has a synchronous counterpart:

```csharp
var client = factory.Connect(options);
var share = client.OpenShare("Documents");

string content = share.ReadAllText("report.txt");
share.WriteAllText("output.txt", "Done.");
```

## Connection Options

```csharp
var options = new ConnectionOptions
{
    Host = "server.local",      // Hostname or IP address (required)
    Username = "admin",         // Null for anonymous/guest access
    Password = "secret",        // Null for anonymous/guest access
    Domain = "CORP",            // Authentication domain (default: empty)
    Port = 445,                 // SMB port (default: 445)
    Resilience = new ResilienceOptions
    {
        MaxRetries = 3,                              // Retry attempts (default: 3)
        RetryDelay = TimeSpan.FromMilliseconds(500), // Base delay, exponential backoff (default: 500ms)
        OperationTimeout = TimeSpan.FromSeconds(30)  // Per-operation timeout (default: 30s)
    }
};
```

## Error Handling

All errors throw specific exceptions derived from `ShareException`:

```csharp
try
{
    var content = await share.ReadAllTextAsync("missing.txt");
}
catch (ShareFileNotFoundException ex)
{
    Console.WriteLine($"File not found: {ex.Path}");
}
catch (ShareAccessDeniedException ex)
{
    Console.WriteLine($"Access denied: {ex.Path}");
}
catch (ShareConnectionException)
{
    Console.WriteLine("Connection lost.");
}
```

| Exception | When |
|---|---|
| `ShareAuthenticationException` | Invalid credentials |
| `ShareConnectionException` | Cannot connect or connection lost |
| `ShareFileNotFoundException` | File does not exist |
| `ShareDirectoryNotFoundException` | Directory does not exist |
| `ShareAccessDeniedException` | Insufficient permissions |
| `ShareAlreadyExistsException` | File/directory already exists (when overwrite is disabled) |
| `ShareIOException` | Timeout, sharing violation, or other I/O error |

## Next Steps

- [API Reference](api-reference.md) — full interface documentation
- [Architecture](architecture.md) — how the library is structured
