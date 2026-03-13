# SimpleShareLibrary

> **Note:** This project is in early development. The API may change without notice and has not been validated in production environments. Use at your own risk.

A simplified .NET Standard 2.0 wrapper around [SMBLibrary](https://github.com/TalAloni/SMBLibrary) by [Tal Aloni](https://github.com/TalAloni).

## Why?

SMBLibrary is a powerful, pure .NET SMB client/server implementation supporting SMB 1.0 through 3.1.1. However, its low-level API requires significant boilerplate for common operations. SimpleShareLibrary provides an easy-to-use, protocol-agnostic API for everyday SMB file operations.

**Before (SMBLibrary):**
```csharp
SMB2Client client = new SMB2Client();
bool isConnected = client.Connect(IPAddress.Parse("192.168.1.11"), SMBTransportType.DirectTCPTransport);
if (isConnected)
{
    NTStatus status = client.Login(String.Empty, "user", "pass");
    if (status == NTStatus.STATUS_SUCCESS)
    {
        ISMBFileStore fileStore = client.TreeConnect("Share", out status);
        if (status == NTStatus.STATUS_SUCCESS)
        {
            object fileHandle;
            FileStatus fileStatus;
            status = fileStore.CreateFile(out fileHandle, out fileStatus, "file.txt",
                AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE, FileAttributes.Normal,
                ShareAccess.Read, CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
            // ... read loop, close, disconnect, logoff ...
        }
    }
}
```

**After (SimpleShareLibrary):**
```csharp
using (var smb = new SmbClient("192.168.1.11", "Share", "user", "pass"))
{
    byte[] data = smb.ReadFile("file.txt");
}
```

## Installation

```
dotnet add package SimpleShareLibrary
```

## License

This project is licensed under the [MIT License](LICENSE).

## Third-Party Dependencies

This library uses [SMBLibrary](https://github.com/TalAloni/SMBLibrary) by **Tal Aloni** ([@TalAloni](https://github.com/TalAloni)) as a runtime NuGet dependency, licensed under [LGPL-3.0-or-later](LICENSES/LGPL-3.0.txt). SMBLibrary is **not** embedded or merged into this assembly — it remains a separate, replaceable component as required by the LGPL.

All core SMB protocol implementation credit belongs to Tal Aloni and the SMBLibrary contributors.

## Disclaimer

This project is **not affiliated with, endorsed by, or sponsored by** Tal Aloni or the SMBLibrary project.

This software is provided "as is", without warranty of any kind. See [DISCLAIMER.md](DISCLAIMER.md) for full details (English and German).

## Acknowledgments

Built on top of [SMBLibrary](https://github.com/TalAloni/SMBLibrary) — a powerful, pure .NET SMB client/server implementation. Without SMBLibrary, this project would not exist.
