using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Mirror;

/// <summary>
/// A's UDP discovery responder (R1, R4, KTD-8). Listens on the fixed discovery port
/// and answers probes with a beacon carrying the app version, ECU-connection state,
/// display name, and a stable per-instance id. Android acquires a multicast/broadcast
/// lock so Wi-Fi does not filter the broadcast probes.
/// </summary>
public sealed class DiscoveryResponderService : IDisposable
{
    /// <summary>Probe payload B broadcasts to find A (JSON).</summary>
    public const string ProbePayload = "{\"probe\":\"me221\"}";

    private readonly ApprovedDeviceStore _store;
    private readonly Func<bool> _ecuConnected;
    private readonly Func<string> _appVersion;
    private readonly ILogger<DiscoveryResponderService> _logger;
    private UdpClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;

#if ANDROID
    private Android.Net.Wifi.WifiManager.MulticastLock? _multicastLock;
#endif

    public DiscoveryResponderService(
        ApprovedDeviceStore store,
        IEcuConnectionService? connection = null,
        Func<string>? appVersion = null,
        ILogger<DiscoveryResponderService>? logger = null)
    {
        _store = store;
        _ecuConnected = connection is null ? static () => false : () => connection.State == ConnectionState.Connected;
        _appVersion = appVersion ?? (() => AppInfo.Current.VersionString);
        _logger = logger ?? NullLogger<DiscoveryResponderService>.Instance;
    }

    public bool IsRunning => _client is not null;
    public int BoundPort { get; private set; }

    public void Start(int? port = null)
    {
        if (_client is not null)
            return;

        var endpoint = new IPEndPoint(IPAddress.Any, port ?? MirrorProtocol.DiscoveryPort);
        _client = new UdpClient(endpoint);
        _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _client.EnableBroadcast = true;
        BoundPort = ((IPEndPoint)_client.Client.LocalEndPoint!).Port;
        _cts = new CancellationTokenSource();
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token));

#if ANDROID
        AcquireMulticastLock();
#endif

        _logger.LogInformation("Discovery responder listening on UDP {Port}", BoundPort);
    }

    public void Stop()
    {
        if (_client is null)
            return;

        _cts?.Cancel();
        _client.Dispose();
        _client = null;
#if ANDROID
        ReleaseMulticastLock();
#endif
        _logger.LogInformation("Discovery responder stopped");
    }

    public void Dispose() => Stop();

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _client!.ReceiveAsync(ct).ConfigureAwait(false);
                if (result.Buffer.Length > 0 && Encoding.UTF8.GetString(result.Buffer).Contains("me221", StringComparison.Ordinal))
                    await RespondBeaconAsync(result.RemoteEndPoint).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "Discovery receive failed");
            }
        }
    }

    private async Task RespondBeaconAsync(IPEndPoint remote)
    {
        try
        {
            var instanceId = await _store.GetOrCreateInstanceIdAsync().ConfigureAwait(false);
            var beacon = new
            {
                appVersion = _appVersion(),
                ecuConnected = _ecuConnected(),
                displayName = await _store.GetDisplayNameAsync().ConfigureAwait(false),
                instanceId,
            };
            var json = JsonSerializer.Serialize(beacon);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _client!.SendAsync(bytes, bytes.Length, remote).ConfigureAwait(false);
            _logger.LogDebug("Discovery beacon sent to {Remote}", remote);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send discovery beacon");
        }
    }

#if ANDROID
    private void AcquireMulticastLock()
    {
        try
        {
            var wifi = (Android.Net.Wifi.WifiManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.WifiService);
            _multicastLock = wifi?.CreateMulticastLock("me221-discovery");
            _multicastLock?.SetReferenceCounted(false);
            _multicastLock?.Acquire();
            _logger.LogDebug("Android multicast lock acquired");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to acquire Android multicast lock; broadcast reception may be filtered");
        }
    }

    private void ReleaseMulticastLock()
    {
        try
        {
            _multicastLock?.Release();
            _multicastLock?.Dispose();
            _multicastLock = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release Android multicast lock");
        }
    }
#endif
}