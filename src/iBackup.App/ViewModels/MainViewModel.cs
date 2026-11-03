using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using iBackup.App.Converters;
using iBackup.Core.Interfaces;
using iBackup.Core.Models;
using iBackup.Core.Services;

namespace iBackup.App.ViewModels;

/// <summary>
/// ViewModel for the main dashboard view, managing device connection status,
/// backup execution lifecycle, storage meters, automated scheduling, and live transfer progress reporting.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IDeviceMonitor _deviceMonitor;
    private readonly IBackupEngine _backupEngine;
    private readonly IManifestRepository _manifestRepository;
    private readonly IBackupSchedulerService _schedulerService;
    private readonly DispatcherQueue? _dispatcherQueue;
    private CancellationTokenSource? _backupCts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBackupCommand))]
    private DeviceInfo? _connectedDevice;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBackupCommand))]
    private string _destinationPath = string.Empty;

    [ObservableProperty]
    private string _statusText = "Waiting for device...";

    [ObservableProperty]
    private string _activityStage = "Waiting";

    [ObservableProperty]
    private string _startButtonText = "Start Backup";

    [ObservableProperty]
    private bool _isMessageOpen;

    [ObservableProperty]
    private string _messageTitle = string.Empty;

    [ObservableProperty]
    private string _messageText = string.Empty;

    [ObservableProperty]
    private InfoBarSeverity _messageSeverity = InfoBarSeverity.Informational;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBackupCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseResumeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelBackupCommand))]
    private bool _isBackingUp;

    [ObservableProperty]
    private bool _isPaused;

    public string PauseResumeButtonText => IsPaused ? "Resume" : "Pause";

    partial void OnIsPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(PauseResumeButtonText));
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBackupCommand))]
    private bool _isDeviceConnected;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _filesProgress = "0 / 0";

    [ObservableProperty]
    private string _bytesProgress = "0 B / 0 B";

    [ObservableProperty]
    private string _estimatedTimeRemaining = "--:--";

    [ObservableProperty]
    private string _lastBackupSummary = "No backups recorded yet.";

    [ObservableProperty]
    private bool _autoBackupOnConnect;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private double _deviceStoragePercent;

    [ObservableProperty]
    private string _deviceStorageFormatted = string.Empty;

    [ObservableProperty]
    private string _destinationFreeSpaceSummary = string.Empty;

    [ObservableProperty]
    private string _connectionModeText = "USB Connection";

    [ObservableProperty]
    private string _nextScheduledBackupSummary = string.Empty;

    /// <summary>
    /// Event fired when the user requests folder selection via the Browse command.
    /// </summary>
    public event Action? RequestBrowseFolder;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    public MainViewModel(
        IDeviceMonitor deviceMonitor,
        IBackupEngine backupEngine,
        IManifestRepository manifestRepository,
        IBackupSchedulerService schedulerService)
    {
        _deviceMonitor = deviceMonitor ?? throw new ArgumentNullException(nameof(deviceMonitor));
        _backupEngine = backupEngine ?? throw new ArgumentNullException(nameof(backupEngine));
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Default backup destination to Pictures\iBackup
        var defaultPictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        DestinationPath = Path.Combine(defaultPictures, "iBackup");

        // Subscribe to device monitor events
        _deviceMonitor.DeviceConnected += OnDeviceConnected;
        _deviceMonitor.DeviceDisconnected += OnDeviceDisconnected;

        // Subscribe to backup engine events
        _backupEngine.ProgressChanged += OnBackupProgressChanged;
        _backupEngine.BackupCompleted += OnBackupCompleted;
        _backupEngine.BackupFailed += OnBackupFailed;

        // Subscribe to schedule triggers
        _schedulerService.ScheduledBackupTriggered += OnScheduledBackupTriggered;

        // Check for already connected devices on startup
        CheckInitialDeviceConnection();

        // Load historical stats and update drive status
        _ = LoadInitialStatsAsync();
        UpdateStorageAndScheduleInfo();
    }

    private void UpdateStorageAndScheduleInfo()
    {
        // Destination disk space
        if (!string.IsNullOrWhiteSpace(DestinationPath))
        {
            if (BackupEngine.HasSufficientDiskSpace(DestinationPath, 0, out long freeBytes))
            {
                DestinationFreeSpaceSummary = $"{FileSizeConverter.FormatBytes(freeBytes)} free on target drive";
            }
        }

        // Schedule countdown
        var next = _schedulerService.CurrentSchedule.CalculateNextRunTime(DateTime.Now);
        NextScheduledBackupSummary = next.HasValue
            ? $"Next scheduled sync: {next.Value:g}"
            : "No schedule active";
    }

    private void OnScheduledBackupTriggered(object? sender, DateTime triggerTime)
    {
        RunOnUIThread(() =>
        {
            if (IsDeviceConnected && !IsBackingUp && CanStartBackup)
            {
                ShowMessage("Scheduled backup starting", "Executing scheduled automatic media backup...", InfoBarSeverity.Informational);
                _ = StartBackupAsync();
            }
            else if (!IsDeviceConnected)
            {
                ShowMessage("Scheduled backup ready", "A scheduled backup is due. Connect your iPhone via USB or Wi-Fi to begin.", InfoBarSeverity.Informational);
            }
        });
    }

    /// <summary>
    /// Checks if a device is already connected at launch time.
    /// </summary>
    private void CheckInitialDeviceConnection()
    {
        try
        {
            var connectedDevices = _deviceMonitor.GetConnectedDevices();
            if (connectedDevices != null && connectedDevices.Count > 0)
            {
                var device = connectedDevices[0];
                ConnectedDevice = device;
                IsDeviceConnected = true;
                StatusText = $"Connected to {device.Name}";
                ActivityStage = "Ready";
                ConnectionModeText = device.Model.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ? "Wi-Fi Sync" : "USB Connection";

                if (device.TotalStorageBytes > 0)
                {
                    DeviceStoragePercent = Math.Clamp((double)device.UsedStorageBytes / device.TotalStorageBytes * 100.0, 0.0, 100.0);
                    DeviceStorageFormatted = device.FriendlyStorageSummary;
                }

                ShowMessage(
                    "iPhone ready",
                    "Keep the iPhone unlocked and trusted while scanning and copying media.",
                    InfoBarSeverity.Success);
                _ = LoadDeviceSettingsAsync(device.Udid);
            }
        }
        catch
        {
            IsDeviceConnected = false;
        }
    }

    /// <summary>
    /// Loads saved settings for the specified device.
    /// </summary>
    private async Task LoadDeviceSettingsAsync(string udid)
    {
        try
        {
            var settings = await _manifestRepository.GetDeviceSettingsAsync(udid)
                ?? await _manifestRepository.GetDeviceSettingsAsync("global_default");

            if (settings != null)
            {
                RunOnUIThread(() =>
                {
                    if (!string.IsNullOrWhiteSpace(settings.DestinationPath))
                    {
                        DestinationPath = settings.DestinationPath;
                    }
                    AutoBackupOnConnect = settings.AutoBackup;
                    UpdateStorageAndScheduleInfo();
                });
            }
        }
        catch
        {
            // Ignored - use defaults
        }
    }

    /// <summary>
    /// Loads initial backup statistics from the manifest repository.
    /// </summary>
    private async Task LoadInitialStatsAsync()
    {
        try
        {
            if (ConnectedDevice == null)
            {
                return;
            }

            var stats = await _manifestRepository.GetStatsAsync(ConnectedDevice.Udid);
            if (stats.TotalSessions > 0)
            {
                RunOnUIThread(() =>
                {
                    LastBackupSummary = $"Total backups: {stats.TotalSessions:N0} | {stats.TotalBackedUpFiles:N0} files ({FileSizeConverter.FormatBytes(stats.TotalBackedUpBytes)})";
                });
            }
        }
        catch
        {
            // Ignored - use default summary
        }
    }

    private bool CanStartBackup => IsDeviceConnected && !IsBackingUp && !string.IsNullOrWhiteSpace(DestinationPath);

    private bool CanPauseResume => IsBackingUp;

    private bool CanCancelBackup => IsBackingUp;

    /// <summary>
    /// Starts the backup operation for the currently connected device.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartBackup))]
    public async Task StartBackupAsync()
    {
        if (IsBackingUp || _backupEngine.IsRunning) return;
        if (ConnectedDevice == null || string.IsNullOrWhiteSpace(DestinationPath))
        {
            ShowMessage("Choose a backup folder", "Select a writable destination before starting the backup.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            try
            {
                Directory.CreateDirectory(DestinationPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                ActivityStage = "Needs attention";
                StatusText = "Backup folder is not available.";
                ShowMessage(
                    "Choose another backup folder",
                    $"iBackup could not use “{DestinationPath}”. Select a local folder or connected drive and try again.",
                    InfoBarSeverity.Error);
                return;
            }

            IsBackingUp = true;
            IsPaused = false;
            IsScanning = true;
            IsMessageOpen = false;
            ActivityStage = "Scanning";
            StartButtonText = "Scanning...";
            ProgressPercent = 0;
            FilesProgress = "Looking for media";
            BytesProgress = "Not started";
            EstimatedTimeRemaining = "After scan";
            StatusText = $"Scanning media on {ConnectedDevice.Name}...";

            _backupCts = new CancellationTokenSource();

            // Load saved user preferences from SQLite
            var savedSettings = await _manifestRepository.GetDeviceSettingsAsync(ConnectedDevice.Udid)
                ?? await _manifestRepository.GetDeviceSettingsAsync("global_default");

            var settings = new BackupSettings
            {
                DeviceUdid = ConnectedDevice.Udid,
                DestinationPath = DestinationPath,
                AutoBackup = AutoBackupOnConnect,
                OrganizationMode = savedSettings?.OrganizationMode ?? FileOrganizationMode.ByYearMonth,
                IncludePhotos = savedSettings?.IncludePhotos ?? true,
                IncludeVideos = savedSettings?.IncludeVideos ?? true,
                IncludeRaw = savedSettings?.IncludeRaw ?? true,
                IncludeScreenshots = savedSettings?.IncludeScreenshots ?? true,
                VerifyIntegrity = savedSettings?.VerifyIntegrity ?? false,
                MinimizeToTray = MinimizeToTray
            };

            // Save device settings
            try
            {
                await _manifestRepository.SaveDeviceSettingsAsync(settings);
            }
            catch
            {
                // Non-fatal
            }

            var device = ConnectedDevice;
            var token = _backupCts.Token;

            await Task.Run(() => _backupEngine.StartBackupAsync(device, settings, token));
        }
        catch (OperationCanceledException)
        {
            ActivityStage = "Cancelled";
            StatusText = "Backup was cancelled.";
            ShowMessage("Backup cancelled", "Completed copies remain in your backup folder. You can retry the remaining files.", InfoBarSeverity.Warning);
        }
        catch (Exception ex)
        {
            ActivityStage = "Needs attention";
            StatusText = $"Backup error: {ex.Message}";
            ShowMessage("Backup could not continue", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsBackingUp = false;
            IsScanning = false;
            IsPaused = false;
            StartButtonText = "Start Backup";
            UpdateStorageAndScheduleInfo();
        }
    }

    [RelayCommand(CanExecute = nameof(CanPauseResume))]
    public void PauseResume()
    {
        if (!IsBackingUp) return;

        if (IsPaused)
        {
            _backupEngine.Resume();
            IsPaused = false;
            ActivityStage = "Copying";
            StatusText = "Backup resumed.";
        }
        else
        {
            _backupEngine.Pause();
            IsPaused = true;
            ActivityStage = "Paused";
            StatusText = "Backup paused.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelBackup))]
    public void CancelBackup()
    {
        if (!IsBackingUp) return;

        ActivityStage = "Cancelling";
        StatusText = "Cancelling backup...";
        _backupEngine.Cancel();
        _backupCts?.Cancel();
    }

    [RelayCommand]
    public void BrowseDestination()
    {
        RequestBrowseFolder?.Invoke();
    }

    [RelayCommand]
    public void OpenDestination()
    {
        OpenFolder(DestinationPath, "Choose a backup folder first.");
    }

    [RelayCommand]
    public void OpenLogs()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDirectory = Path.Combine(localAppData, "iBackup", "logs");
        OpenFolder(logDirectory, "Diagnostic logs are not available yet.");
    }

    private void OnDeviceConnected(object? sender, DeviceInfo device)
    {
        RunOnUIThread(() =>
        {
            ConnectedDevice = device;
            IsDeviceConnected = true;
            StatusText = $"Connected to {device.Name}";
            ActivityStage = "Ready";
            ConnectionModeText = device.Model.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ? "Wi-Fi Sync" : "USB Connection";

            if (device.TotalStorageBytes > 0)
            {
                DeviceStoragePercent = Math.Clamp((double)device.UsedStorageBytes / device.TotalStorageBytes * 100.0, 0.0, 100.0);
                DeviceStorageFormatted = device.FriendlyStorageSummary;
            }

            ShowMessage(
                "iPhone connected",
                "Keep the iPhone unlocked and trusted while scanning and copying media.",
                InfoBarSeverity.Success);

            _ = LoadAndMaybeStartAsync(device.Udid);
            _ = LoadInitialStatsAsync();
        });
    }

    private void OnDeviceDisconnected(object? sender, string deviceId)
    {
        RunOnUIThread(() =>
        {
            if (ConnectedDevice?.Udid == deviceId)
            {
                if (IsBackingUp)
                {
                    CancelBackup();
                }

                ConnectedDevice = null;
                IsDeviceConnected = false;
                StatusText = "Device disconnected. Reconnect your iPhone to continue.";
                ActivityStage = "Disconnected";
                DeviceStorageFormatted = string.Empty;
                DeviceStoragePercent = 0;

                ShowMessage(
                    "iPhone disconnected",
                    "The device was unplugged or lost connection. Reconnect it and keep it unlocked to resume.",
                    InfoBarSeverity.Warning);
            }
        });
    }

    private void OnBackupProgressChanged(object? sender, BackupProgress progress)
    {
        RunOnUIThread(() =>
        {
            IsScanning = progress.IsScanning;

            if (progress.IsScanning)
            {
                ActivityStage = "Scanning";
                StartButtonText = "Scanning...";
                StatusText = string.IsNullOrWhiteSpace(progress.CurrentFileName)
                    ? "Scanning for media files..."
                    : $"Found {progress.FilesCompleted:N0} files...";
                FilesProgress = $"{progress.FilesCompleted:N0} found";
                BytesProgress = "Calculating";
                EstimatedTimeRemaining = "Scanning";
                ProgressPercent = 0;
                return;
            }

            ActivityStage = IsPaused ? "Paused" : "Copying";
            StartButtonText = "Backing up...";

            if (progress.TotalFiles > 0)
            {
                ProgressPercent = Math.Min(100.0, Math.Max(0.0, (double)progress.FilesCompleted / progress.TotalFiles * 100.0));
                FilesProgress = $"{progress.FilesCompleted:N0} / {progress.TotalFiles:N0}";
            }

            if (progress.TotalBytes > 0)
            {
                BytesProgress = $"{FileSizeConverter.FormatBytes(progress.BytesCompleted)} / {FileSizeConverter.FormatBytes(progress.TotalBytes)}";
            }

            EstimatedTimeRemaining = progress.EstimatedSecondsRemaining.HasValue
                ? TimeSpan.FromSeconds(progress.EstimatedSecondsRemaining.Value).ToString(@"mm\:ss")
                : "--:--";

            if (!string.IsNullOrWhiteSpace(progress.CurrentFileName))
            {
                StatusText = $"Backing up: {progress.CurrentFileName}";
            }
        });
    }

    private void OnBackupCompleted(object? sender, BackupSession session)
    {
        RunOnUIThread(() =>
        {
            IsBackingUp = false;
            IsScanning = false;
            IsPaused = false;
            ProgressPercent = 100;
            ActivityStage = session.FailedFiles > 0 ? "Completed with issues" : "Complete";
            StatusText = $"Backup complete. {session.NewFiles:N0} new files copied, {session.SkippedFiles:N0} already backed up.";
            LastBackupSummary = $"Last backup: {session.StartedAt:g} - {session.NewFiles:N0} files ({FileSizeConverter.FormatBytes(session.TotalBytes)})";
            ShowMessage(
                session.FailedFiles > 0 ? "Backup completed with issues" : "Backup complete",
                $"Copied {session.NewFiles:N0}, skipped {session.SkippedFiles:N0}, failed {session.FailedFiles:N0}.",
                session.FailedFiles > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
            _ = LoadInitialStatsAsync();
        });
    }

    private void OnBackupFailed(object? sender, Exception ex)
    {
        RunOnUIThread(() =>
        {
            IsBackingUp = false;
            IsScanning = false;
            IsPaused = false;
            ActivityStage = "Needs attention";
            StatusText = $"Backup failed: {ex.Message}";
            ShowMessage("Backup failed", ex.Message, InfoBarSeverity.Error);
        });
    }

    private void ShowMessage(string title, string message, InfoBarSeverity severity)
    {
        MessageTitle = title;
        MessageText = message;
        MessageSeverity = severity;
        IsMessageOpen = true;
    }

    private async Task LoadAndMaybeStartAsync(string udid)
    {
        await LoadDeviceSettingsAsync(udid);
        if (ConnectedDevice?.Udid == udid && AutoBackupOnConnect && CanStartBackup)
            await StartBackupAsync();
    }

    private void OpenFolder(string path, string errorMessage)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                ShowMessage("Folder unavailable", errorMessage, InfoBarSeverity.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowMessage("Could not open folder", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void RunOnUIThread(Action action)
    {
        if (_dispatcherQueue != null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
        else
        {
            action();
        }
    }
}
