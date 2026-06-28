using System.Buffers.Binary;
using System.Text;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221.Emulator.Domain;
using ME221.Emulator.Messages;

namespace ME221.Emulator.Application.Handlers;

public sealed class SysCommandHandler(EntityStore entityStore, EcuState state) : ICommandHandler
{
    private readonly EcuState _state = state;

    public bool CanHandle(byte classId, byte command)
    {
        return classId == WireFormat.ClassSys;
    }

    public ValueTask<MessageFrame> HandleAsync(MessageFrame request)
    {
        var response = request.Command switch
        {
            WireFormat.SysGetEcuInfo => HandleGetEcuInfo(),
            WireFormat.SysGetHash => HandleGetHash(request),
            WireFormat.SysSetRtc => HandleSetRtc(),
            WireFormat.SysFactoryReset => HandleFactoryReset(),
            WireFormat.SysPwLockSetState => HandlePwLockSetState(),
            WireFormat.SysPwLockGetState => HandlePwLockGetState(),
            WireFormat.SysRaceUnlock => HandleRaceUnlock(),
            _ => new StatusResponse(request.Class, request.Command, MessageStatus.UnsupportedRequest),
        };

        return ValueTask.FromResult(response);
    }

    private MessageFrame HandleGetEcuInfo()
    {
        var meta = entityStore.Calibration.Metadata;
        var payload = BuildGetEcuInfoPayload(meta.ProductName, meta.ModelName, meta.Version);
        return new GetEcuInfoResponse(payload);
    }

    private static byte[] BuildGetEcuInfoPayload(string product, string model, string version)
    {
        using var ms = new MemoryStream();
        ms.WriteByte((byte)MessageStatus.Success);
        WriteNullTerminated(ms, product);
        WriteNullTerminated(ms, model);
        WriteNullTerminated(ms, version);
        WriteNullTerminated(ms, version);
        return ms.ToArray();
    }

    private static void WriteNullTerminated(MemoryStream ms, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        ms.Write(bytes);
        ms.WriteByte(0);
    }

    private static MessageFrame HandleGetHash(MessageFrame request)
    {
        var mode = request.Payload.Length > 0 ? request.Payload.Span[0] : (byte)0;

        // Payload: [status][mode][overallHash LE]
        var payload = new byte[4];
        payload[0] = (byte)MessageStatus.Success;
        payload[1] = mode;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), 0x1234);

        return new GetHashResponse(payload);
    }

    private static MessageFrame HandleSetRtc()
    {
        return new SetRtcResponse(MessageStatus.Success);
    }

    private static MessageFrame HandleFactoryReset()
    {
        return new StatusResponse(WireFormat.ClassSys, WireFormat.SysFactoryReset, MessageStatus.UnsupportedRequest);
    }

    private static MessageFrame HandlePwLockSetState()
    {
        return new PwLockSetStateResponse(MessageStatus.Success);
    }

    private static MessageFrame HandlePwLockGetState()
    {
        var payload = new byte[3];
        payload[0] = (byte)MessageStatus.Success;
        payload[1] = 0; // locked = false
        payload[2] = 0; // tunerContact = false
        return new PwLockGetStateResponse(payload);
    }

    private static MessageFrame HandleRaceUnlock()
    {
        return new RaceUnlockResponse(MessageStatus.Success);
    }
}
