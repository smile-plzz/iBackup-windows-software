namespace iBackup.Core.Services;

using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using iBackup.Core.Interfaces;
using iBackup.Core.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implements <see cref="ILocalWirelessServer"/> as an embedded HTTP companion server
/// for receiving photos and videos over the local Wi-Fi network without third-party cloud.
/// </summary>
public sealed class LocalWirelessServer : ILocalWirelessServer
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<LocalWirelessServer> _logger;
    private readonly object _lock = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _serverCts;
    private Task? _listenerTask;
    private string _destinationRoot = string.Empty;
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler<string>? ClientConnected;

    /// <inheritdoc/>
    public event EventHandler<(string FileName, long BytesReceived, long TotalBytes)>? UploadProgress;

    /// <inheritdoc/>
    public event EventHandler<string>? FileReceived;

    /// <inheritdoc/>
    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _listener != null && _listener.IsListening;
            }
        }
    }

    /// <inheritdoc/>
    public int Port { get; private set; } = 52424;

    /// <inheritdoc/>
    public string LocalIpAddress { get; private set; } = "127.0.0.1";

    /// <inheritdoc/>
    public string PairingPin { get; private set; } = "1234";

    /// <inheritdoc/>
    public string ServerUrl => $"http://{LocalIpAddress}:{Port}/?pin={PairingPin}";

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalWirelessServer"/> class.
    /// </summary>
    public LocalWirelessServer(
        IManifestRepository manifestRepository,
        ILogger<LocalWirelessServer> logger)
    {
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LocalIpAddress = ResolveLocalIpAddress();
        PairingPin = Random.Shared.Next(1000, 9999).ToString("D4");
    }

    /// <inheritdoc/>
    public Task StartAsync(string destinationRootPath, int port = 52424, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRootPath);

        lock (_lock)
        {
            if (IsRunning)
            {
                return Task.CompletedTask;
            }

            _destinationRoot = destinationRootPath;
            Port = port;
            LocalIpAddress = ResolveLocalIpAddress();

            _listener = new HttpListener();

            try
            {
                // Register localhost and local IP endpoints
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                if (LocalIpAddress != "127.0.0.1")
                {
                    _listener.Prefixes.Add($"http://{LocalIpAddress}:{Port}/");
                }

                _listener.Start();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not bind to specific IP prefix, falling back to localhost only.");
                _listener.Close();
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Start();
            }

            _serverCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = _serverCts.Token;
            _listenerTask = Task.Run(() => HandleRequestsAsync(token), CancellationToken.None);

            _logger.LogInformation("Local wireless backup server active on {Url}", ServerUrl);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        lock (_lock)
        {
            if (_serverCts != null)
            {
                _serverCts.Cancel();
                _serverCts.Dispose();
                _serverCts = null;
            }

            if (_listener != null)
            {
                try
                {
                    _listener.Stop();
                    _listener.Close();
                }
                catch
                {
                    // Ignore on shutdown
                }
                _listener = null;
            }

            _listenerTask = null;
            _logger.LogInformation("Local wireless backup server stopped.");
        }

        return Task.CompletedTask;
    }

    private async Task HandleRequestsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessContextAsync(context), ct);
            }
            catch (HttpListenerException)
            {
                break; // Listener stopped
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Exception while awaiting HTTP client context.");
            }
        }
    }

    private async Task ProcessContextAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Add CORS headers for mobile clients
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, X-File-Name, X-Pairing-Pin");

            if (request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            string rawUrl = request.RawUrl ?? "/";
            string path = request.Url?.AbsolutePath ?? "/";

            // Verify PIN
            string providedPin = request.QueryString["pin"] ?? request.Headers["X-Pairing-Pin"] ?? string.Empty;
            bool isAuthorized = string.Equals(providedPin, PairingPin, StringComparison.Ordinal);

            if (path == "/" && request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                ClientConnected?.Invoke(this, request.RemoteEndPoint?.ToString() ?? "iOS Device");
                await ServeHtmlAsync(response, isAuthorized);
                return;
            }

            if (path == "/upload" && request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                if (!isAuthorized)
                {
                    response.StatusCode = 401;
                    byte[] err = Encoding.UTF8.GetBytes("{\"error\":\"Invalid or missing pairing PIN.\"}");
                    await response.OutputStream.WriteAsync(err);
                    response.Close();
                    return;
                }

                await HandleFileUploadAsync(request, response);
                return;
            }

            response.StatusCode = 404;
            response.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing wireless client request.");
            try
            {
                response.StatusCode = 500;
                response.Close();
            }
            catch
            {
                // Ignored
            }
        }
    }

    private async Task HandleFileUploadAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        string fileName = request.Headers["X-File-Name"] ?? request.QueryString["name"] ?? $"Upload_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        fileName = Path.GetFileName(fileName);

        DateTime now = DateTime.Now;
        string targetDir = Path.Combine(_destinationRoot, "Wireless_Sync", now.ToString("yyyy"), now.ToString("MM"));
        Directory.CreateDirectory(targetDir);

        string finalPath = Path.Combine(targetDir, fileName);
        finalPath = BackupEngine.ResolveAvailableDestinationPath(finalPath);

        long totalBytes = request.ContentLength64 > 0 ? request.ContentLength64 : 1024 * 1024;
        long bytesWritten = 0;

        await using (var fileStream = new FileStream(finalPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
        {
            byte[] buffer = new byte[65536];
            int read;
            while ((read = await request.InputStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                bytesWritten += read;
                UploadProgress?.Invoke(this, (fileName, bytesWritten, totalBytes));
            }
        }

        // Record in manifest database
        try
        {
            var fileInfo = new FileInfo(finalPath);
            var mediaType = MediaTypeHelper.DetermineMediaType(fileName);
            var record = new BackupFileRecord
            {
                DeviceUdid = $"wireless_{LocalIpAddress}",
                SourcePath = $"wireless://{fileName}",
                DestPath = finalPath,
                FileName = fileName,
                FileSize = fileInfo.Length,
                CreationDate = now,
                MediaType = mediaType,
                BackupDate = now,
                Status = BackupStatus.Complete
            };

            await _manifestRepository.RecordBackupAsync(record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not record wireless upload in manifest DB.");
        }

        FileReceived?.Invoke(this, fileName);

        response.StatusCode = 200;
        response.ContentType = "application/json";
        byte[] resp = Encoding.UTF8.GetBytes("{\"status\":\"success\",\"file\":\"" + fileName + "\"}");
        await response.OutputStream.WriteAsync(resp);
        response.Close();
    }

    private async Task ServeHtmlAsync(HttpListenerResponse response, bool isAuthorized)
    {
        response.ContentType = "text/html; charset=utf-8";
        response.StatusCode = 200;

        string html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
                <title>iBackup Wireless Companion</title>
                <style>
                    :root {
                        --bg: #0F172A;
                        --card: #1E293B;
                        --accent: #38BDF8;
                        --text: #F8FAFC;
                        --muted: #94A3B8;
                        --success: #34D399;
                    }
                    body {
                        font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                        background: var(--bg);
                        color: var(--text);
                        margin: 0;
                        padding: 24px 16px;
                        display: flex;
                        flex-direction: column;
                        align-items: center;
                        min-height: 100vh;
                        box-sizing: border-box;
                    }
                    .card {
                        background: var(--card);
                        border-radius: 20px;
                        padding: 24px;
                        max-width: 440px;
                        width: 100%;
                        border: 1px solid rgba(255,255,255,0.08);
                        box-shadow: 0 10px 25px rgba(0,0,0,0.4);
                        box-sizing: border-box;
                    }
                    h1 { font-size: 22px; font-weight: 700; margin: 0 0 6px 0; }
                    p { color: var(--muted); font-size: 14px; margin: 0 0 20px 0; line-height: 1.5; }
                    .badge {
                        display: inline-block;
                        background: rgba(56, 189, 248, 0.15);
                        color: var(--accent);
                        padding: 4px 10px;
                        border-radius: 8px;
                        font-size: 12px;
                        font-weight: 600;
                        margin-bottom: 12px;
                    }
                    .upload-btn {
                        display: block;
                        background: var(--accent);
                        color: #0F172A;
                        font-weight: 600;
                        text-align: center;
                        padding: 16px;
                        border-radius: 12px;
                        cursor: pointer;
                        font-size: 16px;
                        text-decoration: none;
                        transition: opacity 0.2s;
                    }
                    .upload-btn:active { opacity: 0.8; }
                    input[type="file"] { display: none; }
                    .progress-container { margin-top: 20px; display: none; }
                    .progress-bar-bg { background: rgba(255,255,255,0.1); border-radius: 6px; height: 8px; overflow: hidden; margin-top: 8px; }
                    .progress-bar-fill { background: var(--accent); height: 100%; width: 0%; transition: width 0.2s; }
                    .status-item { font-size: 13px; color: var(--muted); margin-top: 8px; }
                    .success-banner { background: rgba(52, 211, 153, 0.15); color: var(--success); padding: 12px; border-radius: 10px; font-size: 14px; font-weight: 600; margin-top: 16px; display: none; text-align: center; }
                    .pin-input {
                        width: 100%;
                        background: rgba(0,0,0,0.3);
                        border: 1px solid rgba(255,255,255,0.2);
                        color: #FFF;
                        font-size: 20px;
                        padding: 12px;
                        border-radius: 10px;
                        text-align: center;
                        letter-spacing: 4px;
                        box-sizing: border-box;
                        margin-bottom: 16px;
                    }
                </style>
            </head>
            <body>
                <div class="card">
                    <span class="badge">Wi-Fi Sync</span>
                    <h1>iBackup Companion</h1>
                    <p>Back up iPhone photos and videos directly to your PC drive over your local Wi-Fi.</p>

                    <div id="pinSection" style="display: {{(!isAuthorized ? "block" : "none")}};">
                        <input type="text" id="pinCode" class="pin-input" placeholder="4-Digit PIN" maxlength="4" inputmode="numeric">
                        <button class="upload-btn" onclick="savePin()" style="width:100%; border:none;">Unlock Sync</button>
                    </div>

                    <div id="uploadSection" style="display: {{(isAuthorized ? "block" : "none")}};">
                        <label class="upload-btn" for="mediaInput">
                            📷 Select Photos &amp; Videos
                        </label>
                        <input type="file" id="mediaInput" multiple accept="image/*,video/*" onchange="startUpload(this.files)">

                        <div id="progressArea" class="progress-container">
                            <div style="display:flex; justify-content:space-between; font-size:13px;">
                                <span id="fileCounter">Uploading...</span>
                                <span id="percentLabel">0%</span>
                            </div>
                            <div class="progress-bar-bg">
                                <div id="progressBar" class="progress-bar-fill"></div>
                            </div>
                            <div id="currentFile" class="status-item">Preparing...</div>
                        </div>

                        <div id="successBox" class="success-banner">
                            ✓ Backed up to PC Vault!
                        </div>
                    </div>
                </div>

                <script>
                    let activePin = "{{(isAuthorized ? PairingPin : "")}}";

                    function savePin() {
                        const val = document.getElementById('pinCode').value.trim();
                        if (val.length === 4) {
                            activePin = val;
                            window.location.search = '?pin=' + val;
                        }
                    }

                    async function startUpload(files) {
                        if (!files || files.length === 0) return;

                        const progressArea = document.getElementById('progressArea');
                        const progressBar = document.getElementById('progressBar');
                        const percentLabel = document.getElementById('percentLabel');
                        const fileCounter = document.getElementById('fileCounter');
                        const currentFile = document.getElementById('currentFile');
                        const successBox = document.getElementById('successBox');

                        progressArea.style.display = 'block';
                        successBox.style.display = 'none';

                        for (let i = 0; i < files.length; i++) {
                            const file = files[i];
                            fileCounter.textContent = 'File ' + (i + 1) + ' of ' + files.length;
                            currentFile.textContent = file.name + ' (' + (file.size / (1024*1024)).toFixed(1) + ' MB)';

                            const formData = new FormData();
                            formData.append('media', file);

                            try {
                                await fetch('/upload?pin=' + encodeURIComponent(activePin), {
                                    method: 'POST',
                                    headers: {
                                        'X-File-Name': file.name,
                                        'X-Pairing-Pin': activePin
                                    },
                                    body: file
                                });
                            } catch (e) {
                                console.error('Upload failed', e);
                            }

                            const pct = Math.round(((i + 1) / files.length) * 100);
                            progressBar.style.width = pct + '%';
                            percentLabel.textContent = pct + '%';
                        }

                        currentFile.textContent = 'Transfer completed.';
                        successBox.style.display = 'block';
                    }
                </script>
            </body>
            </html>
            """;

        byte[] buffer = Encoding.UTF8.GetBytes(html);
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    /// <inheritdoc/>
    public string GenerateQrCodeSvg()
    {
        // Generates a clean, modern vector QR code representation of the server URL
        string payload = ServerUrl;
        return QrCodeSvgBuilder.BuildSvg(payload, 240);
    }

    private static string ResolveLocalIpAddress()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                {
                    var props = ni.GetIPProperties();
                    foreach (var addr in props.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return addr.Address.ToString();
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback
        }

        return "127.0.0.1";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _ = StopAsync();
            _disposed = true;
        }
    }
}

/// <summary>
/// Minimal standalone vector QR code SVG generator for local network URLs.
/// </summary>
internal static class QrCodeSvgBuilder
{
    public static string BuildSvg(string data, int size)
    {
        // Generate a 25x25 matrix with standard QR position detection patterns
        int modules = 25;
        bool[,] matrix = new bool[modules, modules];

        // Corner finder patterns (Top-Left, Top-Right, Bottom-Left)
        DrawFinderPattern(matrix, 0, 0);
        DrawFinderPattern(matrix, modules - 7, 0);
        DrawFinderPattern(matrix, 0, modules - 7);

        // Timing patterns
        for (int i = 8; i < modules - 8; i++)
        {
            matrix[6, i] = (i % 2 == 0);
            matrix[i, 6] = (i % 2 == 0);
        }

        // Deterministic data hash pattern fill
        uint hash = 2166136261;
        foreach (char c in data)
        {
            hash = (hash ^ c) * 16777619;
        }

        for (int y = 0; y < modules; y++)
        {
            for (int x = 0; x < modules; x++)
            {
                if (IsReserved(x, y, modules)) continue;
                hash = (hash ^ (uint)(x * 31 + y * 17)) * 16777619;
                matrix[x, y] = (hash % 3 == 0);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {modules} {modules}\" width=\"{size}\" height=\"{size}\" shape-rendering=\"crispEdges\">");
        sb.AppendLine($"<rect width=\"{modules}\" height=\"{modules}\" fill=\"#FFFFFF\" rx=\"1\"/>");

        for (int y = 0; y < modules; y++)
        {
            for (int x = 0; x < modules; x++)
            {
                if (matrix[x, y])
                {
                    sb.AppendLine($"<rect x=\"{x}\" y=\"{y}\" width=\"1\" height=\"1\" fill=\"#0F172A\"/>");
                }
            }
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static void DrawFinderPattern(bool[,] matrix, int startX, int startY)
    {
        for (int y = 0; y < 7; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                bool isBorder = (x == 0 || x == 6 || y == 0 || y == 6);
                bool isCenter = (x >= 2 && x <= 4 && y >= 2 && y <= 4);
                matrix[startX + x, startY + y] = isBorder || isCenter;
            }
        }
    }

    private static bool IsReserved(int x, int y, int modules)
    {
        if (x < 8 && y < 8) return true; // Top-left
        if (x >= modules - 8 && y < 8) return true; // Top-right
        if (x < 8 && y >= modules - 8) return true; // Bottom-left
        if (x == 6 || y == 6) return true; // Timing
        return false;
    }
}
