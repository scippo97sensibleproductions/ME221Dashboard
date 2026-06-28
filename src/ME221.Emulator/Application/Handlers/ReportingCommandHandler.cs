using System.Buffers.Binary;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221.Emulator.Domain;
using ME221.Emulator.Messages;

namespace ME221.Emulator.Application.Handlers;

public sealed class ReportingCommandHandler(EntityStore entityStore, EcuState state) : ICommandHandler
{
    public bool CanHandle(byte classId, byte command)
    {
        return classId == WireFormat.ClassReporting;
    }

    public ValueTask<MessageFrame> HandleAsync(MessageFrame request)
    {
        var response = request.Command switch
        {
            WireFormat.ReportingSetState => HandleSetState(request),
            WireFormat.ReportingSetSpecialCfg => HandleSetSpecialCfg(request),
            WireFormat.ReportingSendReport => HandleSendReport(),
            WireFormat.ReportingSendAck => HandleSendAck(),
            _ => new StatusResponse(request.Class, request.Command, MessageStatus.UnsupportedRequest),
        };

        return ValueTask.FromResult(response);
    }

    private MessageFrame HandleSetState(MessageFrame request)
    {
        var enabled = request.Payload.Length > 0 && request.Payload.Span[0] != 0;
        state.ReportingEnabled = enabled;

        if (enabled)
        {
            return BuildSetStateResponse();
        }

        return new StatusResponse(WireFormat.ClassReporting, WireFormat.ReportingSetState, MessageStatus.Success);
    }

    private MessageFrame BuildSetStateResponse()
    {
        // Build V2 SetStateResponse: [status:1][count:2 LE][entity × N: each 3 bytes (id:2 + type:1)]
        // Include ALL data links (not just those with DataKey) to match real ECU behavior
        var knownLinks = entityStore.Calibration.DataLinks;

        var payloadSize = 1 + 2 + knownLinks.Count * 3;
        var payload = new byte[payloadSize];
        payload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), (ushort)knownLinks.Count);

        var offset = 3;
        foreach (var link in knownLinks)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset), link.Id);
            offset += 2;
            payload[offset++] = entityStore.GetDataLinkReportingType(link.Id);
        }

        return new SetStateResponse(payload);
    }

    private MessageFrame HandleSetSpecialCfg(MessageFrame request)
    {
        var payload = request.Payload;
        if (payload.Length < 1)
        {
            return new StatusResponse(WireFormat.ClassReporting, WireFormat.ReportingSetSpecialCfg, MessageStatus.InvalidParameter);
        }

        var frequency = payload.Span[0];
        state.SpecialCfgFrequency = frequency;
        state.SpecialCfgActive = true;

        if (payload.Length > 1)
        {
            var entityIds = new List<ushort>();
            for (var i = 1; i + 1 < payload.Length; i += 2)
            {
                entityIds.Add(BinaryPrimitives.ReadUInt16LittleEndian(payload.Span[i..]));
            }
            state.SpecialCfgEntityIds = entityIds;
        }

        // Build response: [status:1][freq:1][count:2 LE][entity × N: each 3 bytes]
        var knownLinks = state.SpecialCfgEntityIds.Count > 0
            ? entityStore.Calibration.DataLinks.Where(dl => state.SpecialCfgEntityIds.Contains(dl.Id)).ToList()
            : entityStore.Calibration.DataLinks.ToList();

        var responseSize = 1 + 1 + 2 + knownLinks.Count * 3;
        var responsePayload = new byte[responseSize];
        responsePayload[0] = (byte)MessageStatus.Success;
        responsePayload[1] = frequency;
        BinaryPrimitives.WriteUInt16LittleEndian(responsePayload.AsSpan(2), (ushort)knownLinks.Count);

        var offset = 4;
        foreach (var link in knownLinks)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(responsePayload.AsSpan(offset), link.Id);
            offset += 2;
            responsePayload[offset++] = entityStore.GetDataLinkReportingType(link.Id);
        }

        return new SetSpecialCfgResponse(responsePayload);
    }

    private static MessageFrame HandleSendReport()
    {
        return new SendAckResponse(MessageStatus.Success);
    }

    private static MessageFrame HandleSendAck()
    {
        return new SendAckResponse(MessageStatus.Success);
    }
}
