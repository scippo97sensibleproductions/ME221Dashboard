using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Devices.Sensors;

namespace ME221Dashboard.Services;

/// <summary>
/// Real GPS via MAUI Geolocation API. Used on Android.
/// </summary>
public sealed class GeolocationGpsService(ILogger<GeolocationGpsService>? logger = null) : IGpsService, IDisposable
{
    private readonly ILogger<GeolocationGpsService> _logger = logger ?? NullLogger<GeolocationGpsService>.Instance;
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    public bool IsRunning => _runTask is { IsCompleted: false };

    public event EventHandler<GpsLocationEventArgs>? LocationUpdated;

    public async Task<bool> StartAsync(CancellationToken ct = default)
    {
        if (IsRunning)
            return true;

        // Check permission first
        var status = await MainThread.InvokeOnMainThreadAsync(() =>
            Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>()).ConfigureAwait(false);

        if (status != PermissionStatus.Granted)
        {
            status = await MainThread.InvokeOnMainThreadAsync(() =>
                Permissions.RequestAsync<Permissions.LocationWhenInUse>()).ConfigureAwait(false);
        }

        if (status != PermissionStatus.Granted)
        {
            _logger.LogWarning("Location permission not granted");
            return false;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _runTask = RunLoopAsync(_cts.Token);
        _logger.LogInformation("Geolocation GPS started");
        return true;
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_runTask is not null)
            await _runTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _cts.Dispose();
        _cts = null;
        _logger.LogInformation("Geolocation GPS stopped");
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var location = await Geolocation.GetLocationAsync(request, ct).ConfigureAwait(false);
                if (location is not null)
                {
                    LocationUpdated?.Invoke(this, new GpsLocationEventArgs(
                        latitude: location.Latitude,
                        longitude: location.Longitude,
                        altitude: location.Altitude,
                        speed: location.Speed,
                        course: location.Course,
                        accuracy: location.Accuracy));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get location");
            }

            try
            {
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _runTask?.Dispose();
    }
}
