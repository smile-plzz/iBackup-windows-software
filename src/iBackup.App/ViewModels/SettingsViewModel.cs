namespace iBackup.App.ViewModels;

using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iBackup.App.Helpers;
using iBackup.Core.Interfaces;
using iBackup.Core.Models;
using Microsoft.UI.Dispatching;

/// <summary>
/// ViewModel for application and backup configuration settings.
/// Manages persistence of backup directory, file organization patterns, media filters,
/// automated schedules, and startup behaviors.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private const string GlobalSettingsKey = "global_default";
    private readonly IManifestRepository _manifestRepository;
    private readonly IBackupSchedulerService _schedulerService;
    private readonly DispatcherQueue? _dispatcherQueue;
    private bool _isInitialized;

    [ObservableProperty]
    private string _destinationPath = string.Empty;

    [ObservableProperty]
    private FileOrganizationMode _organizationMode = FileOrganizationMode.ByYearMonth;

    [ObservableProperty]
    private int _selectedOrganizationIndex;

    [ObservableProperty]
    private bool _backupPhotos = true;

    [ObservableProperty]
    private bool _backupVideos = true;

    [ObservableProperty]
    private bool _backupRaw = true;

    [ObservableProperty]
    private bool _backupScreenshots = true;

    [ObservableProperty]
    private bool _verifyIntegrity;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _autoBackup = true;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    // Scheduled Backups
    [ObservableProperty]
    private bool _isScheduleEnabled;

    [ObservableProperty]
    private int _selectedScheduleFrequencyIndex = 0; // 0 = Daily, 1 = Weekly, 2 = On Connection

    [ObservableProperty]
    private TimeSpan _scheduleTime = new(21, 0, 0); // 9:00 PM

    [ObservableProperty]
    private bool _scheduleOnlyOnAcPower = true;

    [ObservableProperty]
    private string _scheduleSummaryText = "Automatic daily backup at 21:00";

    // Wireless Sync Settings
    [ObservableProperty]
    private bool _enableWirelessSync = true;

    [ObservableProperty]
    private int _wirelessPort = 52424;

    [ObservableProperty]
    private string _appVersion = $"Version {typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.2.0"} · Preview";

    [ObservableProperty]
    private string _gitHubUrl = "https://github.com/smile-plzz/iBackup";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    public SettingsViewModel(
        IManifestRepository manifestRepository,
        IBackupSchedulerService schedulerService)
    {
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        var defaultPictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        DestinationPath = Path.Combine(defaultPictures, "iBackup");

        StartWithWindows = StartupHelper.IsStartupEnabled();

        _ = LoadSettingsAsync();
    }

    /// <summary>
    /// Loads saved settings asynchronously from repository.
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _manifestRepository.GetDeviceSettingsAsync(GlobalSettingsKey);
            if (settings != null)
            {
                RunOnUIThread(() =>
                {
                    if (!string.IsNullOrWhiteSpace(settings.DestinationPath))
                    {
                        DestinationPath = settings.DestinationPath;
                    }

                    OrganizationMode = settings.OrganizationMode;
                    SelectedOrganizationIndex = (int)settings.OrganizationMode;
                    BackupPhotos = settings.IncludePhotos;
                    BackupVideos = settings.IncludeVideos;
                    BackupRaw = settings.IncludeRaw;
                    BackupScreenshots = settings.IncludeScreenshots;
                    VerifyIntegrity = settings.VerifyIntegrity;
                    AutoBackup = settings.AutoBackup;
                    MinimizeToTray = settings.MinimizeToTray;
                    StartWithWindows = settings.StartWithWindows;
                    EnableWirelessSync = settings.EnableWirelessSync;
                    WirelessPort = settings.WirelessPort;

                    if (settings.Schedule != null)
                    {
                        IsScheduleEnabled = settings.Schedule.IsEnabled;
                        SelectedScheduleFrequencyIndex = settings.Schedule.Frequency switch
                        {
                            ScheduleFrequency.Daily => 0,
                            ScheduleFrequency.Weekly => 1,
                            ScheduleFrequency.OnConnect => 2,
                            _ => 0
                        };
                        ScheduleTime = settings.Schedule.ScheduledTime;
                        ScheduleOnlyOnAcPower = settings.Schedule.OnlyOnAcPower;
                    }

                    UpdateScheduleSummary();
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load settings: {ex.Message}";
        }
        finally
        {
            _isInitialized = true;
        }
    }

    private void UpdateScheduleSummary()
    {
        if (!IsScheduleEnabled)
        {
            ScheduleSummaryText = "Scheduled backup is currently disabled.";
            return;
        }

        string timeStr = $"{ScheduleTime.Hours:D2}:{ScheduleTime.Minutes:D2}";
        ScheduleSummaryText = SelectedScheduleFrequencyIndex switch
        {
            0 => $"Runs every day at {timeStr} (AC power required: {(ScheduleOnlyOnAcPower ? "Yes" : "No")})",
            1 => $"Runs every Sunday at {timeStr}",
            2 => "Runs automatically whenever your iPhone connects",
            _ => "Runs on configured schedule"
        };
    }

    partial void OnSelectedOrganizationIndexChanged(int value)
    {
        OrganizationMode = value switch
        {
            0 => FileOrganizationMode.ByYearMonth,
            1 => FileOrganizationMode.ByYearMonthDay,
            2 => FileOrganizationMode.Flat,
            _ => FileOrganizationMode.ByYearMonth
        };

        AutoSaveIfInitialized();
    }

    partial void OnBackupPhotosChanged(bool value) => AutoSaveIfInitialized();
    partial void OnBackupVideosChanged(bool value) => AutoSaveIfInitialized();
    partial void OnBackupRawChanged(bool value) => AutoSaveIfInitialized();
    partial void OnBackupScreenshotsChanged(bool value) => AutoSaveIfInitialized();
    partial void OnVerifyIntegrityChanged(bool value) => AutoSaveIfInitialized();
    partial void OnAutoBackupChanged(bool value) => AutoSaveIfInitialized();
    partial void OnMinimizeToTrayChanged(bool value) => AutoSaveIfInitialized();
    partial void OnEnableWirelessSyncChanged(bool value) => AutoSaveIfInitialized();
    partial void OnWirelessPortChanged(int value) => AutoSaveIfInitialized();

    partial void OnIsScheduleEnabledChanged(bool value)
    {
        UpdateScheduleSummary();
        AutoSaveIfInitialized();
    }

    partial void OnSelectedScheduleFrequencyIndexChanged(int value)
    {
        UpdateScheduleSummary();
        AutoSaveIfInitialized();
    }

    partial void OnScheduleTimeChanged(TimeSpan value)
    {
        UpdateScheduleSummary();
        AutoSaveIfInitialized();
    }

    partial void OnScheduleOnlyOnAcPowerChanged(bool value)
    {
        UpdateScheduleSummary();
        AutoSaveIfInitialized();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        StartupHelper.SetStartupEnabled(value);
        AutoSaveIfInitialized();
    }

    private void AutoSaveIfInitialized()
    {
        if (_isInitialized)
        {
            _ = SaveSettingsAsync();
        }
    }

    /// <summary>
    /// Explicitly saves the current settings to repository and updates scheduler.
    /// </summary>
    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        try
        {
            var schedule = new BackupSchedule
            {
                IsEnabled = IsScheduleEnabled,
                Frequency = SelectedScheduleFrequencyIndex switch
                {
                    0 => ScheduleFrequency.Daily,
                    1 => ScheduleFrequency.Weekly,
                    2 => ScheduleFrequency.OnConnect,
                    _ => ScheduleFrequency.Daily
                },
                ScheduledTime = ScheduleTime,
                OnlyOnAcPower = ScheduleOnlyOnAcPower
            };

            var settings = new BackupSettings
            {
                DestinationPath = DestinationPath,
                OrganizationMode = OrganizationMode,
                DeviceUdid = GlobalSettingsKey,
                IncludePhotos = BackupPhotos,
                IncludeVideos = BackupVideos,
                IncludeRaw = BackupRaw,
                IncludeScreenshots = BackupScreenshots,
                VerifyIntegrity = VerifyIntegrity,
                AutoBackup = AutoBackup,
                MinimizeToTray = MinimizeToTray,
                StartWithWindows = StartWithWindows,
                EnableWirelessSync = EnableWirelessSync,
                WirelessPort = WirelessPort,
                Schedule = schedule
            };

            await _manifestRepository.SaveDeviceSettingsAsync(settings);
            await _schedulerService.UpdateScheduleAsync(GlobalSettingsKey, schedule);

            StatusMessage = "Settings saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
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
