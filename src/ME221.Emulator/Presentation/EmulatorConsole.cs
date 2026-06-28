using ME221.Data.Models;

namespace ME221.Emulator.Presentation;

public class EmulatorConsole
{
    public virtual void Startup(CalibrationData calibration, int port)
    {
        Console.WriteLine($"[ECU Emulator] ME221 ECU Emulator v0.1 -- Listening on 127.0.0.1:{port}");
        Console.WriteLine($"[ECU Emulator] Loaded: {calibration.Metadata.ProductName} {calibration.Metadata.ModelName} v{calibration.Metadata.Version}");
        Console.WriteLine($"[ECU Emulator] {calibration.DataLinks.Count} data links, {calibration.Tables.Count} tables, {calibration.Drivers.Count} drivers");
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
}
