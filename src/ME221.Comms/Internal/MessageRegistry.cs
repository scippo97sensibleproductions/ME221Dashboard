using System.Buffers.Binary;
using ME221.Comms.Messages;

namespace ME221.Comms.Internal;

/// <summary>
/// Static registry that maps (messageType, messageClass, messageCommand) tuples
/// to concrete <see cref="MessageFrame"/> instances.
///
/// Built at assembly load time — zero reflection on the hot path.
/// </summary>
internal static class MessageRegistry
{
    /// <summary>
    /// Factory delegate type for creating message frames from payload.
    /// </summary>
    private delegate MessageFrame FactoryFn(ReadOnlySpan<byte> payload);

    /// <summary>
    /// Maps wire-format tuples to factory functions.
    /// Key: (type, class, command) → Value: function that creates the message.
    /// </summary>
    private static readonly Dictionary<(byte Type, byte Class, byte Command), FactoryFn> Factories = new()
    {
        // ── Device Information (Sys class, 0x04) ─────────────────────────
        { (WireFormat.ResponseType, WireFormat.ClassSys, WireFormat.SysGetEcuInfo), CreateGetEcuInfoResponse },
        { (WireFormat.RequestType,  WireFormat.ClassSys, WireFormat.SysGetEcuInfo), _ => new GetEcuInfoRequest() },

        { (WireFormat.ResponseType, WireFormat.ClassSys, WireFormat.SysGetHash), p => new GetHashResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassSys, WireFormat.SysGetHash), p => new GetHashRequest((HashRequestMode)p[0]) },

        { (WireFormat.ResponseType, WireFormat.ClassSys, WireFormat.SysSetRtc), p => CreateResponse(WireFormat.ClassSys, WireFormat.SysSetRtc, p) },
        { (WireFormat.RequestType,  WireFormat.ClassSys, WireFormat.SysSetRtc), p => new SetRtcRequest(p.ToArray()) },

        { (WireFormat.ResponseType, WireFormat.ClassSys, WireFormat.SysFactoryReset), p => CreateResponse(WireFormat.ClassSys, WireFormat.SysFactoryReset, p) },
        { (WireFormat.RequestType,  WireFormat.ClassSys, WireFormat.SysFactoryReset), _ => new FactoryResetRequest() },

        { (WireFormat.ResponseType, WireFormat.ClassSys, WireFormat.SysPwLockSetState), p => CreateResponse(WireFormat.ClassSys, WireFormat.SysPwLockSetState, p) },
        { (WireFormat.RequestType,  WireFormat.ClassSys, WireFormat.SysPwLockSetState), p => new PwLockSetStateRequest(p.ToArray()) },

        { (WireFormat.ResponseType, WireFormat.ClassSys, WireFormat.SysPwLockGetState), p => new PwLockGetStateResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassSys, WireFormat.SysPwLockGetState), _ => new PwLockGetStateRequest() },

        { (WireFormat.ResponseType, WireFormat.ClassSys, WireFormat.SysRaceUnlock), p => CreateResponse(WireFormat.ClassSys, WireFormat.SysRaceUnlock, p) },
        { (WireFormat.RequestType,  WireFormat.ClassSys, WireFormat.SysRaceUnlock), p => new RaceUnlockRequest(p) },

        // ── Drivers (class 0x02) ─────────────────────────────────────────
        { (WireFormat.ResponseType, WireFormat.ClassDrivers, WireFormat.DriversSetDriver), p => new SetDriverResponse(p) },

        { (WireFormat.ResponseType, WireFormat.ClassDrivers, WireFormat.DriversGetDriver), p => new GetDriverResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassDrivers, WireFormat.DriversGetDriver), p => new GetDriverRequest(BinaryPrimitives.ReadUInt16LittleEndian(p)) },

        { (WireFormat.ResponseType, WireFormat.ClassDrivers, WireFormat.DriversStoreInNvm), p => new StoreDriverResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassDrivers, WireFormat.DriversStoreInNvm), p => new StoreDriverRequest(BinaryPrimitives.ReadUInt16LittleEndian(p)) },

        // ── Tables (class 0x01) ──────────────────────────────────────────
        { (WireFormat.ResponseType, WireFormat.ClassTables, WireFormat.TablesSetTable), p => new SetTableResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassTables, WireFormat.TablesSetTable),
            p => { var id = BinaryPrimitives.ReadUInt16LittleEndian(p); var sz = BinaryPrimitives.ReadUInt16LittleEndian(p[2..]); return new SetTableRequest(id, p.Slice(4, sz).ToArray()); } },

        { (WireFormat.ResponseType, WireFormat.ClassTables, WireFormat.TablesGetTable), p => new GetTableResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassTables, WireFormat.TablesGetTable), p => new GetTableRequest(BinaryPrimitives.ReadUInt16LittleEndian(p)) },

        { (WireFormat.ResponseType, WireFormat.ClassTables, WireFormat.TablesEnableTable), p => new EnableTableResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassTables, WireFormat.TablesEnableTable), p => new EnableTableRequest(BinaryPrimitives.ReadUInt16LittleEndian(p)) },

        { (WireFormat.ResponseType, WireFormat.ClassTables, WireFormat.TablesDisableTable), p => new DisableTableResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassTables, WireFormat.TablesDisableTable), p => new DisableTableRequest(BinaryPrimitives.ReadUInt16LittleEndian(p)) },

        { (WireFormat.ResponseType, WireFormat.ClassTables, WireFormat.TablesSetDataAtOffsets), p => new SetTableDataResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassTables, WireFormat.TablesSetDataAtOffsets), p => new SetTableDataRequest(0, p.ToArray()) },

        { (WireFormat.ResponseType, WireFormat.ClassTables, WireFormat.TablesGetDataAtOffset), p => new GetTableDataResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassTables, WireFormat.TablesGetDataAtOffset), p => new GetTableDataRequest(0, p.ToArray()) },

        { (WireFormat.ResponseType, WireFormat.ClassTables, WireFormat.TablesStoreInNvm), p => new StoreTableResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassTables, WireFormat.TablesStoreInNvm), p => new StoreTableRequest(BinaryPrimitives.ReadUInt16LittleEndian(p)) },

        { (WireFormat.ResponseType, WireFormat.ClassTables, WireFormat.TablesSetTableReporting), p => new SetTableReportingResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassTables, WireFormat.TablesSetTableReporting), p => new SetTableReportingRequest(0, p.ToArray()) },

        // ── Reporting (class 0x00) ───────────────────────────────────────
        { (WireFormat.ResponseType, WireFormat.ClassReporting, WireFormat.ReportingSendReport), payload => new SendReportResponse(payload) },
        { (WireFormat.RequestType,  WireFormat.ClassReporting, WireFormat.ReportingSendReport), _ => new SendReportRequest() },

        { (WireFormat.ResponseType, WireFormat.ClassReporting, WireFormat.ReportingSendAck), p => CreateResponse(WireFormat.ClassReporting, WireFormat.ReportingSendAck, p) },
        { (WireFormat.RequestType,  WireFormat.ClassReporting, WireFormat.ReportingSendAck), _ => SendAckRequest.Instance },

        { (WireFormat.ResponseType, WireFormat.ClassReporting, WireFormat.ReportingSetState), p => new SetStateResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassReporting, WireFormat.ReportingSetState), p => new SetStateRequest(p.Length > 0 && p[0] != 0) },

        { (WireFormat.ResponseType, WireFormat.ClassReporting, WireFormat.ReportingSetSpecialCfg), p => new SetSpecialCfgResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassReporting, WireFormat.ReportingSetSpecialCfg), p => new SetSpecialCfgRequest(p.ToArray()) },

        // ── Firmware Update (class 0x05) ─────────────────────────────────
        { (WireFormat.ResponseType, WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateStartFirmwareUpdate), p => CreateResponse(WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateStartFirmwareUpdate, p) },
        { (WireFormat.RequestType,  WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateStartFirmwareUpdate), p => new StartFwUpdateRequest(p.ToArray()) },

        { (WireFormat.ResponseType, WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateRegionInfoGet), p => CreateResponse(WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateRegionInfoGet, p) },
        { (WireFormat.RequestType,  WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateRegionInfoGet), p => new RegionInfoGetRequest(p[0]) },

        { (WireFormat.ResponseType, WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateDataGet), p => CreateResponse(WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateDataGet, p) },
        { (WireFormat.RequestType,  WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateDataGet), p => new RegionDataGetRequest(p.ToArray()) },

        { (WireFormat.ResponseType, WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateStatusReport), _ => new StatusReportResponse(MessageStatus.Success) },

        { (WireFormat.ResponseType, WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateEnterBlMode), p => CreateResponse(WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateEnterBlMode, p) },
        { (WireFormat.RequestType,  WireFormat.ClassFirmwareUpdate, WireFormat.FirmwareUpdateEnterBlMode), _ => new EnterBlModeRequest() },

        // ── Data Log (class 0x06) ────────────────────────────────────────
        { (WireFormat.ResponseType, WireFormat.ClassDataLog, WireFormat.DataLogIsSupported), p => new IsSupportedResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassDataLog, WireFormat.DataLogIsSupported), _ => new IsSupportedRequest() },

        { (WireFormat.ResponseType, WireFormat.ClassDataLog, WireFormat.DataLogGetConfig), p => new GetConfigResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassDataLog, WireFormat.DataLogGetConfig), _ => new GetConfigRequest() },

        { (WireFormat.ResponseType, WireFormat.ClassDataLog, WireFormat.DataLogSetConfig), p => CreateResponse(WireFormat.ClassDataLog, WireFormat.DataLogSetConfig, p) },
        { (WireFormat.RequestType,  WireFormat.ClassDataLog, WireFormat.DataLogSetConfig), p => new SetConfigRequest(p.ToArray()) },

        { (WireFormat.ResponseType, WireFormat.ClassDataLog, WireFormat.DataLogStart), p => CreateResponse(WireFormat.ClassDataLog, WireFormat.DataLogStart, p) },
        { (WireFormat.RequestType,  WireFormat.ClassDataLog, WireFormat.DataLogStart), _ => new StartRequest() },

        { (WireFormat.ResponseType, WireFormat.ClassDataLog, WireFormat.DataLogStop), p => CreateResponse(WireFormat.ClassDataLog, WireFormat.DataLogStop, p) },
        { (WireFormat.RequestType,  WireFormat.ClassDataLog, WireFormat.DataLogStop), _ => new StopRequest() },

        { (WireFormat.ResponseType, WireFormat.ClassDataLog, WireFormat.DataLogGetLogsSummary), p => CreateResponse(WireFormat.ClassDataLog, WireFormat.DataLogGetLogsSummary, p) },
        { (WireFormat.RequestType,  WireFormat.ClassDataLog, WireFormat.DataLogGetLogsSummary), _ => new GetLogsSummaryRequest() },

        { (WireFormat.ResponseType, WireFormat.ClassDataLog, WireFormat.DataLogGetLogDetail), p => CreateResponse(WireFormat.ClassDataLog, WireFormat.DataLogGetLogDetail, p) },
        { (WireFormat.RequestType,  WireFormat.ClassDataLog, WireFormat.DataLogGetLogDetail), p => new GetLogDetailRequest(BinaryPrimitives.ReadUInt16LittleEndian(p)) },

        { (WireFormat.ResponseType, WireFormat.ClassDataLog, WireFormat.DataLogGetLogRegion), p => CreateResponse(WireFormat.ClassDataLog, WireFormat.DataLogGetLogRegion, p) },
        { (WireFormat.RequestType,  WireFormat.ClassDataLog, WireFormat.DataLogGetLogRegion), p => new GetLogRegionRequest(p[0], BinaryPrimitives.ReadUInt32LittleEndian(p.Slice(1, 4)), BinaryPrimitives.ReadUInt32LittleEndian(p.Slice(5, 4))) },

        { (WireFormat.ResponseType, WireFormat.ClassDataLog, WireFormat.DataLogEraseLog), p => CreateResponse(WireFormat.ClassDataLog, WireFormat.DataLogEraseLog, p) },
        { (WireFormat.RequestType,  WireFormat.ClassDataLog, WireFormat.DataLogEraseLog), _ => new EraseLogRequest() },

        { (WireFormat.ResponseType, WireFormat.ClassDataLog, WireFormat.DataLogFormatMemory), p => CreateResponse(WireFormat.ClassDataLog, WireFormat.DataLogFormatMemory, p) },
        { (WireFormat.RequestType,  WireFormat.ClassDataLog, WireFormat.DataLogFormatMemory), _ => new FormatMemoryRequest() },

        // ── DBW (class 0x08) ─────────────────────────────────────────────
        { (WireFormat.ResponseType, WireFormat.ClassDbw, WireFormat.DbwSetDbwDuty), p => CreateResponse(WireFormat.ClassDbw, WireFormat.DbwSetDbwDuty, p) },
        { (WireFormat.RequestType,  WireFormat.ClassDbw, WireFormat.DbwSetDbwDuty), p => new SetDbwDutyRequest(p.ToArray()) },

        // ── Trigger Logger (class 0x07) ──────────────────────────────────
        { (WireFormat.ResponseType, WireFormat.ClassTriggerLogger, WireFormat.TriggerLoggerIsSupported), p => new TriggerLoggerIsSupportedResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassTriggerLogger, WireFormat.TriggerLoggerIsSupported), _ => new TriggerLoggerIsSupportedRequest() },

        { (WireFormat.ResponseType, WireFormat.ClassTriggerLogger, WireFormat.TriggerLoggerSetState), p => new TriggerLoggerSetStateResponse(p) },
        { (WireFormat.RequestType,  WireFormat.ClassTriggerLogger, WireFormat.TriggerLoggerSetState), p => new TriggerLoggerSetStateRequest(p.Length > 0 && p[0] != 0, p.Length > 1 ? p[1] : (byte)0) },

        { (WireFormat.ResponseType, WireFormat.ClassTriggerLogger, WireFormat.TriggerLoggerReport), p => new ReportResponse(p) },
    };

    /// <summary>
    /// Looks up a concrete message type by wire-format fields and creates an instance.
    /// </summary>
    /// <param name="type">Message type byte.</param>
    /// <param name="classId">Message class byte.</param>
    /// <param name="command">Message command byte.</param>
    /// <param name="payload">The payload span.</param>
    /// <returns>A new <see cref="MessageFrame"/> instance, or <c>null</c> if no factory is registered.</returns>
    public static MessageFrame? Create(byte type, byte classId, byte command, ReadOnlySpan<byte> payload)
    {
        if (Factories.TryGetValue((type, classId, command), out var factory))
        {
            return factory(payload);
        }
        return null;
    }

    // ── Factory delegates ──────────────────────────────────────────────

    private static MessageFrame CreateGetEcuInfoResponse(ReadOnlySpan<byte> payload)
    {
        return new GetEcuInfoResponse(payload);
    }

    /// <summary>
    /// Creates a simple response for a given class and command.
    /// Forwards the wire payload directly — single allocation in the constructor.
    /// </summary>
    private static SimpleResponse CreateResponse(byte classId, byte command, ReadOnlySpan<byte> payload)
    {
        return new SimpleResponse(classId, command, payload);
    }

    /// <summary>
    /// A simple response class for responses that only carry a status byte.
    /// Accepts span — allocation happens in the constructor via ToArray().AsMemory().
    /// </summary>
    private sealed class SimpleResponse(byte classId, byte command, ReadOnlySpan<byte> payload)
        : Response(classId, command, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Success, payload.ToArray());
}
