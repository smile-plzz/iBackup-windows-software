# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-11-03
### Added
- **Modern Minimalist Brand Icon**: Vector squircle SVG source (`iBackup-logo.svg`), high-DPI 512x512 PNG, and multi-resolution Windows ICO (16, 24, 32, 48, 64, 128, 256 px).
- **Scheduled Automated Backups**: Background `BackupSchedulerService` supporting Daily, Weekly, and On-Connect intervals with AC power condition checks.
- **Wireless Wi-Fi Sync Companion**: Embedded zero-config local LAN server (`LocalWirelessServer` on port 52424) with QR Code mobile camera pairing, 4-digit PIN security, and direct camera roll upload into the local manifest.
- **Dedicated Wireless Page**: Interactive pairing view with QR code, link copying, browser test launcher, and Apple native Wi-Fi sync instructions.
- **Active Preference Wiring**: Full support for custom folder organization (Year/Month, Year/Month/Day, Flat), media type filters (Photos, Videos, RAW, Screenshots), SHA-256 integrity verification, and Windows startup (`StartupHelper`).
- **Device & Storage Meters**: Visual iPhone storage gauge on the dashboard and target drive available capacity indicator.
- **51 Unit Tests**: Comprehensive test suite covering backup engine, media helpers, scheduling, and local wireless server.

## [0.1.4] - 2026-10-14
### Changed
- Refined native interface with softer cards and adaptive navigation.
- Clarified preview boundaries and active vs planned settings.
- Robust path discovery for iOS 18/19 camera roll directories.
