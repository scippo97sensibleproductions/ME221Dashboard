using System.Diagnostics;
using System.Text.Json;
using ME221.Comms.Messages;
using ME221.Data.Infrastructure;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── VSS Driver Discovery Fields ────────────────────────────────────────
    private int? _vssDriverId;
    private int? _vssModeConfigIndex;
    private bool? _cachedVssSpeedInMph;

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

    /// Called from JS: window.HybridWebView.InvokeDotNet('GetOdometer')
    public string GetOdometer()
    {
        if (_activeDashboardName == null)
            return JsonSerializer.Serialize(new { value = 0.0, unit = "km", useKilometers = true, speedSource = "gps", vssSpeedInMph = false });

        if (!_odometerByDashboard.TryGetValue(_activeDashboardName, out var state))
        {
            // Load from persisted config
            try
            {
                var configTask = _calibration.GetPersistedDashboardConfigAsync();
                configTask.Wait(TimeSpan.FromSeconds(1));
                var config = configTask.Result;
                if (config?.Dashboards.TryGetValue(_activeDashboardName, out var def) == true && def.Odometer != null)
                {
                    state = new OdometerConfig
                    {
                        CurrentValue = def.Odometer.CurrentValue,
                        UseKilometers = def.Odometer.UseKilometers,
                        SpeedSource = def.Odometer.SpeedSource,
                        VssSpeedInMph = def.Odometer.VssSpeedInMph,
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
            _odometerByDashboard[_activeDashboardName] = state;
        }

        return JsonSerializer.Serialize(new
        {
            value = state.CurrentValue,
            unit = state.UseKilometers ? "km" : "mi",
            useKilometers = state.UseKilometers,
            speedSource = state.SpeedSource == OdometerSpeedSource.Vss ? "vss" : "gps",
            vssSpeedInMph = state.VssSpeedInMph,
        });
    }

    /// Called from JS: window.HybridWebView.InvokeDotNet('ResetOdometer')
    public async Task ResetOdometer()
    {
        if (_activeDashboardName == null) return;
        _odometerByDashboard[_activeDashboardName] = new OdometerConfig { CurrentValue = 0 };
        await PersistOdometerAsync(_activeDashboardName, _odometerByDashboard[_activeDashboardName]).ConfigureAwait(false);
    }

    /// Called from JS: window.HybridWebView.InvokeDotNet('SetOdometerValue', [value])
    public async Task SetOdometerValue(double value)
    {
        if (_activeDashboardName == null) return;
        if (!_odometerByDashboard.TryGetValue(_activeDashboardName, out var state))
            state = new OdometerConfig();
        state.CurrentValue = Math.Max(0, value);
        _odometerByDashboard[_activeDashboardName] = state;
        await PersistOdometerAsync(_activeDashboardName, state).ConfigureAwait(false);
    }

    /// Called from JS: window.HybridWebView.InvokeDotNet('SetOdometerUnit', [useKilometers])
    public async Task SetOdometerUnit(bool useKilometers)
    {
        if (_activeDashboardName == null) return;
        if (!_odometerByDashboard.TryGetValue(_activeDashboardName, out var state))
            state = new OdometerConfig();
        state.UseKilometers = useKilometers;
        _odometerByDashboard[_activeDashboardName] = state;
        await PersistOdometerAsync(_activeDashboardName, state).ConfigureAwait(false);
    }

    internal void OnGpsLocationUpdated(object? sender, GpsLocationEventArgs e)
    {
        if (_webView is null) return;

        // ── Odometer accumulation ────────────────────────────────────────
        try
        {
            var now = Stopwatch.GetTimestamp();
            if (_lastGpsTimestamp != 0)
            {
                var dtSec = (now - _lastGpsTimestamp) / (double)Stopwatch.Frequency;
                if (dtSec is > 0 and < 60)
                {
                    if (e.Speed.HasValue) SpeedKmToOdometer(e.Speed.Value * 3.6, dtSec); // m/s → km/h
                }
            }
            _lastGpsTimestamp = now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Odometer accumulation failed");
        }

        // Send GPS update (no longer includes odometer)
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
        if (_activeDashboardName != null && _odometerByDashboard.TryGetValue(_activeDashboardName, out var odoSt))
        {
            var odoJson = JsonSerializer.Serialize(new
            {
                @event = "odometerUpdate",
                odometer = odoSt.CurrentValue,
                odometerUnit = odoSt.UseKilometers ? "km" : "mi",
            });
            MainThread.BeginInvokeOnMainThread(() => _webView.SendRawMessage(odoJson));
        }
    }

    private void SpeedKmToOdometer(double speedKmh, double dtSec)
    {
        var distanceKm = speedKmh * (dtSec / 3600.0);
        if (distanceKm <= 0) return;

        var activeName = _activeDashboardName;
        if (activeName == null) return;

        if (!_odometerByDashboard.TryGetValue(activeName, out var state))
        {
            // Load from persisted config
            try
            {
                var configTask = _calibration.GetPersistedDashboardConfigAsync();
                configTask.Wait(TimeSpan.FromSeconds(1));
                var config = configTask.Result;
                if (config?.Dashboards.TryGetValue(activeName, out var def) == true && def.Odometer != null)
                {
                    state = new OdometerConfig
                    {
                        CurrentValue = def.Odometer.CurrentValue,
                        UseKilometers = def.Odometer.UseKilometers,
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
            _odometerByDashboard[activeName] = state;
        }

        if (state.UseKilometers)
            state.CurrentValue += distanceKm;
        else
            state.CurrentValue += distanceKm * 0.621371; // km to miles

        // Persist immediately — odometer value must never be lost
        PersistOdometerAsync(activeName, state).ConfigureAwait(false);
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

    // ─── VSS Driver Discovery ──────────────────────────────────────────────

    /// <summary>
    /// Scans calibration driver definitions for a driver with a Mode parameter
    /// whose options match "Pulses per mile" / "Pulses per km".
    /// Returns (driverId, configIndex) or null. Caches the result.
    /// </summary>
    internal async Task<(int DriverId, int ConfigIndex)?> DiscoverVssDriverAsync()
    {
        if (_vssDriverId.HasValue && _vssModeConfigIndex.HasValue)
            return (_vssDriverId.Value, _vssModeConfigIndex.Value);

        var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
        var drivers = calResult.Data?.Drivers;
        if (drivers == null) return null;

        foreach (var driver in drivers)
        {
            for (var i = 0; i < driver.Configs.Count; i++)
            {
                var config = driver.Configs[i];
                if (config.Options?.Any(o =>
                    o.Name.Contains("Pulses per mile", StringComparison.OrdinalIgnoreCase) ||
                    o.Name.Contains("Pulses per km", StringComparison.OrdinalIgnoreCase)) == true)
                {
                    _vssDriverId = driver.Id;
                    _vssModeConfigIndex = i;
                    return (driver.Id, i);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Reads the VSS driver's Mode parameter from the ECU to determine speed unit.
    /// 0 = pulses per mile (mph), 1 = pulses per km (km/h).
    /// Caches the result.
    /// </summary>
    internal async Task ReadVssSpeedUnitAsync()
    {
        var discovery = await DiscoverVssDriverAsync().ConfigureAwait(false);
        if (discovery == null) { _logger.LogWarning("VSS driver not found in calibration"); return; }

        var protocol = _connection.GetProtocolService();
        if (protocol == null) { _logger.LogWarning("ECU not connected, cannot read VSS driver"); return; }

        var request = new GetDriverRequest((ushort)discovery.Value.DriverId);
        var response = await protocol.SendAsync<GetDriverResponse>(request).ConfigureAwait(false);
        if (response.Status != MessageStatus.Success) return;

        var wireData = DriverSerializer.Deserialize(response.SerializedDriver.Span);
        if (wireData.Configs.Length > discovery.Value.ConfigIndex)
        {
            var mode = (int)wireData.Configs[discovery.Value.ConfigIndex];
            var isInMph = mode == 0;
            _cachedVssSpeedInMph = isInMph;

            if (_activeDashboardName != null && _odometerByDashboard.TryGetValue(_activeDashboardName, out var state))
            {
                state.VssSpeedInMph = isInMph;
                await PersistOdometerAsync(_activeDashboardName, state).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Reads the VSS speed entity ID from the vehicle config.
    /// </summary>
    internal async Task<int?> GetVssSpeedEntityIdAsync()
    {
        var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
        return config?.Vehicle?.VssSpeedEntityId;
    }

    /// <summary>
    /// Bridge method: Sets the odometer speed source (GPS or VSS).
    /// Called from JS: window.HybridWebView.InvokeDotNet('SetOdometerSpeedSource', [source])
    /// </summary>
    public async Task SetOdometerSpeedSource(int source)
    {
        if (_activeDashboardName == null) return;
        if (!_odometerByDashboard.TryGetValue(_activeDashboardName, out var state))
            state = new OdometerConfig();

        state.SpeedSource = source == 1 ? OdometerSpeedSource.Vss : OdometerSpeedSource.Gps;
        _odometerByDashboard[_activeDashboardName] = state;

        if (state.SpeedSource == OdometerSpeedSource.Vss)
        {
            await ReadVssSpeedUnitAsync().ConfigureAwait(false);
            await LoadVssEntityIdAsync().ConfigureAwait(false);
        }

        await PersistOdometerAsync(_activeDashboardName, state).ConfigureAwait(false);
    }
}
