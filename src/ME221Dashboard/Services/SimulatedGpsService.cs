using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services;

/// <summary>
/// Simulated GPS for Windows desktop. Reads speed from LiveDataService (ECU-reported speed)
/// instead of generating an independent profile. This ensures GPS speed correlates with RPM
/// for correct derived value computation (gear prediction, true speed, speed error).
/// Falls back to zero speed when ECU is not connected.
/// GPS refresh rate simulates a typical Android device (~1Hz with jitter).
/// </summary>
public sealed class SimulatedGpsService(ILiveDataService? liveData = null, ILogger<SimulatedGpsService>? logger = null)
    : IGpsService, IDisposable
{
    private readonly ILogger<SimulatedGpsService> _logger = logger ?? NullLogger<SimulatedGpsService>.Instance;
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    // Route center (Munich autobahn area)
    private const double CenterLat = 48.1351;
    private const double CenterLon = 11.5820;
    private const double RadiusKm = 2.0;

    // Entity ID for ECU-reported vehicle speed (VSS Speed from calibration)
    private const int SpeedEntityId = 940;

    private double _angle;
    private double _speed; // m/s
    private double? _fixedSpeedKmh; // null = read from ECU, non-null = hold this constant speed

    public bool IsRunning => _runTask is { IsCompleted: false };

    public event EventHandler<GpsLocationEventArgs>? LocationUpdated;

    public Task<bool> StartAsync(CancellationToken ct = default)
    {
        if (IsRunning)
            return Task.FromResult(true);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _angle = 0;
        _speed = 0;

        _runTask = RunLoopAsync(_cts.Token);
        _logger.LogInformation("Simulated GPS started");
        return Task.FromResult(true);
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_runTask is not null)
            await _runTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _cts.Dispose();
        _cts = null;
        _logger.LogInformation("Simulated GPS stopped");
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        var lastTick = Stopwatch.GetTimestamp();
        var tickMs = 1000.0 / Stopwatch.Frequency;
        var rng = new Random();

        while (!ct.IsCancellationRequested)
        {
            var now = Stopwatch.GetTimestamp();
            var dt = (now - lastTick) * tickMs / 1000.0; // seconds
            lastTick = now;

            if (dt > 0 && dt < 2)
                UpdateSpeedFromEcu();

            var lat = CenterLat + RadiusKm / 111.32 * Math.Sin(_angle);
            var lon = CenterLon + RadiusKm / (111.32 * Math.Cos(CenterLat * Math.PI / 180)) * Math.Cos(_angle);

            LocationUpdated?.Invoke(this, new GpsLocationEventArgs(
                latitude: lat,
                longitude: lon,
                altitude: 520 + 10 * Math.Sin(_angle * 3),
                speed: _speed,
                course: (_angle * 180 / Math.PI + 90) % 360,
                accuracy: 3.0));

            // Simulate Android GPS refresh rate: ~1Hz with jitter (800–1200ms)
            var delay = 800 + rng.Next(400);
            try
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void UpdateSpeedFromEcu()
    {
        if (_fixedSpeedKmh.HasValue)
        {
            _speed = _fixedSpeedKmh.Value / 3.6; // km/h → m/s
            AdvanceAngle(0.1); // approximate dt for angle advancement
            return;
        }

        // Read speed from LiveDataService (ECU-reported VSS Speed)
        if (liveData is not null)
        {
            var speedKmh = liveData[SpeedEntityId];
            if (speedKmh.HasValue && !float.IsNaN(speedKmh.Value) && speedKmh.Value >= 0)
            {
                _speed = speedKmh.Value / 3.6; // km/h → m/s
            }
            else
            {
                _speed = 0; // ECU not connected yet
            }
        }
        else
        {
            _speed = 0; // no LiveDataService available
        }

        AdvanceAngle(0.1);
    }

    private void AdvanceAngle(double dtSeconds)
    {
        var angularSpeed = _speed / (RadiusKm * 1000); // rad/s
        _angle += angularSpeed * dtSeconds;
        if (_angle > 2 * Math.PI) _angle -= 2 * Math.PI;
    }

    /// <summary>
    /// Set a fixed speed for debugging. Pass null to resume normal simulation.
    /// </summary>
    public void SetFixedSpeed(double? speedKmh)
    {
        _fixedSpeedKmh = speedKmh;
        if (speedKmh.HasValue)
            _speed = speedKmh.Value / 3.6; // km/h → m/s
        _logger.LogInformation("GPS debug speed: {Speed}", speedKmh?.ToString() ?? "simulation");
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _runTask?.Dispose();
    }
}
