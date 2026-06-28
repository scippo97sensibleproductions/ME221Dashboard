namespace ME221.Comms.Internal;

/// <summary>
/// Constants that define the ME221 ECU communication wire protocol.
/// Every magic number in the protocol is named here for clarity.
/// No raw hex literals should appear in protocol code — use these constants instead.
/// </summary>
public static class WireFormat
{
    // ── Frame Synchronisation ──────────────────────────────────────────

    /// <summary>Sync byte 1: 'M' (0x4D).</summary>
    public const byte SyncByteOne = 0x4D;

    /// <summary>Sync byte 2: 'E' (0x45).</summary>
    public const byte SyncByteTwo = 0x45;

    /// <summary>Length of the sync prefix in bytes (2).</summary>
    public const int SyncLength = 2;

    // ── Header Field Offsets (relative to position after sync bytes) ───

    /// <summary>Offset of payload length field after sync bytes (0).</summary>
    public const int PayloadLengthOffsetInHeader = 0;

    /// <summary>Offset of message type field after sync bytes (2).</summary>
    public const int MessageTypeOffsetInHeader = 2;

    /// <summary>Offset of message class field after sync bytes (3).</summary>
    public const int MessageClassOffsetInHeader = 3;

    /// <summary>Offset of message command field after sync bytes (4).</summary>
    public const int MessageCommandOffsetInHeader = 4;

    /// <summary>Length of the fixed header after sync bytes (type + class + command = 3 bytes).</summary>
    public const int HeaderLengthAfterSync = 3;

    /// <summary>Length of the payload length field (2 bytes, little-endian).</summary>
    public const int PayloadLengthFieldLength = 2;

    /// <summary>
    /// Total fixed frame overhead: sync(2) + length(2) + type(1) + class(1) + command(1) + crc(2) = 9 bytes.
    /// Payload sits between the header and the CRC trailer.
    /// </summary>
    public const int FixedFrameOverhead = SyncLength
        + PayloadLengthFieldLength
        + HeaderLengthAfterSync
        + CrcLength;

    // ── Message Type Values ────────────────────────────────────────────

    /// <summary>Request message type (0x00).</summary>
    public const byte RequestType = 0x00;

    /// <summary>Response message type (0x0F).</summary>
    public const byte ResponseType = 0x0F;

    // ── Message Class Values ───────────────────────────────────────────

    /// <summary>Reporting class (0x00).</summary>
    public const byte ClassReporting = 0x00;

    /// <summary>Tables class (0x01).</summary>
    public const byte ClassTables = 0x01;

    /// <summary>Drivers class (0x02).</summary>
    public const byte ClassDrivers = 0x02;

    /// <summary>DataLinks class (0x03).</summary>
    public const byte ClassDataLinks = 0x03;

    /// <summary>System (Sys) class (0x04).</summary>
    public const byte ClassSys = 0x04;

    /// <summary>Firmware Update class (0x05).</summary>
    public const byte ClassFirmwareUpdate = 0x05;

    /// <summary>Data Log class (0x06).</summary>
    public const byte ClassDataLog = 0x06;

    /// <summary>Trigger Logger class (0x07).</summary>
    public const byte ClassTriggerLogger = 0x07;

    /// <summary>DBW (Duty Cycle) class (0x08).</summary>
    public const byte ClassDbw = 0x08;

    // ── Sys Class Commands (class 0x04) ────────────────────────────────

    /// <summary>Sys / GetECUInfo (command 0x00).</summary>
    public const byte SysGetEcuInfo = 0x00;

    /// <summary>Sys / GetHash (command 0x01).</summary>
    public const byte SysGetHash = 0x01;

    /// <summary>Sys / SetRTC (command 0x02).</summary>
    public const byte SysSetRtc = 0x02;

    /// <summary>Sys / FactoryReset (command 0x03).</summary>
    public const byte SysFactoryReset = 0x03;

    /// <summary>Sys / PWLockSetState (command 0x04).</summary>
    public const byte SysPwLockSetState = 0x04;

    /// <summary>Sys / PWLockGetState (command 0x05).</summary>
    public const byte SysPwLockGetState = 0x05;

    /// <summary>Sys / RaceUnlock (command 0x06).</summary>
    public const byte SysRaceUnlock = 0x06;

    // ── Drivers Class Commands (class 0x02) ────────────────────────────

    /// <summary>Drivers / SetDriver (command 0x00).</summary>
    public const byte DriversSetDriver = 0x00;

    /// <summary>Drivers / GetDriver (command 0x01).</summary>
    public const byte DriversGetDriver = 0x01;

    /// <summary>Drivers / StoreInNVM (command 0x02).</summary>
    public const byte DriversStoreInNvm = 0x02;

    // ── Tables Class Commands (class 0x01) ─────────────────────────────

    /// <summary>Tables / SetTable (command 0x00).</summary>
    public const byte TablesSetTable = 0x00;

    /// <summary>Tables / GetTable (command 0x01).</summary>
    public const byte TablesGetTable = 0x01;

    /// <summary>Tables / EnableTable (command 0x02).</summary>
    public const byte TablesEnableTable = 0x02;

    /// <summary>Tables / DisableTable (command 0x03).</summary>
    public const byte TablesDisableTable = 0x03;

    /// <summary>Tables / SetDataAtOffsets (command 0x04).</summary>
    public const byte TablesSetDataAtOffsets = 0x04;

    /// <summary>Tables / GetDataAtOffset (command 0x05).</summary>
    public const byte TablesGetDataAtOffset = 0x05;

    /// <summary>Tables / StoreInNVM (command 0x06).</summary>
    public const byte TablesStoreInNvm = 0x06;

    /// <summary>Tables / SetTableReporting (command 0x07).</summary>
    public const byte TablesSetTableReporting = 0x07;

    // ── Reporting Class Commands (class 0x00) ──────────────────────────

    /// <summary>Reporting / SendReport (command 0x00).</summary>
    public const byte ReportingSendReport = 0x00;

    /// <summary>Reporting / SendAck (command 0x01).</summary>
    public const byte ReportingSendAck = 0x01;

    /// <summary>Reporting / SetState (command 0x02).</summary>
    public const byte ReportingSetState = 0x02;

    /// <summary>Reporting / SetSpecialCfg (command 0x03).</summary>
    public const byte ReportingSetSpecialCfg = 0x03;

    // ── Firmware Update Class Commands (class 0x05) ────────────────────

    /// <summary>FirmwareUpdate / StartFirmwareUpdate (command 0x00).</summary>
    public const byte FirmwareUpdateStartFirmwareUpdate = 0x00;

    /// <summary>FirmwareUpdate / RegionInfoGet (command 0x01).</summary>
    public const byte FirmwareUpdateRegionInfoGet = 0x01;

    /// <summary>FirmwareUpdate / DataGet (command 0x02).</summary>
    public const byte FirmwareUpdateDataGet = 0x02;

    /// <summary>FirmwareUpdate / StatusReport (command 0x03).</summary>
    public const byte FirmwareUpdateStatusReport = 0x03;

    /// <summary>FirmwareUpdate / EnterBLMode (command 0x04).</summary>
    public const byte FirmwareUpdateEnterBlMode = 0x04;

    // ── Data Log Class Commands (class 0x06) ───────────────────────────

    /// <summary>DataLog / IsSupported (command 0x00).</summary>
    public const byte DataLogIsSupported = 0x00;

    /// <summary>DataLog / GetConfig (command 0x01).</summary>
    public const byte DataLogGetConfig = 0x01;

    /// <summary>DataLog / SetConfig (command 0x02).</summary>
    public const byte DataLogSetConfig = 0x02;

    /// <summary>DataLog / Start (command 0x03).</summary>
    public const byte DataLogStart = 0x03;

    /// <summary>DataLog / Stop (command 0x04).</summary>
    public const byte DataLogStop = 0x04;

    /// <summary>DataLog / GetLogsSummary (command 0x05).</summary>
    public const byte DataLogGetLogsSummary = 0x05;

    /// <summary>DataLog / GetLogDetail (command 0x06).</summary>
    public const byte DataLogGetLogDetail = 0x06;

    /// <summary>DataLog / GetLogRegion (command 0x07).</summary>
    public const byte DataLogGetLogRegion = 0x07;

    /// <summary>DataLog / EraseLog (command 0x08).</summary>
    public const byte DataLogEraseLog = 0x08;

    /// <summary>DataLog / FormatMemory (command 0x09).</summary>
    public const byte DataLogFormatMemory = 0x09;

    // ── DBW Class Commands (class 0x08) ────────────────────────────────

    /// <summary>DBW / SetDBWDuty (command 0x00).</summary>
    public const byte DbwSetDbwDuty = 0x00;

    // ── Trigger Logger Class Commands (class 0x07) ─────────────────────

    /// <summary>TriggerLogger / IsSupported (command 0x00).</summary>
    public const byte TriggerLoggerIsSupported = 0x00;

    /// <summary>TriggerLogger / SetState (command 0x01).</summary>
    public const byte TriggerLoggerSetState = 0x01;

    /// <summary>TriggerLogger / Report (command 0x02).</summary>
    public const byte TriggerLoggerReport = 0x02;

    // ── CRC ────────────────────────────────────────────────────────────

    /// <summary>Length of the CRC trailer in bytes (2, little-endian).</summary>
    public const int CrcLength = 2;

    /// <summary>Initial CRC accumulator value.</summary>
    public const ushort CrcInitial = 0x0000;
}
