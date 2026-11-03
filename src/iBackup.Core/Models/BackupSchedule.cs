namespace iBackup.Core.Models;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Supported automatic backup schedule frequencies.
/// </summary>
public enum ScheduleFrequency
{
    /// <summary>
    /// Automatic scheduled backup is disabled.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Runs once every day at the specified time.
    /// </summary>
    Daily = 1,

    /// <summary>
    /// Runs once every week on the specified day of week and time.
    /// </summary>
    Weekly = 2,

    /// <summary>
    /// Automatically triggers backup whenever device connection is detected.
    /// </summary>
    OnConnect = 3
}

/// <summary>
/// Configuration for automated background backups.
/// </summary>
public class BackupSchedule
{
    /// <summary>
    /// Gets or sets a value indicating whether scheduled backup is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the scheduling frequency.
    /// </summary>
    public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Daily;

    /// <summary>
    /// Gets or sets the target time of day for scheduled execution (default 21:00 / 9:00 PM).
    /// </summary>
    public TimeSpan ScheduledTime { get; set; } = new(21, 0, 0);

    /// <summary>
    /// Gets or sets the day of the week for weekly schedules.
    /// </summary>
    public DayOfWeek ScheduledDay { get; set; } = DayOfWeek.Sunday;

    /// <summary>
    /// Gets or sets a value indicating whether backup requires AC power (prevent battery drain on laptops).
    /// </summary>
    public bool OnlyOnAcPower { get; set; } = true;

    /// <summary>
    /// Gets or sets the last time the schedule ran.
    /// </summary>
    public DateTime? LastRunTime { get; set; }

    /// <summary>
    /// Calculates the next expected execution timestamp based on current time and settings.
    /// </summary>
    /// <param name="now">The reference current time.</param>
    /// <returns>The calculated next execution time, or null if disabled.</returns>
    public DateTime? CalculateNextRunTime(DateTime now)
    {
        if (!IsEnabled || Frequency == ScheduleFrequency.Disabled || Frequency == ScheduleFrequency.OnConnect)
        {
            return null;
        }

        DateTime targetToday = now.Date.Add(ScheduledTime);

        if (Frequency == ScheduleFrequency.Daily)
        {
            return now < targetToday ? targetToday : targetToday.AddDays(1);
        }

        if (Frequency == ScheduleFrequency.Weekly)
        {
            int daysUntilTargetDay = ((int)ScheduledDay - (int)now.DayOfWeek + 7) % 7;
            DateTime candidate = targetToday.AddDays(daysUntilTargetDay);

            if (daysUntilTargetDay == 0 && now >= targetToday)
            {
                candidate = candidate.AddDays(7);
            }

            return candidate;
        }

        return null;
    }
}

