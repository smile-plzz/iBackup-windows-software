namespace iBackup.Core.Models;

/// <summary>
/// Configures backup preferences, schedule, wireless options, and target storage options for a specific iOS device.
/// </summary>
public class BackupSettings
{
    /// <summary>
    /// Gets or sets the unique identifier of the device these settings apply to.
    /// </summary>
    public string DeviceUdid { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base local directory path where backups will be stored.
    /// </summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether automatic backup should trigger when the device connects.
    /// </summary>
    public bool AutoBackup { get; set; }

    /// <summary>
    /// Gets or sets the directory organization structure for backed up files.
    /// </summary>
    public FileOrganizationMode OrganizationMode { get; set; } = FileOrganizationMode.ByYearMonth;

    /// <summary>
    /// Gets or sets a value indicating whether regular photos should be backed up.
    /// </summary>
    public bool IncludePhotos { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether videos should be backed up.
    /// </summary>
    public bool IncludeVideos { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether RAW/DNG media files should be backed up.
    /// </summary>
    public bool IncludeRaw { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether screenshots should be included in the backup.
    /// </summary>
    public bool IncludeScreenshots { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether file integrity should be verified using SHA-256 after transfer.
    /// </summary>
    public bool VerifyIntegrity { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether closing the window should keep the app in the system tray.
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the application should launch on Windows user login.
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the local Wi-Fi wireless companion server is enabled.
    /// </summary>
    public bool EnableWirelessSync { get; set; } = true;

    /// <summary>
    /// Gets or sets the local port used by the wireless companion server.
    /// </summary>
    public int WirelessPort { get; set; } = 52424;

    /// <summary>
    /// Gets or sets the recurring backup schedule settings.
    /// </summary>
    public BackupSchedule Schedule { get; set; } = new();
}
