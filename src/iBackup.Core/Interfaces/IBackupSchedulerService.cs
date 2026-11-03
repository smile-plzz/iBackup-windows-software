namespace iBackup.Core.Interfaces;

using System;
using System.Threading.Tasks;
using iBackup.Core.Models;

/// <summary>
/// Service responsible for managing automated recurring backup schedules,
/// monitoring power and connection states, and triggering scheduled backups.
/// </summary>
public interface IBackupSchedulerService : IDisposable
{
    /// <summary>
    /// Event raised when a scheduled backup interval is reached and ready to execute.
    /// </summary>
    event EventHandler<DateTime>? ScheduledBackupTriggered;

    /// <summary>
    /// Gets the current active schedule configuration.
    /// </summary>
    BackupSchedule CurrentSchedule { get; }

    /// <summary>
    /// Gets a value indicating whether the background scheduler timer loop is active.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Loads saved schedule preferences from repository for the specified device or global configuration.
    /// </summary>
    /// <param name="deviceUdid">The device UDID or global identifier.</param>
    Task LoadScheduleAsync(string deviceUdid = "global_default");

    /// <summary>
    /// Updates and persists schedule configuration.
    /// </summary>
    /// <param name="deviceUdid">The device UDID or global identifier.</param>
    /// <param name="schedule">The updated schedule settings.</param>
    Task UpdateScheduleAsync(string deviceUdid, BackupSchedule schedule);

    /// <summary>
    /// Starts the background scheduling timer loop.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the background scheduling timer loop.
    /// </summary>
    void Stop();

    /// <summary>
    /// Evaluates whether the current schedule is due to run.
    /// </summary>
    /// <param name="now">The current timestamp.</param>
    /// <param name="isOnAcPower">Whether the system is connected to AC power.</param>
    /// <returns>True if backup conditions are satisfied; otherwise false.</returns>
    bool ShouldTrigger(DateTime now, bool isOnAcPower = true);
}

