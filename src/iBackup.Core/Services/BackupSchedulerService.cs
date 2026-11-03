namespace iBackup.Core.Services;

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using iBackup.Core.Interfaces;
using iBackup.Core.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Monitors time of day and power conditions to automatically trigger scheduled backups.
/// </summary>
public sealed class BackupSchedulerService : IBackupSchedulerService
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<BackupSchedulerService> _logger;
    private readonly object _lock = new();

    private CancellationTokenSource? _timerCts;
    private Task? _timerTask;
    private bool _disposed;
    private string _activeUdid = "global_default";

    /// <inheritdoc/>
    public event EventHandler<DateTime>? ScheduledBackupTriggered;

    /// <inheritdoc/>
    public BackupSchedule CurrentSchedule { get; private set; } = new();

    /// <inheritdoc/>
    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _timerTask != null && !_timerTask.IsCompleted;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupSchedulerService"/> class.
    /// </summary>
    /// <param name="manifestRepository">The manifest repository for persistence.</param>
    /// <param name="logger">The logger instance.</param>
    public BackupSchedulerService(
        IManifestRepository manifestRepository,
        ILogger<BackupSchedulerService> logger)
    {
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task LoadScheduleAsync(string deviceUdid = "global_default")
    {
        _activeUdid = deviceUdid;
        try
        {
            var settings = await _manifestRepository.GetDeviceSettingsAsync(deviceUdid);
            if (settings?.Schedule != null)
            {
                CurrentSchedule = settings.Schedule;
            }
            else
            {
                CurrentSchedule = new BackupSchedule();
            }

            _logger.LogInformation("Loaded backup schedule for {DeviceUdid}: Enabled={Enabled}, Freq={Freq}, Time={Time}",
                deviceUdid, CurrentSchedule.IsEnabled, CurrentSchedule.Frequency, CurrentSchedule.ScheduledTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load backup schedule for {DeviceUdid}. Using defaults.", deviceUdid);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateScheduleAsync(string deviceUdid, BackupSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        _activeUdid = deviceUdid;
        CurrentSchedule = schedule;

        try
        {
            var settings = await _manifestRepository.GetDeviceSettingsAsync(deviceUdid) ?? new BackupSettings
            {
                DeviceUdid = deviceUdid
            };

            settings.Schedule = schedule;
            await _manifestRepository.SaveDeviceSettingsAsync(settings);
            _logger.LogInformation("Updated and saved backup schedule for {DeviceUdid}.", deviceUdid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist updated backup schedule for {DeviceUdid}.", deviceUdid);
        }
    }

    /// <inheritdoc/>
    public void Start()
    {
        lock (_lock)
        {
            if (_timerTask != null && !_timerTask.IsCompleted)
            {
                return;
            }

            _timerCts = new CancellationTokenSource();
            var token = _timerCts.Token;
            _timerTask = Task.Run(() => SchedulerLoopAsync(token), CancellationToken.None);
            _logger.LogInformation("Backup scheduler background timer started.");
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_lock)
        {
            if (_timerCts != null)
            {
                _timerCts.Cancel();
                _timerCts.Dispose();
                _timerCts = null;
            }

            _timerTask = null;
            _logger.LogInformation("Backup scheduler background timer stopped.");
        }
    }

    /// <inheritdoc/>
    public bool ShouldTrigger(DateTime now, bool isOnAcPower = true)
    {
        if (!CurrentSchedule.IsEnabled)
        {
            return false;
        }

        if (CurrentSchedule.OnlyOnAcPower && !isOnAcPower)
        {
            _logger.LogDebug("Scheduled backup skipped: device is running on battery.");
            return false;
        }

        if (CurrentSchedule.Frequency == ScheduleFrequency.OnConnect)
        {
            return false; // Handled directly by device connection listener
        }

        // Compare hour and minute with scheduled time
        bool timeMatches = now.TimeOfDay.Hours == CurrentSchedule.ScheduledTime.Hours &&
                           now.TimeOfDay.Minutes == CurrentSchedule.ScheduledTime.Minutes;

        if (!timeMatches)
        {
            return false;
        }

        // Check if already executed in the current minute/day
        if (CurrentSchedule.LastRunTime.HasValue)
        {
            var last = CurrentSchedule.LastRunTime.Value;
            if (last.Date == now.Date && last.Hour == now.Hour && last.Minute == now.Minute)
            {
                return false;
            }
        }

        if (CurrentSchedule.Frequency == ScheduleFrequency.Daily)
        {
            return true;
        }

        if (CurrentSchedule.Frequency == ScheduleFrequency.Weekly)
        {
            return now.DayOfWeek == CurrentSchedule.ScheduledDay;
        }

        return false;
    }

    private async Task SchedulerLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (await timer.WaitForNextTickAsync(ct))
                {
                    DateTime now = DateTime.Now;
                    bool acPower = CheckIsOnAcPower();

                    if (ShouldTrigger(now, acPower))
                    {
                        _logger.LogInformation("Scheduled backup criteria met at {Now}. Raising trigger event.", now);
                        CurrentSchedule.LastRunTime = now;

                        // Persist last run time
                        _ = UpdateScheduleAsync(_activeUdid, CurrentSchedule);

                        ScheduledBackupTriggered?.Invoke(this, now);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in backup scheduler loop.");
            }
        }
    }

    private static bool CheckIsOnAcPower()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                if (GetSystemPowerStatus(out var status))
                {
                    // ACLineStatus: 0 = Offline, 1 = Online, 255 = Unknown
                    return status.ACLineStatus != 0;
                }
            }
            catch
            {
                // Fallback to true if power API is unavailable
            }
        }

        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}

