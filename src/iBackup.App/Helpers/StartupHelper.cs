namespace iBackup.App.Helpers;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

/// <summary>
/// Manages Windows startup registration via HKCU Run registry key.
/// </summary>
public static class StartupHelper
{
    private const string AppName = "iBackup";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Gets a value indicating whether the application is set to start with Windows.
    /// </summary>
    public static bool IsStartupEnabled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets whether the application should start automatically when the user signs into Windows.
    /// </summary>
    /// <param name="enabled">True to enable auto-start; false to disable.</param>
    public static void SetStartupEnabled(bool enabled)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null)
            {
                return;
            }

            if (enabled)
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\" --minimized");
                }
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Silently handle registry access restrictions
        }
    }
}

