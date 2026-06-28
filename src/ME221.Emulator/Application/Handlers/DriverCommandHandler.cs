using System.Buffers.Binary;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221.Data.Infrastructure;
using ME221.Emulator.Domain;
using ME221.Emulator.Messages;

namespace ME221.Emulator.Application.Handlers;

public sealed class DriverCommandHandler(EntityStore entityStore, EcuState state) : ICommandHandler
{
    private readonly EcuState _state = state;

    public bool CanHandle(byte classId, byte command)
    {
        return classId == WireFormat.ClassDrivers;
    }

    public ValueTask<MessageFrame> HandleAsync(MessageFrame request)
    {
        var response = request.Command switch
        {
            WireFormat.DriversSetDriver => HandleSetDriver(request),
            WireFormat.DriversGetDriver => HandleGetDriver(request),
            WireFormat.DriversStoreInNvm => HandleStoreInNvm(request),
            _ => new StatusResponse(request.Class, request.Command, MessageStatus.UnsupportedRequest),
        };

        return ValueTask.FromResult(response);
    }

    private MessageFrame HandleSetDriver(MessageFrame request)
    {
        var payload = request.Payload;
        if (payload.Length < 2)
            return new StatusResponse(WireFormat.ClassDrivers, WireFormat.DriversSetDriver, MessageStatus.InvalidParameter);

        var driverId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        if (!entityStore.Calibration.Drivers.Any(d => d.Id == driverId))
            return new StatusResponse(WireFormat.ClassDrivers, WireFormat.DriversSetDriver, MessageStatus.Failure);

        if (payload.Length < 3)
            return new StatusResponse(WireFormat.ClassDrivers, WireFormat.DriversSetDriver, MessageStatus.InvalidParameter);

        DriverWireData wireData;
        try
        {
            wireData = DriverSerializer.Deserialize(payload.Span[2..]);
        }
        catch (ArgumentException)
        {
            return new StatusResponse(WireFormat.ClassDrivers, WireFormat.DriversSetDriver, MessageStatus.InvalidParameter);
        }

        var driverDef = entityStore.Calibration.Drivers.First(d => d.Id == driverId);
        if (wireData.Configs.Length != driverDef.NumberOfConfigs)
            return new StatusResponse(WireFormat.ClassDrivers, WireFormat.DriversSetDriver, MessageStatus.InvalidParameter);

        entityStore.SetDriverConfigs(driverId, wireData.Configs);
        driverDef.OutputLinkIds = wireData.OutputIds;
        driverDef.InputLinkIds = wireData.InputIds;

        var responsePayload = new byte[3];
        responsePayload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteUInt16LittleEndian(responsePayload.AsSpan(1), driverId);
        return new SetDriverResponse(responsePayload);
    }

    private MessageFrame HandleGetDriver(MessageFrame request)
    {
        var payload = request.Payload;
        if (payload.Length < 2)
            return new StatusResponse(WireFormat.ClassDrivers, WireFormat.DriversGetDriver, MessageStatus.InvalidParameter);

        var driverId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        if (!entityStore.Calibration.Drivers.Any(d => d.Id == driverId))
            return new StatusResponse(WireFormat.ClassDrivers, WireFormat.DriversGetDriver, MessageStatus.Failure);

        var driverDef = entityStore.Calibration.Drivers.First(d => d.Id == driverId);

        entityStore.TryGetDriverConfigs(driverId, out var configs);
        configs ??= new float[driverDef.NumberOfConfigs];

        var numOutputs = driverDef.NumberOfOutputs;
        var numInputs = driverDef.NumberOfInputs;

        var outputIds = driverDef.OutputLinkIds;
        var inputIds = driverDef.InputLinkIds;

        var serialized = DriverSerializer.Serialize(configs, outputIds, inputIds);
        var responsePayload = new byte[1 + 2 + serialized.Length];
        responsePayload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteUInt16LittleEndian(responsePayload.AsSpan(1), driverId);
        serialized.CopyTo(responsePayload.AsSpan(3));

        return new GetDriverResponse(responsePayload);
    }

    private static MessageFrame HandleStoreInNvm(MessageFrame request)
    {
        return new StoreDriverResponse([(byte)MessageStatus.Success]);
    }
}
