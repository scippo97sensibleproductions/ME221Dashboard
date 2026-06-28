using System.Buffers.Binary;
using System.Text;
using ME221.Comms.Internal;

namespace ME221.Comms.Messages;

// ─── Device Information Requests ────────────────────────────────────────

/// <summary>
/// Request to get ECU information (product name, model, firmware version, UUID, hash).
/// Command: Sys / GetECUInfo (class 0x04, command 0x00).
/// </summary>
public sealed class GetEcuInfoRequest() : Request(WireFormat.ClassSys, WireFormat.SysGetEcuInfo, Array.Empty<byte>());

/// <summary>
/// Request to get the ECU hash (overall or detailed).
/// Command: Sys / GetHash (class 0x04, command 0x01).
/// </summary>
public sealed class GetHashRequest(HashRequestMode mode)
    : Request(WireFormat.ClassSys, WireFormat.SysGetHash, new[] { (byte)mode })
{
    /// <summary>The hash request mode.</summary>
    public HashRequestMode Mode { get; } = mode;
}

/// <summary>
/// Request to set the ECU real-time clock.
/// Command: Sys / SetRTC (class 0x04, command 0x02).
/// </summary>
public sealed class SetRtcRequest(byte[] payload) : Request(WireFormat.ClassSys, WireFormat.SysSetRtc, payload);

/// <summary>
/// Request to perform a factory reset on the ECU.
/// Command: Sys / FactoryReset (class 0x04, command 0x03).
/// </summary>
public sealed class FactoryResetRequest()
    : Request(WireFormat.ClassSys, WireFormat.SysFactoryReset, Array.Empty<byte>());

/// <summary>
/// Request to set the password lock state (lock/unlock).
/// Command: Sys / PWLockSetState (class 0x04, command 0x04).
/// </summary>
public sealed class PwLockSetStateRequest(byte[] payload)
    : Request(WireFormat.ClassSys, WireFormat.SysPwLockSetState, payload);

/// <summary>
/// Request to get the current password lock state.
/// Command: Sys / PWLockGetState (class 0x04, command 0x05).
/// </summary>
public sealed class PwLockGetStateRequest()
    : Request(WireFormat.ClassSys, WireFormat.SysPwLockGetState, Array.Empty<byte>());

/// <summary>
/// Request to perform a race unlock on the ECU.
/// Command: Sys / RaceUnlock (class 0x04, command 0x06).
/// Payload: encrypted UUID bytes used for competition unlock.
/// </summary>
public sealed class RaceUnlockRequest(ReadOnlySpan<byte> encUuid)
    : Request(WireFormat.ClassSys, WireFormat.SysRaceUnlock, encUuid.ToArray().AsMemory())
{
    public RaceUnlockRequest() : this(ReadOnlySpan<byte>.Empty) { }
}

// ─── Device Information Responses ───────────────────────────────────────

/// <summary>
/// Response containing ECU identification information.
///
/// Payload layout (after status byte):
///   UTF-8 strings separated by null bytes:
///   productName\0modelName\0version\0ECUFwVersion\0[UUID]\0[Hash]\0[PW]
/// </summary>
public sealed class GetEcuInfoResponse : Response
{
    /// <summary>Product name string.</summary>
    public string ProductName { get; }

    /// <summary>Model name string.</summary>
    public string ModelName { get; }

    /// <summary>Software version string.</summary>
    public string Version { get; }

    /// <summary>ECU firmware version string.</summary>
    public string EcuFirmwareVersion { get; }

    /// <summary>ECU UUID (if present).</summary>
    public string? Uuid { get; }

    /// <summary>Overall hash value in hex (if present).</summary>
    public ushort? Hash { get; }

    /// <summary>Whether the ECU has a hash.</summary>
    public bool HasHash { get; }

    /// <summary>Whether the ECU supports password protection.</summary>
    public bool PasswordProtectionFeature { get; }

    public GetEcuInfoResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassSys, WireFormat.SysGetEcuInfo, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length <= 1)
        {
            ProductName = string.Empty;
            ModelName = string.Empty;
            Version = string.Empty;
            EcuFirmwareVersion = string.Empty;
            return;
        }

        var data = payload[1..];
        var strings = SplitNullTerminated(data);

        ProductName = strings.Length > 0 && strings[0] is not null ? Encoding.UTF8.GetString(strings[0]) : string.Empty;
        ModelName = strings.Length > 1 && strings[1] is not null ? Encoding.UTF8.GetString(strings[1]) : string.Empty;
        Version = strings.Length > 2 && strings[2] is not null ? Encoding.UTF8.GetString(strings[2]) : string.Empty;
        EcuFirmwareVersion = strings.Length > 3 && strings[3] is not null ? Encoding.UTF8.GetString(strings[3]) : string.Empty;

        if (strings.Length > 4 && strings[4] is not null && strings[4].Length > 0)
            Uuid = Encoding.UTF8.GetString(strings[4]);

        if (strings.Length > 5 && strings[5] is not null && strings[5].Length > 0)
        {
            HasHash = true;
            Hash = ParseHexUShort(strings[5]);
        }

        if (strings.Length > 6 && strings[6] is not null && strings[6].Length >= 2 &&
            strings[6][0] == (byte)'P' && strings[6][1] == (byte)'W')
        {
            PasswordProtectionFeature = true;
        }
    }

    private static byte[][] SplitNullTerminated(ReadOnlySpan<byte> data)
    {
        // Count null bytes to allocate exact array
        var count = 1;
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] == 0) count++;
        }

        var result = new byte[count][];
        var index = 0;
        var start = 0;

        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] == 0)
            {
                var segmentLength = i - start;
                result[index++] = data.Slice(start, segmentLength).ToArray();
                start = i + 1;
            }
        }
        // Last segment (after final null, or entire data if no nulls)
        if (start < data.Length)
        {
            var segmentLength = data.Length - start;
            result[index] = data.Slice(start, segmentLength).ToArray();
        }

        return result;
    }

    private static ushort? ParseHexUShort(ReadOnlySpan<byte> span)
    {
        try
        {
            return BinaryPrimitives.ReadUInt16BigEndian(span);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Response to a GetHash request, containing overall and/or detailed hash data.
///
/// Payload layout (after status byte):
///   [1 byte mode] [2 bytes overall hash LE] [if DETAILED: 2 bytes count LE, then count × (2 bytes ID LE + 2 bytes hash LE)]
/// </summary>
public sealed class GetHashResponse : Response
{
    /// <summary>The hash request mode that was answered.</summary>
    public HashRequestMode Mode { get; }

    /// <summary>Overall ECU hash value.</summary>
    public ushort OverallHash { get; }

    /// <summary>Per-entity hashes (only populated when Mode is Detailed).</summary>
    public Dictionary<ushort, ushort> Hashes { get; } = new();

    public GetHashResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassSys, WireFormat.SysGetHash, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length < 3) return;

        Mode = (HashRequestMode)payload[1];
        OverallHash = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]);

        if (Mode == HashRequestMode.Detailed && payload.Length > 4)
        {
            var offset = 4;
            var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset));
            offset += 2;

            for (ushort i = 0; i < count; i++)
            {
                if (offset + 4 <= payload.Length)
                {
                    var id = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset));
                    offset += 2;
                    var hash = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset));
                    offset += 2;
                    Hashes[id] = hash;
                }
            }
        }
    }
}

/// <summary>
/// Response to a SetRTC request.
/// </summary>
public sealed class SetRtcResponse(MessageStatus status) : Response(WireFormat.ClassSys, WireFormat.SysSetRtc, status);

/// <summary>
/// Response to a FactoryReset request.
/// </summary>
public sealed class FactoryResetResponse(MessageStatus status)
    : Response(WireFormat.ClassSys, WireFormat.SysFactoryReset, status);

/// <summary>
/// Response to a PWLockSetState request.
/// </summary>
public sealed class PwLockSetStateResponse(MessageStatus status)
    : Response(WireFormat.ClassSys, WireFormat.SysPwLockSetState, status);

/// <summary>
/// Response to a PWLockGetState request.
///
/// Payload layout (after status byte):
///   [1 byte locked] [1 byte tunerContact]
/// </summary>
public sealed class PwLockGetStateResponse : Response
{
    /// <summary>Whether the ECU is currently locked (1 = locked).</summary>
    public byte Locked { get; }

    /// <summary>Whether a tuner is currently in contact (1 = yes).</summary>
    public byte TunerContact { get; }

    public PwLockGetStateResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassSys, WireFormat.SysPwLockGetState, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length > 1)
            Locked = payload[1];
        if (payload.Length > 2)
            TunerContact = payload[2];
    }
}

/// <summary>
/// Response to a RaceUnlock request.
/// </summary>
public sealed class RaceUnlockResponse(MessageStatus status)
    : Response(WireFormat.ClassSys, WireFormat.SysRaceUnlock, status);
