# Architecture

## Overview

SimpleShareLibrary uses a **provider model** to abstract away protocol-specific details behind generic interfaces. The public API is protocol-agnostic — consumers code against `IShareClientFactory`, `IShareClient`, and `IShare` without knowing which protocol is used underneath.

```
┌──────────────────────────────────────────────┐
│              Consumer Code                    │
│  (uses IShareClientFactory / IShare)          │
└──────────────┬───────────────────────────────┘
               │
┌──────────────▼───────────────────────────────┐
│          Public Interfaces                    │
│  IShareClientFactory → IShareClient → IShare  │
│  ConnectionOptions, CopyOptions, MoveOptions   │
│  ShareFileInfo, ShareException hierarchy       │
└──────────────┬───────────────────────────────┘
               │
┌──────────────▼───────────────────────────────┐
│         Providers (internal)                  │
│  ┌─────────────────────────────────────────┐  │
│  │  SMB Provider (Providers/Smb/)          │  │
│  │  SmbShareClientFactory                  │  │
│  │  SmbShareClient                         │  │
│  │  SmbShare                               │  │
│  │  PathHelper, NTStatusMapper,            │  │
│  │  RetryHelper, SmbFileStream             │  │
│  └─────────────────────────────────────────┘  │
│                                               │
│  ┌─────────────────────────────────────────┐  │
│  │  Future: NFS, SFTP, WebDAV, ...        │  │
│  └─────────────────────────────────────────┘  │
└───────────────────────────────────────────────┘
```

## Layer Responsibilities

### Public Interfaces (`SimpleShareLibrary/`)

- **`IShareClientFactory`** — creates authenticated connections from `ConnectionOptions`
- **`IShareClient`** — represents an authenticated session; lists shares and opens them
- **`IShare`** — file and directory operations on an opened share
- **`ShareClientFactory`** — static entry point that creates protocol-specific factories

These interfaces define the contract. Consumers never reference provider-specific types.

### Options & Data Types

- **`ConnectionOptions`** — host, credentials, port, resilience settings
- **`CopyOptions` / `MoveOptions`** — control overwrite, recursion, safe mode
- **`ResilienceOptions`** — retry count, delay, timeout
- **`ShareFileInfo`** — file/directory metadata returned by listing and info operations

### Exception Hierarchy

```
ShareException
├── ShareAuthenticationException
├── ShareConnectionException
├── ShareFileNotFoundException
├── ShareDirectoryNotFoundException
├── ShareAccessDeniedException
├── ShareAlreadyExistsException
└── ShareIOException
```

Protocol-specific errors are mapped to these generic exceptions by each provider. For the SMB provider, `NTStatusMapper` handles this translation.

### SMB Provider (`Providers/Smb/`)

The only provider currently implemented. All classes are `internal`.

| Class | Role |
|---|---|
| `SmbShareClientFactory` | Connects to SMB servers, authenticates, returns `SmbShareClient` |
| `SmbShareClient` | Wraps `ISMBClient` session, lists shares, opens `SmbShare` instances |
| `SmbShare` | Implements all `IShare` operations against `ISMBFileStore` |
| `PathHelper` | Normalizes paths (forward/backslash, leading/trailing separators) |
| `NTStatusMapper` | Maps `NTStatus` codes to `ShareException` subtypes |
| `RetryHelper` | Polly-based retry with exponential backoff and timeout |
| `SmbFileStream` | `Stream` subclass with chunked read/write respecting SMB limits |
| `PortAwareSMB2Client` | Subclass exposing `SMB2Client.Connect` with custom port |

### Async/Sync Pattern

Every public operation has both an async and sync overload. Internally, `SmbShare` uses shared "Core" methods with a `bool useAsync` parameter that dispatches to either async or sync SMB IO wrappers, avoiding code duplication.

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| [SMBLibrary](https://www.nuget.org/packages/SMBLibrary) | 1.5.6 | SMB2/SMB3 protocol implementation |
| [Polly](https://www.nuget.org/packages/Polly) | 7.2.4 | Retry policies with exponential backoff |

## Target Framework

- **.NET Standard 2.0** — compatible with .NET Core 2.0+, .NET Framework 4.6.1+, and .NET 5+
