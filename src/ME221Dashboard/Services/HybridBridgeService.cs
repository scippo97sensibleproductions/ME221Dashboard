using System.Diagnostics;
using System.Text.Json;
using ME221.Comms;
using ME221.Comms.Messages;
using ME221.Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Controls;

namespace ME221Dashboard.Services;

/// <summary>
/// Bridge service that enables communication between Svelte JS (via HybridWebView) and C#/.NET services.
/// JavaScript calls C# methods via window.HybridWebView.InvokeDotNet.
/// C# calls JavaScript via HybridWebView.InvokeJavaScriptAsync or SendRawMessage.
///
/// This is the core orchestrator. Method implementations are in partial class files under Bridge/:
/// - EcuBridgeMethods.cs: ECU info, calibration load/match
/// - ConnectionBridgeMethods.cs: TCP/Serial connect, disconnect, reporting
/// - DashboardBridgeMethods.cs: Dashboard CRUD, config, sensor selection
/// - TableBridgeMethods.cs: Table definitions, read/write
/// - GpsBridgeMethods.cs: GPS start/stop, odometer
/// - VehicleConfigBridgeMethods.cs: Vehicle config get/set
/// - PermissionBridgeMethods.cs: Android permissions (USB, location, storage)
/// - ImageBridgeMethods.cs: Image pickers, base64 conversion
/// - LogBridgeMethods.cs: Log streaming
/// - FileBridgeMethods.cs: File export/import, dashboard export/import
/// </summary>
public partial class HybridBridgeService : IDisposable
{
    internal static readonly JsonSerializerOptions SJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    internal static readonly Dictionary<int, (string Name, string Unit, double Min, double Max)> S_gpsDefaults = new()
    {
        [-1001] = ("GPS Speed", "km/h", 0, 240),
        [-1002] = ("GPS Latitude", "\u00b0", -90, 90),
        [-1003] = ("GPS Longitude", "\u00b0", -180, 180),
        [-1004] = ("GPS Altitude", "m", 0, 1000),
        [-1005] = ("GPS Course", "\u00b0", 0, 360),
        [-1006] = ("GPS Accuracy", "m", 0, 50),
    };

    internal static readonly Dictionary<int, (string Name, string Unit, double Min, double Max)> S_odometerDefaults = new()
    {
        [-2001] = ("Odometer", "km", 0, 999999),
    };

    internal static readonly Dictionary<int, (string Name, string Unit, double Min, double Max)> S_derivedDefaults = new()
    {
        [-3001] = ("Gear", "", -1, 10),
        [-3002] = ("True Speed", "km/h", 0, 400),
        [-3003] = ("Boost Pressure", "kPa", 0, 400),
        [-3004] = ("Speed Error", "km/h", -50, 50),
    };

    private const int OdometerEntityId = -2001;

    internal readonly IEcuConnectionService _connection;
    internal readonly ILiveDataService _liveData;
    internal readonly ICalibrationService _calibration;
    internal readonly DashboardPackageService _packageService;
    internal readonly ILogger<HybridBridgeService> _logger;
    internal readonly IGpsService? _gps;
    internal readonly LogCapture _logCapture;
    internal readonly IChannelFactory? _channelFactory;
    internal readonly IUpdateCheckerService? _updateChecker;
    internal HybridWebView? _webView;

    internal readonly Dictionary<int, string> _entityIdStrings = new();
    internal readonly Dictionary<string, string> _imageBase64Cache = new();
    private readonly System.Text.StringBuilder _liveDataSb = new(1024);
    private string? _lastLiveDataJson;
    private Action? _cachedSendLiveUpdate;
    internal string? _activeDashboardName;
    internal long _lastGpsTimestamp;
    internal long _lastVssTimestamp;
    internal readonly Dictionary<string, OdometerConfig> _odometerByDashboard = new();
    private int? _cachedVssEntityId;

    public HybridBridgeService(
        IEcuConnectionService connection,
        ILiveDataService liveData,
        ICalibrationService calibration,
        DashboardPackageService packageService,
        LogCapture logCapture,
        ILogger<HybridBridgeService>? logger = null,
        IGpsService? gps = null,
        IChannelFactory? channelFactory = null,
        IUpdateCheckerService? updateChecker = null)
    {
        _connection = connection;
        _liveData = liveData;
        _calibration = calibration;
        _packageService = packageService;
        _logCapture = logCapture;
        _logger = logger ?? NullLogger<HybridBridgeService>.Instance;
        _gps = gps;
        _channelFactory = channelFactory;
        _updateChecker = updateChecker;

        _logger.LogInformation("GPS service: {GpsType}", _gps?.GetType().Name ?? "NULL (none registered)");

        // Subscribe to connection state changes to notify React
        _connection.ConnectionStateChanged += OnConnectionStateChanged;
        _liveData.EntitiesUpdated += OnEntitiesUpdated;

        if (_gps is not null)
            _gps.LocationUpdated += OnGpsLocationUpdated;
    }

    /// <summary>
    /// Initialize the bridge with the HybridWebView instance.
    /// Must be called after the HybridWebView is created.
    /// </summary>
    public void Initialize(HybridWebView webView)
    {
        _webView = webView;
        _cachedSendLiveUpdate = SendLiveUpdate;
        _logCapture.SetWebViewSender(json => MainThread.InvokeOnMainThreadAsync(() =>
        {
            _webView?.SendRawMessage(json);
            return Task.CompletedTask;
        }));
    }

    private void SendLiveUpdate()
    {
        var json = _lastLiveDataJson;
        if (_webView != null && json != null)
            _webView.SendRawMessage(json);
    }

    // ─── Methods to notify Svelte (C# → JS) ───────────────────────────────

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
#if ANDROID
        var context = global::Android.App.Application.Context;
        if (e.NewState == ConnectionState.Connected)
            Platforms.Android.Services.EcuForegroundService.Start(context);
        else if (e.NewState is ConnectionState.Disconnected or ConnectionState.Error)
            Platforms.Android.Services.EcuForegroundService.Stop(context);
#endif

        // Force-persist odometer on disconnect/error — don't wait for the flush timer
        if (e.NewState is ConnectionState.Disconnected or ConnectionState.Error)
        {
            FlushOdometer();
        }

        // Refresh VSS entity ID cache on reconnect if VSS source is active
        if (e.NewState == ConnectionState.Connected && _activeDashboardName != null &&
            _odometerByDashboard.TryGetValue(_activeDashboardName, out var odoState) &&
            odoState.SpeedSource == OdometerSpeedSource.Vss)
        {
            _ = LoadVssEntityIdAsync();
            _ = ReadVssSpeedUnitAsync();
        }

        SendToReact(new
        {
            @event = "connectionStateChanged",
            state = e.NewState.ToString(),
            error = e.Error
        });
    }

    private void OnEntitiesUpdated(object? sender, EntitiesUpdatedEventArgs e)
    {
        if (_webView == null) return;

        // Build JSON manually — avoids JsonSerializer.Serialize overhead on hot path.
        // Format: {"event":"liveDataUpdate","values":{"id":value,...}}
        _liveDataSb.Clear();
        _liveDataSb.Append("{\"event\":\"liveDataUpdate\",\"values\":{");
        var ids = e.EntityIds.Span;
        for (var i = 0; i < e.Count; i++)
        {
            var id = ids[i];
            if (!_entityIdStrings.TryGetValue(id, out var idStr))
            {
                idStr = id.ToString();
                _entityIdStrings[id] = idStr;
            }
            if (i > 0) _liveDataSb.Append(',');
            _liveDataSb.Append('"');
            _liveDataSb.Append(idStr);
            _liveDataSb.Append("\":");
            var val = _liveData[id];
            if (val.HasValue)
                _liveDataSb.Append(val.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            else
                _liveDataSb.Append("null");
        }
        _liveDataSb.Append("}}");

        _lastLiveDataJson = _liveDataSb.ToString();
        if (_cachedSendLiveUpdate is { } send)
            MainThread.BeginInvokeOnMainThread(send);

        // ── VSS odometer accumulation ──────────────────────────────────────
        if (_activeDashboardName != null &&
            _odometerByDashboard.TryGetValue(_activeDashboardName, out var odoState) &&
            odoState.SpeedSource == OdometerSpeedSource.Vss &&
            _cachedVssEntityId.HasValue)
        {
            for (var i = 0; i < e.Count; i++)
            {
                if (ids[i] == _cachedVssEntityId.Value)
                {
                    var speedRaw = _liveData[_cachedVssEntityId.Value];
                    if (speedRaw.HasValue)
                    {
                        var isInMph = _cachedVssSpeedInMph ?? odoState.VssSpeedInMph;
                        var speedKmh = isInMph ? speedRaw.Value * 1.60934f : speedRaw.Value;
                        var now = Stopwatch.GetTimestamp();
                        if (_lastVssTimestamp != 0)
                        {
                            var dtSec = (now - _lastVssTimestamp) / (double)Stopwatch.Frequency;
                            if (dtSec is > 0 and < 60)
                                SpeedKmToOdometer(speedKmh, dtSec);
                        }
                        _lastVssTimestamp = now;
                    }
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Send a message to the Svelte app via SendRawMessage.
    /// </summary>
    internal void SendToReact(object data)
    {
        try
        {
            if (_webView == null) return;

            var json = JsonSerializer.Serialize(data, SJsonOptions);

            MainThread.BeginInvokeOnMainThread(() => _webView.SendRawMessage(json));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to React");
        }
    }

    /// <summary>
    /// Loads and caches the VSS speed entity ID from vehicle config.
    /// </summary>
    internal async Task LoadVssEntityIdAsync()
    {
        _cachedVssEntityId = await GetVssSpeedEntityIdAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Handle raw messages received from JavaScript.
    /// </summary>
    public void HandleRawMessage(string message)
    {
        _logger.LogDebug("Raw message from JS: {Message}", message);
        // Process raw messages if needed (currently just logging)
    }

    public void Dispose()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogCritical("SHUTDOWN: HybridBridgeService.Dispose START");

        // Force-persist odometer on shutdown
        if (_activeDashboardName != null && _odometerByDashboard.TryGetValue(_activeDashboardName, out var odoState))
        {
            try
            {
                _logger.LogCritical("SHUTDOWN: Persisting odometer for {Dashboard}", _activeDashboardName);
                PersistOdometerAsync(_activeDashboardName, odoState).GetAwaiter().GetResult();
                _logger.LogCritical("SHUTDOWN: Odometer persisted in {Elapsed}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to persist odometer on dispose"); }
        }

        _logger.LogCritical("SHUTDOWN: Unsubscribing events");
        _connection.ConnectionStateChanged -= OnConnectionStateChanged;
        _liveData.EntitiesUpdated -= OnEntitiesUpdated;
        if (_gps is not null)
            _gps.LocationUpdated -= OnGpsLocationUpdated;
        _webView = null;
        _logger.LogCritical("SHUTDOWN: HybridBridgeService.Dispose DONE in {Elapsed}ms", sw.ElapsedMilliseconds);
        GC.SuppressFinalize(this);
    }
}
