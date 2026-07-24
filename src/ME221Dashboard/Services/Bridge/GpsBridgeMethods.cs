using System.Diagnostics;
using System.Text.Json;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Odometer Throttle ──────────────────────────────────────────────────
    private bool _odometerDirty;
    private Timer? _odometerFlushTimer;

    // ─── Speed unit conversion factors (source unit → km/h) ─────────────────
    private static readonly Dictionary<string, double> SpeedUnitToKmh = new(StringComparer.OrdinalIgnoreCase)
    {
        ["km/h"] = 1.0,
        ["mph"] = 1.60934,
        ["m/s"] = 3.6,
        ["knots"] = 1.852,
    };

    // ─── GPS Methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Start GPS updates. Called from JS: window.HybridWebView.InvokeDotNet('StartGps')
    /// </summary>
    public async Task<string> StartGps()
    {
        _logger.LogInformation("StartGps called");
        if (_gps is null)
            return JsonSerializer.Serialize(new { success = false, error = "GPS not available on this platform" });

        try
        {
            var started = await _gps.StartAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = started });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartGps failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Stop GPS updates. Called from JS: window.HybridWebView.InvokeDotNet('StopGps')
    /// </summary>
    public async Task<string> StopGps()
    {
        _logger.LogInformation("StopGps called");
        if (_gps is null)
            return JsonSerializer.Serialize(new { success = true });

        try
        {
            await _gps.StopAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StopGps failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get current GPS status. Called from JS: window.HybridWebView.InvokeDotNet('GetGpsStatus')
    /// </summary>
    public string GetGpsStatus()
    {
        return JsonSerializer.Serialize(new
        {
            available = _gps is not null,
            isRunning = _gps?.IsRunning ?? false,
        });
    }

    /// <summary>
    /// Set a fixed GPS speed for debugging (km/h). Pass null to resume normal simulation.
    /// Only works with SimulatedGpsService. Called from JS: window.HybridWebView.InvokeDotNet('SetDebugSpeed', [speedKmh])
    /// </summary>
    public string SetDebugSpeed(double? speedKmh)
    {
        if (_gps is SimulatedGpsService sim)
        {
            sim.SetFixedSpeed(speedKmh);
            return JsonSerializer.Serialize(new { success = true });
        }
        return JsonSerializer.Serialize(new { success = false, error = "Debug speed only available with simulated GPS" });
    }

    /// <summary>
    /// Load odometer state from persisted config (or create default).
    /// </summary>
    private OdometerConfig LoadOdometerState(string dashboardName)
    {
        if (_odometerByDashboard.TryGetValue(dashboardName, out var existing))
            return existing;

        OdometerConfig state;
        try
        {
            var configTask = _calibration.GetPersistedDashboardConfigAsync();
            configTask.Wait(TimeSpan.FromSeconds(1));
            var config = configTask.Result;
            if (config?.Dashboards.TryGetValue(dashboardName, out var def) == true && def.Odometer != null)
            {
                state = new OdometerConfig
                {
                    CurrentValue = def.Odometer.CurrentValue,
                    UseKilometers = def.Odometer.UseKilometers,
                    SpeedSource = def.Odometer.SpeedSource,
                    VssSpeedInMph = def.Odometer.VssSpeedInMph,
                    SpeedEntityId = def.Odometer.SpeedEntityId,
                    SpeedUnit = def.Odometer.SpeedUnit,
                };
            }
            else
            {
                state = new OdometerConfig();
            }
        }
        catch
        {
            state = new OdometerConfig();
        }
        _odometerByDashboard[dashboardName] = state;
        return state;
    }

    /// Called from JS: window.HybridWebView.InvokeDotNet('GetOdometer')
    public string GetOdometer()
    {
        if (_activeDashboardName == null)
            return JsonSerializer.Serialize(new { value = 0.0, unit = "km", useKilometers = true, speedSource = "gps", speedEntityId = (int?)null, speedUnit = "km/h" });

        var state = LoadOdometerState(_activeDashboardName);

        return JsonSerializer.Serialize(new
        {
            value = state.CurrentValue,
            unit = state.UseKilometers ? "km" : "mi",
            useKilometers = state.UseKilometers,
            speedSource = state.SpeedSource == OdometerSpeedSource.Vss ? "vss" : "gps",
            speedEntityId = state.SpeedEntityId,
            speedUnit = state.SpeedUnit,
        });
    }

    /// Called from JS: window.HybridWebView.InvokeDotNet('ResetOdometer')
    public async Task ResetOdometer()
    {
        if (_activeDashboardName == null) return;
        var old = LoadOdometerState(_activeDashboardName);
        _odometerByDashboard[_activeDashboardName] = new OdometerConfig
        {
            UseKilometers = old.UseKilometers,
            SpeedSource = old.SpeedSource,
            SpeedEntityId = old.SpeedEntityId,
            SpeedUnit = old.SpeedUnit,
        };
        await PersistOdometerAsync(_activeDashboardName, _odometerByDashboard[_activeDashboardName]).ConfigureAwait(false);
        SendOdometerUpdate();
    }

    /// Called from JS: window.HybridWebView.InvokeDotNet('SetOdometerValue', [value])
    public async Task SetOdometerValue(double value)
    {
        if (_activeDashboardName == null) return;
        var state = LoadOdometerState(_activeDashboardName);
        state.CurrentValue = Math.Max(0, value);
        _odometerByDashboard[_activeDashboardName] = state;
        await PersistOdometerAsync(_activeDashboardName, state).ConfigureAwait(false);
        SendOdometerUpdate();
    }

    /// Called from JS: window.HybridWebView.InvokeDotNet('SetOdometerUnit', [useKilometers])
    public async Task SetOdometerUnit(bool useKilometers)
    {
        if (_activeDashboardName == null) return;
        var state = LoadOdometerState(_activeDashboardName);
        state.UseKilometers = useKilometers;
        _odometerByDashboard[_activeDashboardName] = state;
        await PersistOdometerAsync(_activeDashboardName, state).ConfigureAwait(false);
        SendOdometerUpdate();
    }

    /// <summary>
    /// Bridge method: Sets the odometer speed source (GPS or custom entity).
    /// Called from JS: window.HybridWebView.InvokeDotNet('SetOdometerSpeedSource', [source])
    /// source: 0 = GPS, 1 = custom entity (use SetOdometerSpeedConfig to set entity/unit)
    /// </summary>
    public async Task SetOdometerSpeedSource(int source)
    {
        if (_activeDashboardName == null) return;
        var state = LoadOdometerState(_activeDashboardName);

        state.SpeedSource = source == 1 ? OdometerSpeedSource.Vss : OdometerSpeedSource.Gps;
        _odometerByDashboard[_activeDashboardName] = state;

        await PersistOdometerAsync(_activeDashboardName, state).ConfigureAwait(false);
        SendOdometerUpdate();
    }

    /// <summary>
    /// Bridge method: Sets the odometer speed entity and unit for custom entity mode.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SetOdometerSpeedConfig', [json])
    /// json: { "entityId": 940, "unit": "km/h" } or { "entityId": null } to clear
    /// </summary>
    public async Task SetOdometerSpeedConfig(string json)
    {
        if (_activeDashboardName == null) return;
        var state = LoadOdometerState(_activeDashboardName);

        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(json);
            if (node != null)
            {
                state.SpeedEntityId = node["entityId"]?.GetValue<int?>();
                var unit = node["unit"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(unit) && SpeedUnitToKmh.ContainsKey(unit))
                    state.SpeedUnit = unit;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse SetOdometerSpeedConfig JSON");
        }

        if (state.SpeedEntityId.HasValue)
            state.SpeedSource = OdometerSpeedSource.Vss;

        _odometerByDashboard[_activeDashboardName] = state;
        await PersistOdometerAsync(_activeDashboardName, state).ConfigureAwait(false);
        SendOdometerUpdate();
    }

    internal void OnGpsLocationUpdated(object? sender, GpsLocationEventArgs e)
    {
        if (_webView is null) return;

        // ── Odometer accumulation (GPS source only) ────────────────────────
        try
        {
            if (_activeDashboardName != null)
            {
                var state = LoadOdometerState(_activeDashboardName);
                if (state.SpeedSource == OdometerSpeedSource.Gps && e.Speed.HasValue)
                {
                    var now = Stopwatch.GetTimestamp();
                    if (_lastGpsTimestamp != 0)
                    {
                        var dtSec = (now - _lastGpsTimestamp) / (double)Stopwatch.Frequency;
                        if (dtSec is > 0 and < 60)
                            SpeedKmToOdometer(e.Speed.Value * 3.6, dtSec); // m/s → km/h
                    }
                    _lastGpsTimestamp = now;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Odometer accumulation failed");
        }

        // Send GPS update
        var json = JsonSerializer.Serialize(new
        {
            @event = "gpsUpdate",
            latitude = e.Latitude,
            longitude = e.Longitude,
            altitude = e.Altitude,
            speed = e.Speed,
            course = e.Course,
            accuracy = e.Accuracy,
            timestamp = DateTime.UtcNow.ToString("o"),
        });

        MainThread.BeginInvokeOnMainThread(() => _webView.SendRawMessage(json));

        // Send separate odometer update
        SendOdometerUpdate();
    }

    private void SpeedKmToOdometer(double speedKmh, double dtSec)
    {
        var distanceKm = speedKmh * (dtSec / 3600.0);
        if (distanceKm <= 0) return;

        var activeName = _activeDashboardName;
        if (activeName == null) return;

        var state = LoadOdometerState(activeName);

        if (state.UseKilometers)
            state.CurrentValue += distanceKm;
        else
            state.CurrentValue += distanceKm * 0.621371; // km to miles

        // Mark dirty — flush every 5 seconds or on disconnect/app-close
        _odometerDirty = true;
        _odometerFlushTimer ??= new Timer(_ => FlushOdometer(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private async Task PersistOdometerAsync(string dashboardName, OdometerConfig state)
    {
        try
        {
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            if (config == null) return;
            if (config.Dashboards.TryGetValue(dashboardName, out var def))
            {
                def.Odometer = state;
                await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist odometer for '{Dashboard}'", dashboardName);
        }
    }

    /// <summary>
    /// Flush dirty odometer to disk. Called by the 5-second timer and on disconnect.
    /// </summary>
    internal void FlushOdometer()
    {
        if (!_odometerDirty) return;
        _odometerDirty = false;
        var activeName = _activeDashboardName;
        if (activeName == null) return;
        if (!_odometerByDashboard.TryGetValue(activeName, out var state)) return;
        _ = PersistOdometerAsync(activeName, state);
    }

    private void SendOdometerUpdate()
    {
        if (_activeDashboardName == null || _webView is null) return;
        if (!_odometerByDashboard.TryGetValue(_activeDashboardName, out var state)) return;
        var json = JsonSerializer.Serialize(new
        {
            @event = "odometerUpdate",
            odometer = state.CurrentValue,
            odometerUnit = state.UseKilometers ? "km" : "mi",
        });
        MainThread.BeginInvokeOnMainThread(() => _webView.SendRawMessage(json));
    }
}
