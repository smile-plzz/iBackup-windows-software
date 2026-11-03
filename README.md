# iBackup

Your memories, on your own drive.

A native Windows app for copying iPhone and iPad photos and videos over USB and local Wi-Fi. Files are saved in ordinary folders, with incremental tracking so a repeat backup skips previously recorded files.

## What it does

- **Dual-Mode Sync**: Supports both direct USB cable transfers and local Wi-Fi wireless backups (via native Apple Wi-Fi sync and embedded QR-code local companion).
- **Scheduled Automation**: Automated daily, weekly, or connection-triggered background backups with smart AC-power detection.
- **Incremental Engine**: Skips files already recorded in the SQLite manifest; never duplicates.
- **Non-Destructive**: Never overwrites existing destination files (appends numbering) and never touches device originals.
- **Active Customization**:
  - Configurable folder organization: By Year/Month, By Year/Month/Day, or Flat.
  - Active media filters: Photos, Videos, Apple ProRAW (DNG), Screenshots.
  - Optional SHA-256 copy verification.
- **Modern Minimalist Design**:
  - Apple-inspired minimalism meets Windows 11 Fluent Design.
  - Device storage capacity breakdown and target drive space gauges.
  - Brand vector icon & multi-resolution Windows `.ico`.
- **Privacy-First**: Operates 100% offline; zero cloud subscriptions, zero tracking or telemetry.

## Quick Start

### Option A: USB Cable Backup
1. Connect your iPhone or iPad via USB, unlock it, and tap **Trust This Computer** if prompted.
2. Select your backup folder under **Save to**.
3. Click **Start Backup**.

### Option B: Wireless Wi-Fi Backup
1. Go to the **Wireless** tab in iBackup.
2. Open your iPhone Camera and point it at the QR code, or navigate to the local link with your 4-digit PIN.
3. Tap **Select Photos & Videos** to transfer files directly to your PC drive at full local Wi-Fi speeds.

### Option C: Scheduled Automation
1. Open **Settings** -> **Scheduled Automated Backups**.
2. Toggle on, choose your frequency (e.g., Daily at 21:00), and optionally require AC power.
3. iBackup will run smoothly in the background and keep your media archived.

## Development & Build

Windows 10/11 x64 and .NET 8 SDK are required.

```powershell
# Restore dependencies
dotnet restore iBackup.sln

# Run unit tests
dotnet test src/iBackup.Core.Tests/iBackup.Core.Tests.csproj -c Release

# Build Release binary
dotnet build src/iBackup.App/iBackup.App.csproj -c Release -p:Platform=x64

# Package portable self-contained release
dotnet publish src/iBackup.App/iBackup.App.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -o artifacts/win-x64
```

## Documentation

- [Product Requirements](docs/PRD.md)
- [Design Conventions](docs/DESIGN.md)
- [Technical Architecture](docs/TECHNICAL-DESIGN.md)
- [Development Log](dev-log.md)
- [Changelog](CHANGELOG.md)

## License

MIT / Proprietary. All rights reserved.
