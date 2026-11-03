# Context

## Product & Goals
- **Product**: iBackup - iOS Media Backup for Windows.
- **Goal**: Provide a native, zero-friction, privacy-first, incremental backup tool for iPhone/iPad media (photos, videos, RAW, Live Photos) to local or external storage, with modern minimalist design, scheduled automation, and wireless/local-network sync capabilities.
- **Aesthetic**: Modern Minimalist (Apple elegance + Windows 11 Fluent Design / Mica, soft 16px cards, typography restraint, system-theme aware, high accessibility).

## Technology Stack
- **Frontend / Shell**: WinUI 3 (Windows App SDK 1.5+), XAML, CommunityToolkit.Mvvm (.NET 8).
- **Core / Engine**: C# (.NET 8 LTS), MediaDevices (WPD/MTP protocol), Microsoft.Data.Sqlite, System.Text.Json.
- **Logging & Diagnostics**: Serilog (rolling local logs in `%LOCALAPPDATA%\iBackup\logs`).
- **Target OS**: Windows 10 (1809+) & Windows 11 x64.
- **Testing**: MSTest / xUnit in `src/iBackup.Core.Tests`.

## Architecture & Invariants
- **Non-destructive**: Never overwrite destination files (append collision suffixes `-1`, `-2`, etc.) and never modify/delete device originals.
- **Incremental Manifest**: SQLite `manifest.db` tracks file fingerprints (`device_udid`, `file_name`, `file_size`, `creation_date`) and SHA-256 hashes.
- **Thread Safety**: All WPD / COM / network blocking operations run off the UI thread via `Task.Run` with responsive cancellation tokens.
- **No Cloud / Privacy First**: Zero telemetry, no cloud dependencies, offline-first.

