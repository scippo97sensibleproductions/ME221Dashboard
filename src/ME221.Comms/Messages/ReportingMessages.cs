using System.Buffers.Binary;
using ME221.Comms.Internal;

namespace ME221.Comms.Messages;

// ─── Reporting Requests ─────────────────────────────────────────────────

/// <summary>
/// Request to send a report.
/// Command: Reporting / SendReport (class 0x00, command 0x00).
/// </summary>
public sealed class SendReportRequest()
    : Request(WireFormat.ClassReporting, WireFormat.ReportingSendReport, Array.Empty<byte>());

/// <summary>
/// Request to send an acknowledgment (heartbeat/lifesign).
/// Command: Reporting / SendAck (class 0x00, command 0x01).
/// Stateless — reuse via <see cref="Instance"/>.
/// </summary>
public sealed class SendAckRequest : Request
{
    public static readonly SendAckRequest Instance = new();

    internal SendAckRequest()
        : base(WireFormat.ClassReporting, WireFormat.ReportingSendAck, Array.Empty<byte>()) { }
}

/// <summary>
/// Request to set reporting state (enable/disable).
/// Command: Reporting / SetState (class 0x00, command 0x02).
/// Payload: 1 byte — non-zero = enabled, zero = disabled.
/// </summary>
public sealed class SetStateRequest(bool enabled) : Request(WireFormat.ClassReporting, WireFormat.ReportingSetState,
    new[] { (byte)(enabled ? 1 : 0) })
{
    public bool Enabled { get; } = enabled;
}

/// <summary>
/// Request to set special configuration.
/// Command: Reporting / SetSpecialCfg (class 0x00, command 0x03).
/// </summary>
public sealed class SetSpecialCfgRequest(byte[] payload)
    : Request(WireFormat.ClassReporting, WireFormat.ReportingSetSpecialCfg, payload);

// ─── Reporting Responses ────────────────────────────────────────────────

/// <summary>
/// Periodic report from the ECU containing live sensor values.
/// Sent every ~100ms when reporting is enabled.
///
/// Payload formats:
///   V1: [2 bytes entity count LE] [for each entity: 2 bytes ID LE + 4 bytes float LE]
///   V2: [2 bytes entity count LE] [for each entity: 2 bytes ID LE + 1 byte type + N bytes value LE]
///       where N depends on <see cref="ReportingType"/> (float=4, int16=2, uint16=2, int8=1, uint8=1, bool=1)
/// </summary>
public sealed class SendReportResponse : Response
{
    /// <summary>The reporting protocol version.</summary>
    public ReportingVersion Version { get; }

    /// <summary>The report entities with ID and value pairs.</summary>
    public ReadOnlyMemory<ReportEntity> Entities { get; }

    /// <summary>
    /// Creates a <see cref="SendReportResponse"/> from a payload, auto-detecting the format.
    ///
    /// Detected formats (tried in order):
    ///   1. V1 self-describing: [status:1][count:2 LE][entity × N: each [id:2][value:4] = 6B]
    ///   2. V2 self-describing: [status:1][count:2 LE][entity × N: each [id:2][type:1][value:N]]
    ///   3. V2 raw (no entities parsed): [status:1][raw values] — needs entity map
    /// </summary>
    public SendReportResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassReporting, WireFormat.ReportingSendReport, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length < 3)
        {
            Version = ReportingVersion.V1;
            Entities = ReadOnlyMemory<ReportEntity>.Empty;
            return;
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
        if (count == 0)
        {
            Version = ReportingVersion.V1;
            Entities = ReadOnlyMemory<ReportEntity>.Empty;
            return;
        }

        const int v1DataStart = 3;
        var v1EntitySize = sizeof(ushort) + sizeof(float);

        // V1: fixed-size entities [id:2][value:4] = 6B each
        if (payload.Length == v1DataStart + count * v1EntitySize)
        {
            Version = ReportingVersion.V1;
            var entities = new ReportEntity[count];
            var offset = v1DataStart;
            for (var i = 0; i < count; i++)
            {
                entities[i] = ReportEntity.Parse(payload.Slice(offset));
                offset += v1EntitySize;
            }
            Entities = entities.AsMemory();
            return;
        }

        // V2 self-describing: variable-size entities [id:2][type:1][value:size(type)]
        Version = ReportingVersion.V2;
        var parsed = TryParseV2SelfDescribing(payload, count, v1DataStart, out var v2Entities);
        if (parsed > 0)
        {
            Entities = v2Entities.AsMemory();
        }
        else
        {
            Entities = ReadOnlyMemory<ReportEntity>.Empty;
        }
    }

    private static int TryParseV2SelfDescribing(ReadOnlySpan<byte> payload, int count, int dataStart, out ReportEntity[] entities)
    {
        entities = [];
        if (count <= 0 || dataStart >= payload.Length)
            return 0;

        var result = new ReportEntity[count];
        var offset = dataStart;
        var parsedCount = 0;

        for (var i = 0; i < count && offset + 3 <= payload.Length; i++)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset));
            offset += 2;
            var type = payload[offset++];
            var size = type switch
            {
                0 => 4,
                1 => 2,
                2 => 2,
                3 => 1,
                4 => 1,
                5 => 1,
                _ => 4,
            };
            if (offset + size > payload.Length)
                break;

            var value = type switch
            {
                0 => BinaryPrimitives.ReadSingleLittleEndian(payload.Slice(offset)),
                1 => BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(offset)),
                2 => BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset)),
                3 => (sbyte)payload[offset],
                4 => payload[offset],
                5 => payload[offset],
                _ => BinaryPrimitives.ReadSingleLittleEndian(payload.Slice(offset)),
            };
            offset += size;
            result[parsedCount++] = new ReportEntity(id, value);
        }

        if (parsedCount > 0)
        {
            if (parsedCount < result.Length)
                Array.Resize(ref result, parsedCount);
            entities = result;
        }
        return parsedCount;
    }

    /// <summary>
    /// Creates a <see cref="SendReportResponse"/> from a V2 payload using an external entity map.
    /// Matches MEITE: [status:1][value1][value2]... — no IDs, no type bytes.
    /// Order and types come from the <paramref name="reportedDataLinks"/> dictionary.
    /// </summary>
    public SendReportResponse(ReadOnlySpan<byte> payload, ReportingVersion version, Dictionary<ushort, (ReportingType Type, int Size)> reportedDataLinks)
        : base(WireFormat.ClassReporting, WireFormat.ReportingSendReport, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        Version = version;

        if (version != ReportingVersion.V2 || reportedDataLinks is null || reportedDataLinks.Count == 0)
        {
            Entities = ReadOnlyMemory<ReportEntity>.Empty;
            return;
        }

        var entities = new ReportEntity[reportedDataLinks.Count];
        var offset = 1; // skip status byte
        var i = 0;

        foreach (var (id, (type, size)) in reportedDataLinks)
        {
            if (offset + size > payload.Length)
                break;

            var value = type switch
            {
                ReportingType.Float4B => BinaryPrimitives.ReadSingleLittleEndian(payload.Slice(offset)),
                ReportingType.Int2B => BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(offset)),
                ReportingType.Uint2B => BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset)),
                ReportingType.Int1B => (sbyte)payload[offset],
                ReportingType.Uint1B => payload[offset],
                ReportingType.Bool1B => payload[offset],
                _ => BinaryPrimitives.ReadSingleLittleEndian(payload.Slice(offset)),
            };

            entities[i++] = new ReportEntity(id, value);
            offset += size;
        }

        if (i < entities.Length)
            Array.Resize(ref entities, i);

        Entities = entities.AsMemory();
    }
}

/// <summary>
/// Response to a SendAck request (used for ECU heartbeat/lifesign).
/// </summary>
public sealed class SendAckResponse(MessageStatus status)
    : Response(WireFormat.ClassReporting, WireFormat.ReportingSendAck, status);

/// <summary>
/// Response to a SetState request.
/// V1 (payload length == 1): status only, no entity map.
/// V2 (payload length > 1): [status:1][count:2 LE][entity × N] where each entity is [id:2 LE][type:1].
/// </summary>
public sealed class SetStateResponse : Response
{
    /// <summary>The reporting protocol version (V1 or V2).</summary>
    public ReportingVersion ProtoVersion { get; }

    /// <summary>Number of data-link entities reported.</summary>
    public int NumEntities { get; }

    /// <summary>Entity descriptors with ID and reporting type.</summary>
    public IReadOnlyList<EntityDescriptor> Entities { get; }

    public SetStateResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassReporting, WireFormat.ReportingSetState, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length <= 1)
        {
            ProtoVersion = ReportingVersion.V1;
            Entities = [];
            return;
        }

        ProtoVersion = ReportingVersion.V2;
        NumEntities = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
        var entities = new EntityDescriptor[NumEntities];
        var offset = 3;
        for (var i = 0; i < NumEntities; i++)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset));
            offset += 2;
            var type = (ReportingType)payload[offset++];
            entities[i] = new EntityDescriptor(id, type);
        }
        Entities = entities;
    }

    /// <summary>A single entity descriptor with a 16-bit ID and a reporting type.</summary>
    public readonly struct EntityDescriptor(ushort id, ReportingType type)
    {
        /// <summary>Data-link entity ID.</summary>
        public ushort Id { get; } = id;

        /// <summary>Reporting type (determines V2 payload byte size).</summary>
        public ReportingType Type { get; } = type;
    }
}

/// <summary>
/// Response to a SetSpecialCfg request.
/// Payload: [status:1][freq:1][count:2 LE][entity × N] where each entity is [id:2 LE][type:1].
/// </summary>
public sealed class SetSpecialCfgResponse : Response
{
    /// <summary>Reporting frequency in Hz.</summary>
    public byte Frequency { get; }

    /// <summary>Number of data-link entities in the special configuration.</summary>
    public int NumEntities { get; }

    /// <summary>Entity descriptors with ID and reporting type.</summary>
    public IReadOnlyList<SetStateResponse.EntityDescriptor> Entities { get; }

    public SetSpecialCfgResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassReporting, WireFormat.ReportingSetSpecialCfg, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length < 4)
        {
            Frequency = 0;
            NumEntities = 0;
            Entities = [];
            return;
        }

        Frequency = payload[1];
        NumEntities = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(2));
        var entities = new SetStateResponse.EntityDescriptor[NumEntities];
        var offset = 4;
        for (var i = 0; i < NumEntities; i++)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset));
            offset += 2;
            var type = (ReportingType)payload[offset++];
            entities[i] = new SetStateResponse.EntityDescriptor(id, type);
        }
        Entities = entities;
    }
}
