using System.IO.Ports;

namespace ME221.Comms.Cli.Discovery;

/// <summary>
/// Polls for available serial/USB devices at a configurable interval.
/// Notifies on device addition/removal events.
/// </summary>
public sealed class DevicePoller(int pollIntervalMs = 500) : IDisposable
{
    private readonly SortedSet<string> _discoveredPorts = [];
    private Timer? _timer;
    private readonly object _lock = new();

    /// <summary>
    /// Fired when a new port becomes available.
    /// </summary>
    public event Action<string>? PortAdded;

    /// <summary>
    /// Fired when a port is disconnected.
    /// </summary>
    public event Action<string>? PortRemoved;

    /// <summary>
    /// Current list of discovered port names.
    /// </summary>
    public IReadOnlyCollection<string> DiscoveredPorts => _discoveredPorts;

    /// <summary>
    /// Starts polling for devices.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            _discoveredPorts.Clear();
            _timer = new Timer(
                _ => Poll(),
                state: null,
                dueTime: 0,
                period: pollIntervalMs);
        }
    }

    /// <summary>
    /// Stops polling and cleans up resources.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Poll()
    {
        try
        {
            var currentPorts = new SortedSet<string>(SerialPort.GetPortNames());
            string[]? added = null;
            string[]? removed = null;

            lock (_lock)
            {
                added = currentPorts.Except(_discoveredPorts).ToArray();
                removed = _discoveredPorts.Except(currentPorts).ToArray();

                _discoveredPorts.Clear();
                _discoveredPorts.UnionWith(currentPorts);
            }

            if (added.Length > 0)
            {
                foreach (var port in added)
                    PortAdded?.Invoke(port);
            }

            if (removed.Length > 0)
            {
                foreach (var port in removed)
                    PortRemoved?.Invoke(port);
            }
        }
        catch
        {
            // Silently ignore polling errors — they're transient
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
