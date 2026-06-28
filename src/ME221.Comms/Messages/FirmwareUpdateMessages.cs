using System.Buffers.Binary;
using ME221.Comms.Internal;

namespace ME221.Comms.Messages;

/// <summary>
/// Request to start a firmware update.
/// Command: FirmwareUpdate / StartFirmwareUpdate (class 0x05, command 0x00).
/// </summary>
public sealed class StartFwUpdateRequest(byte[] payload) : Request(WireFormat.ClassFirmwareUpdate,
    WireFormat.FirmwareUpdateStartFirmwareUpdate, payload);

/// <summary>
/// Response to a StartFWUpdate request.
/// </summary>
public sealed class StartFwUpdateResponse(MessageStatus status) : Response(WireFormat.ClassFirmwareUpdate,
    WireFormat.FirmwareUpdateStartFirmwareUpdate, status);

/// <summary>
/// Request to get firmware region information.
/// Command: FirmwareUpdate / RegionInfoGet (class 0x05, command 0x01).
/// Payload: 1 byte — region index.
/// </summary>
public sealed class RegionInfoGetRequest(byte regionIdx) : Request(WireFormat.ClassFirmwareUpdate,
    WireFormat.FirmwareUpdateRegionInfoGet, new[] { regionIdx })
{
    public byte RegionIdx { get; } = regionIdx;
}

/// <summary>
/// Response to a RegionInfoGet request.
/// </summary>
public sealed class RegionInfoGetResponse(MessageStatus status) : Response(WireFormat.ClassFirmwareUpdate,
    WireFormat.FirmwareUpdateRegionInfoGet, status);

/// <summary>
/// Request to get firmware region data.
/// Command: FirmwareUpdate / DataGet (class 0x05, command 0x02).
/// Payload: 2-byte entity ID (LE) + data request parameters.
/// </summary>
public sealed class RegionDataGetRequest(byte[] payload)
    : Request(WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateDataGet, payload)
{
    public ushort RegionId { get; } = BinaryPrimitives.ReadUInt16LittleEndian(payload);
}

/// <summary>
/// Response to a RegionDataGet request.
/// </summary>
public sealed class RegionDataGetResponse(MessageStatus status)
    : Response(WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateDataGet, status);

/// <summary>
/// Firmware update status report (sent by ECU during update).
/// Command: FirmwareUpdate / StatusReport (class 0x05, command 0x03).
/// </summary>
public sealed class StatusReportResponse(MessageStatus status)
    : Response(WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateStatusReport, status);

/// <summary>
/// Request to enter bootloader mode.
/// Command: FirmwareUpdate / EnterBLMode (class 0x05, command 0x04).
/// </summary>
public sealed class EnterBlModeRequest() : Request(WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateEnterBlMode,
    Array.Empty<byte>());

/// <summary>
/// Response to an EnterBLMode request.
/// </summary>
public sealed class EnterBlModeResponse(MessageStatus status)
    : Response(WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateEnterBlMode, status);
