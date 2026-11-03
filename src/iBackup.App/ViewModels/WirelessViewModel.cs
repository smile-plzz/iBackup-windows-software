namespace iBackup.App.ViewModels;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iBackup.Core.Interfaces;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;

/// <summary>
/// ViewModel managing the local Wi-Fi wireless companion server,
/// QR pairing generation, connection events, and incoming wireless file transfers.
/// </summary>
public partial class WirelessViewModel : ObservableObject
{
    private readonly ILocalWirelessServer _wirelessServer;
    private readonly IManifestRepository _manifestRepository;
    private readonly DispatcherQueue? _dispatcherQueue;

    [ObservableProperty]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private string _localIpAddress = string.Empty;

    [ObservableProperty]
    private int _port = 52424;

    [ObservableProperty]
    private string _pairingPin = string.Empty;

    [ObservableProperty]
    private string _qrCodeSvg = string.Empty;

    [ObservableProperty]
    private bool _isServerRunning;

    [ObservableProperty]
    private int _filesReceivedCount;

    [ObservableProperty]
    private string _statusMessage = "Ready for Wi-Fi pairing";

    [ObservableProperty]
    private string _recentActivityText = "Scan the QR code with your iPhone Camera to connect.";

    /// <summary>
    /// Initializes a new instance of the <see cref="WirelessViewModel"/> class.
    /// </summary>
    public WirelessViewModel(
        ILocalWirelessServer wirelessServer,
        IManifestRepository manifestRepository)
    {
        _wirelessServer = wirelessServer ?? throw new ArgumentNullException(nameof(wirelessServer));
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        UpdateServerDetails();

        _wirelessServer.ClientConnected += OnClientConnected;
        _wirelessServer.UploadProgress += OnUploadProgress;
        _wirelessServer.FileReceived += OnFileReceived;

        _ = EnsureServerStartedAsync();
    }

    private async Task EnsureServerStartedAsync()
    {
        if (!_wirelessServer.IsRunning)
        {
            var defaultPictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string destination = Path.Combine(defaultPictures, "iBackup");

            try
            {
                var settings = await _manifestRepository.GetDeviceSettingsAsync("global_default");
                if (settings != null && !string.IsNullOrWhiteSpace(settings.DestinationPath))
                {
                    destination = settings.DestinationPath;
                }

                await _wirelessServer.StartAsync(destination, Port);
            }
            catch (Exception ex)
            {
                RunOnUIThread(() =>
                {
                    StatusMessage = $"Could not start wireless listener: {ex.Message}";
                });
            }
        }

        RunOnUIThread(UpdateServerDetails);
    }

    private void UpdateServerDetails()
    {
        IsServerRunning = _wirelessServer.IsRunning;
        LocalIpAddress = _wirelessServer.LocalIpAddress;
        Port = _wirelessServer.Port;
        PairingPin = _wirelessServer.PairingPin;
        ServerUrl = _wirelessServer.ServerUrl;
        QrCodeSvg = _wirelessServer.GenerateQrCodeSvg();
    }

    [RelayCommand]
    public async Task ToggleServerAsync()
    {
        if (IsServerRunning)
        {
            await _wirelessServer.StopAsync();
            StatusMessage = "Wireless listener paused";
        }
        else
        {
            var defaultPictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string destination = Path.Combine(defaultPictures, "iBackup");
            await _wirelessServer.StartAsync(destination, Port);
            StatusMessage = "Listening on local Wi-Fi";
        }

        UpdateServerDetails();
    }

    [RelayCommand]
    public void CopyUrl()
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(ServerUrl);
            Clipboard.SetContent(dataPackage);
            StatusMessage = "Pairing link copied to clipboard!";
        }
        catch
        {
            StatusMessage = ServerUrl;
        }
    }

    [RelayCommand]
    public void OpenInBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ServerUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open browser: {ex.Message}";
        }
    }

    private void OnClientConnected(object? sender, string client)
    {
        RunOnUIThread(() =>
        {
            StatusMessage = "Device connected via Wi-Fi";
            RecentActivityText = $"Connected: {client}";
        });
    }

    private void OnUploadProgress(object? sender, (string FileName, long BytesReceived, long TotalBytes) e)
    {
        RunOnUIThread(() =>
        {
            double mb = e.BytesReceived / (1024.0 * 1024.0);
            double totalMb = e.TotalBytes / (1024.0 * 1024.0);
            RecentActivityText = $"Receiving {e.FileName} ({mb:F1} / {totalMb:F1} MB)";
        });
    }

    private void OnFileReceived(object? sender, string fileName)
    {
        RunOnUIThread(() =>
        {
            FilesReceivedCount++;
            StatusMessage = "File saved successfully";
            RecentActivityText = $"Saved: {fileName} ({FilesReceivedCount} files received)";
        });
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

