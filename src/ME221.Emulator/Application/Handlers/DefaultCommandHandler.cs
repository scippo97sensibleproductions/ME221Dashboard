using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221.Emulator.Messages;

namespace ME221.Emulator.Application.Handlers;

public sealed class DefaultCommandHandler : ICommandHandler
{
    public bool CanHandle(byte classId, byte command)
    {
        return true; // catch-all: handles anything no other handler claimed
    }

    public ValueTask<MessageFrame> HandleAsync(MessageFrame request)
    {
        var response = request.Class switch
        {
            WireFormat.ClassDataLinks => HandleDataLinks(request),
            WireFormat.ClassDataLog => HandleDataLog(request),
            WireFormat.ClassTriggerLogger => HandleTriggerLogger(request),
            WireFormat.ClassDbw => HandleDbw(request),
            WireFormat.ClassFirmwareUpdate => HandleFirmwareUpdate(request),
            _ => new StatusResponse(request.Class, request.Command, MessageStatus.Failure),
        };

        return ValueTask.FromResult(response);
    }

    private static MessageFrame HandleDataLinks(MessageFrame request)
    {
        return new StatusResponse(request.Class, request.Command, MessageStatus.Failure);
    }

    private static MessageFrame HandleDataLog(MessageFrame request)
    {
        return request.Command switch
        {
            WireFormat.DataLogIsSupported => new IsSupportedResponse(MessageStatus.Success),
            WireFormat.DataLogGetConfig => new GetLogsSummaryResponse(MessageStatus.Success),
            WireFormat.DataLogSetConfig => new GetLogsSummaryResponse(MessageStatus.Success),
            WireFormat.DataLogStart => new StartResponse(MessageStatus.Success),
            WireFormat.DataLogStop => new StopResponse(MessageStatus.Success),
            WireFormat.DataLogGetLogsSummary => new GetLogsSummaryResponse(MessageStatus.Success),
            WireFormat.DataLogGetLogDetail => new GetLogDetailResponse(MessageStatus.Success),
            WireFormat.DataLogGetLogRegion => new GetLogRegionResponse(MessageStatus.Success),
            WireFormat.DataLogEraseLog => new EraseLogResponse(MessageStatus.Success),
            WireFormat.DataLogFormatMemory => new FormatMemoryResponse(MessageStatus.Success),
            _ => new StatusResponse(request.Class, request.Command, MessageStatus.UnsupportedRequest),
        };
    }

    private static MessageFrame HandleTriggerLogger(MessageFrame request)
    {
        return request.Command switch
        {
            WireFormat.TriggerLoggerIsSupported => new TriggerLoggerIsSupportedResponse(MessageStatus.Success),
            WireFormat.TriggerLoggerSetState => new TriggerLoggerSetStateResponse(MessageStatus.Success),
            WireFormat.TriggerLoggerReport => new ReportResponse([]),
            _ => new StatusResponse(request.Class, request.Command, MessageStatus.UnsupportedRequest),
        };
    }

    private static MessageFrame HandleDbw(MessageFrame request)
    {
        return request.Command switch
        {
            WireFormat.DbwSetDbwDuty => new SetDbwDutyResponse(MessageStatus.Success),
            _ => new StatusResponse(request.Class, request.Command, MessageStatus.UnsupportedRequest),
        };
    }

    private static MessageFrame HandleFirmwareUpdate(MessageFrame request)
    {
        return request.Command switch
        {
            WireFormat.FirmwareUpdateStartFirmwareUpdate => new StartFwUpdateResponse(MessageStatus.UnsupportedRequest),
            WireFormat.FirmwareUpdateRegionInfoGet => new RegionInfoGetResponse(MessageStatus.UnsupportedRequest),
            WireFormat.FirmwareUpdateDataGet => new RegionDataGetResponse(MessageStatus.UnsupportedRequest),
            WireFormat.FirmwareUpdateStatusReport => new StatusReportResponse(MessageStatus.UnsupportedRequest),
            WireFormat.FirmwareUpdateEnterBlMode => new EnterBlModeResponse(MessageStatus.UnsupportedRequest),
            _ => new StatusResponse(request.Class, request.Command, MessageStatus.UnsupportedRequest),
        };
    }
}
