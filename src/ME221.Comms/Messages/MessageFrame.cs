namespace ME221.Comms.Messages;

/// <summary>
/// Base class for all ME221 protocol message frames.
/// Provides the wire-format fields and a <see cref="Switch"/> method for C# pattern-matching dispatch.
/// </summary>
public abstract class MessageFrame
{
    /// <summary>Message type: Request (0x00) or Response (0x0F).</summary>
    public byte Type { get; }

    /// <summary>Message class identifier (Reporting, Tables, Drivers, etc.).</summary>
    public byte Class { get; }

    /// <summary>Message command identifier within the class.</summary>
    public byte Command { get; }

    /// <summary>Payload data as a pooled byte array.</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    /// Creates a new <see cref="MessageFrame"/> from parsed wire bytes.
    /// </summary>
    /// <param name="type">Message type byte.</param>
    /// <param name="classId">Message class byte.</param>
    /// <param name="command">Message command byte.</param>
    /// <param name="payload">Payload span (not copied — caller must ensure lifetime).</param>
    protected MessageFrame(byte type, byte classId, byte command, ReadOnlyMemory<byte> payload)
    {
        Type = type;
        Class = classId;
        Command = command;
        Payload = payload;
    }

    /// <summary>
    /// Dispatches this frame to the appropriate handler using C# pattern matching.
    /// This replaces the old visitor pattern — no 40+ Visit() methods needed.
    /// </summary>
    /// <param name="handler">A delegate that handles the concrete message type.</param>
    /// <typeparam name="T">The return type of the handler.</typeparam>
    /// <returns>The result of the invoked handler.</returns>
    public T Switch<T>(Func<MessageFrame, T> handler)
    {
        return handler(this);
    }

    /// <summary>
    /// Returns a human-readable string showing type, class, command, and payload length.
    /// </summary>
    public override string ToString()
    {
        return $"{Type:X2} {Class:X2} {Command:X2} [{Payload.Length}]";
    }
}
