namespace ME221Dashboard.Services;

public sealed class GpsLocationEventArgs(
    double latitude,
    double longitude,
    double? altitude,
    double? speed,
    double? course,
    double? accuracy) : EventArgs
{
    public double Latitude { get; } = latitude;
    public double Longitude { get; } = longitude;
    public double? Altitude { get; } = altitude;
    public double? Speed { get; } = speed;
    public double? Course { get; } = course;
    public double? Accuracy { get; } = accuracy;
}

public interface IGpsService
{
    bool IsRunning { get; }
    Task<bool> StartAsync(CancellationToken ct = default);
    Task StopAsync();
    event EventHandler<GpsLocationEventArgs>? LocationUpdated;
}
