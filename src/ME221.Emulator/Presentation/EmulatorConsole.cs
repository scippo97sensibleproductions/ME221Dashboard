using System.Globalization;
using ME221.Data.Models;
using ME221.Emulator.Domain;

namespace ME221.Emulator.Presentation;

public class EmulatorConsole
{
    private readonly Dictionary<string, ushort> _linkByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ushort, int> _excursionTicks = new();
    private readonly object _lock = new();
    private SensorSimulator? _attachedSimulator;

    public SensorSimulator? AttachedSimulator => _attachedSimulator;

    public bool DrivingModeHold { get; private set; }

    public virtual void Startup(CalibrationData calibration, int port)
    {
        Console.WriteLine($"[ECU Emulator] ME221 ECU Emulator v0.1 -- Listening on 127.0.0.1:{port}");
        Console.WriteLine($"[ECU Emulator] Loaded: {calibration.Metadata.ProductName} {calibration.Metadata.ModelName} v{calibration.Metadata.Version}");
        Console.WriteLine($"[ECU Emulator] {calibration.DataLinks.Count} data links, {calibration.Tables.Count} tables, {calibration.Drivers.Count} drivers");

        lock (_lock)
        {
            _linkByName.Clear();
            foreach (var link in calibration.DataLinks)
                _linkByName[link.Name] = link.Id;
        }
    }

    public virtual void Connected(string sessionId)
    {
        Console.WriteLine($"[CONNECT][{sessionId}] Client connected");
    }

    public virtual void Disconnected(string sessionId, bool abnormal)
    {
        Console.WriteLine($"[DISCONNECT][{sessionId}] Client disconnected{(abnormal ? " (abnormal)" : "")}");
    }

    public virtual void FrameReceived(string sessionId)
    {
    }

    public virtual void FrameSent(string sessionId)
    {
    }

    public virtual void StateChange(string sessionId, string message)
    {
        Console.WriteLine($"[STATE][{sessionId}] {message}");
    }

    public virtual void Error(string message)
    {
        Console.WriteLine($"[ERROR] {message}");
    }

    public void AttachSimulator(SensorSimulator sim)
    {
        lock (_lock)
            _attachedSimulator = sim;
    }

    public void StartCommandLoop()
    {
        Task.Run(RunCommandLoop);
    }

    private void RunCommandLoop()
    {
        while (true)
        {
            var line = Console.ReadLine();
            if (line is null) return;
            ExecuteCommand(line);
        }
    }

    private void ExecuteCommand(string line)
    {
        var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        switch (parts[0].ToLowerInvariant())
        {
            case "set" when parts.Length >= 3:
                if (!TryResolveLink(parts[1], out var setId))
                {
                    Error($"Unknown link '{parts[1]}' — use an id or a name from the loaded calibration");
                    break;
                }
                if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var setValue))
                {
                    Error($"Invalid value '{parts[2]}' — expected a number");
                    break;
                }
                _attachedSimulator?.SetOverride(setId, setValue);
                Console.WriteLine($"[INJECT] set {parts[1]} ({setId}) = {setValue.ToString("F2", CultureInfo.InvariantCulture)}");
                break;

            case "clear" when parts.Length >= 2:
                if (!TryResolveLink(parts[1], out var clearId))
                {
                    Error($"Unknown link '{parts[1]}' — use an id or a name from the loaded calibration");
                    break;
                }
                _attachedSimulator?.ClearOverride(clearId);
                lock (_lock)
                    _excursionTicks.Remove(clearId);
                Console.WriteLine($"[INJECT] clear {parts[1]} ({clearId})");
                break;

            case "excursion" when parts.Length >= 4:
                if (!TryResolveLink(parts[1], out var excId))
                {
                    Error($"Unknown link '{parts[1]}' — use an id or a name from the loaded calibration");
                    break;
                }
                if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var excValue) ||
                    !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var excMs))
                {
                    Error("Invalid excursion arguments — expected <link> <value> <ms>");
                    break;
                }
                _attachedSimulator?.SetOverride(excId, excValue);
                var ticks = Math.Max(1, excMs / 100);
                lock (_lock)
                    _excursionTicks[excId] = ticks;
                Console.WriteLine($"[INJECT] excursion {parts[1]} ({excId}) = {excValue.ToString("F2", CultureInfo.InvariantCulture)} for {excMs}ms ({ticks} ticks)");
                break;

            case "hold" when parts.Length >= 2 && parts[1].Equals("on", StringComparison.OrdinalIgnoreCase):
                DrivingModeHold = true;
                _attachedSimulator?.SetDrivingModeHold(true);
                Console.WriteLine("[INJECT] driving mode hold ON");
                break;

            case "hold" when parts.Length >= 2 && parts[1].Equals("off", StringComparison.OrdinalIgnoreCase):
                DrivingModeHold = false;
                _attachedSimulator?.SetDrivingModeHold(false);
                Console.WriteLine("[INJECT] driving mode hold OFF");
                break;

            case "help":
                Console.WriteLine("[INJECT] commands:");
                Console.WriteLine("[INJECT]   set <link> <value>             pin a data link to a fixed value");
                Console.WriteLine("[INJECT]   clear <link>                   restore the simulated value");
                Console.WriteLine("[INJECT]   excursion <link> <value> <ms>   pin a value, auto-clear after <ms>");
                Console.WriteLine("[INJECT]   hold on | hold off             freeze the driving mode");
                Console.WriteLine("[INJECT]   help                           show this list");
                break;

            default:
                Error($"Unknown command '{parts[0]}' — type 'help' for the command list");
                break;
        }
    }

    private bool TryResolveLink(string token, out ushort id)
    {
        if (ushort.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
            return true;
        lock (_lock)
            return _linkByName.TryGetValue(token, out id);
    }

    /// <summary>
    /// Advance the scripted excursion timeline one tick. Clears expired excursions.
    /// Called once per reporting tick by the ReportingOrchestrator.
    /// </summary>
    public void AdvanceScript()
    {
        lock (_lock)
        {
            if (_excursionTicks.Count == 0) return;

            var expired = new List<ushort>();
            foreach (var (linkId, remaining) in _excursionTicks)
            {
                if (remaining <= 1)
                    expired.Add(linkId);
                else
                    _excursionTicks[linkId] = remaining - 1;
            }

            foreach (var linkId in expired)
            {
                _excursionTicks.Remove(linkId);
                _attachedSimulator?.ClearOverride(linkId);
                Console.WriteLine($"[INJECT] excursion {linkId} expired");
            }
        }
    }
}
