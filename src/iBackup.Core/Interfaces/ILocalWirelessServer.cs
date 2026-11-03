namespace iBackup.Core.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides a zero-configuration embedded local Wi-Fi synchronization server,
/// allowing iOS devices on the same local network to back up photos and videos cable-free.
/// </summary>
public interface ILocalWirelessServer : IDisposable
{
    /// <summary>
    /// Event raised when an iOS client connects to the wireless companion endpoint.
    /// </summary>
    event EventHandler<string>? ClientConnected;

    /// <summary>
    /// Event raised during active file upload streaming.
    /// </summary>
    event EventHandler<(string FileName, long BytesReceived, long TotalBytes)>? UploadProgress;

    /// <summary>
    /// Event raised when a file is successfully received and persisted to disk.
    /// </summary>
    event EventHandler<string>? FileReceived;

    /// <summary>
    /// Gets a value indicating whether the local wireless server is actively listening.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the port number the server is listening on.
    /// </summary>
    int Port { get; }

    /// <summary>
    /// Gets the primary local IPv4 address of this machine on the LAN.
    /// </summary>
    string LocalIpAddress { get; }

    /// <summary>
    /// Gets the 4-digit security pairing PIN for the current session.
    /// </summary>
    string PairingPin { get; }

    /// <summary>
    /// Gets the full HTTP URL for pairing (e.g., http://192.168.1.50:52424/?pin=1234).
    /// </summary>
    string ServerUrl { get; }

    /// <summary>
    /// Starts the wireless companion listener.
    /// </summary>
    /// <param name="destinationRootPath">The directory where incoming files will be saved.</param>
    /// <param name="port">The local port to bind (default 52424).</param>
    /// <param name="ct">Cancellation token.</param>
    Task StartAsync(string destinationRootPath, int port = 52424, CancellationToken ct = default);

    /// <summary>
    /// Stops the wireless companion listener.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Generates a standalone SVG QR Code representation of the pairing URL.
    /// </summary>
    /// <returns>An SVG XML string representing the QR code.</returns>
    string GenerateQrCodeSvg();
}

