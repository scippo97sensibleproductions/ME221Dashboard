namespace ME221.Comms.Discovery;

/// <summary>
/// Represents a discovered communication device.
/// </summary>
public sealed class DiscoveredDevice(
    string portName,
    string description,
    string? vendorId = null,
    string? productId = null)
{
    /// <summary>
    /// The device identifier (e.g., "COM3", "/dev/ttyUSB0").
    /// </summary>
    public string PortName { get; } = portName;

    /// <summary>
    /// A human-readable description of the device.
    /// </summary>
    public string Description { get; } = description;

    /// <summary>
    /// The vendor ID (if available).
    /// </summary>
    public string? VendorId { get; } = vendorId;

    /// <summary>
    /// The product ID (if available).
    /// </summary>
    public string? ProductId { get; } = productId;

    public override string ToString()
    {
        return $"{PortName} — {Description}";
    }
}

/// <summary>
/// Service for discovering available communication devices (serial ports, USB serial adapters).
/// </summary>
public interface IDeviceDiscoveryService
{
    /// <summary>
    /// Discovers all available communication devices.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the discovery.</param>
    /// <returns>A list of discovered devices.</returns>
    Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(CancellationToken cancellationToken = default);
}
