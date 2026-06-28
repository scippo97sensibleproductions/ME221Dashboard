using System.Buffers.Binary;
using ME221.Comms.Internal;

namespace ME221.Comms.Messages;

/// <summary>
/// Request to get configuration data.
/// Command: DataLog / GetConfig (class 0x06, command 0x01).
/// </summary>
public sealed class GetConfigRequest()
    : Request(WireFormat.ClassDataLog, WireFormat.DataLogGetConfig, Array.Empty<byte>());

/// <summary>
/// Response to a GetConfig request.
/// Payload layout (after status byte):
///   [2 byte freq LE] [2 byte numChans LE] [1 byte switchActiveState]
///   [{2 byte chanId LE} × numChans]
/// </summary>
public sealed class GetConfigResponse : Response
{
    public ushort Frequency { get; }
    public ushort NumChannels { get; }
    public bool SwitchActiveState { get; }
    public List<ushort> ChannelIds { get; }

    public GetConfigResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassDataLog, WireFormat.DataLogGetConfig,
            payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure,
            payload.ToArray().AsMemory())
    {
        if (payload.Length < 2)
        {
            Frequency = 0;
            NumChannels = 0;
            SwitchActiveState = false;
            ChannelIds = [];
            return;
        }

        Frequency = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
        NumChannels = payload.Length > 4
            ? BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(3))
            : (ushort)0;
        SwitchActiveState = payload.Length > 5 && payload[5] != 0;

        ChannelIds = new List<ushort>(NumChannels);
        for (var i = 0; i < NumChannels; i++)
        {
            var offset = 6 + i * 2;
            if (offset + 2 <= payload.Length)
                ChannelIds.Add(BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset)));
        }
    }

    public GetConfigResponse(MessageStatus status) : this(new[] { (byte)status }) { }
}

/// <summary>
/// Request to set configuration data.
/// Command: DataLog / SetConfig (class 0x06, command 0x02).
/// </summary>
public sealed class SetConfigRequest(byte[] payload)
    : Request(WireFormat.ClassDataLog, WireFormat.DataLogSetConfig, payload);

/// <summary>
/// Response to a SetConfig request.
/// </summary>
public sealed class SetConfigResponse(MessageStatus status)
    : Response(WireFormat.ClassDataLog, WireFormat.DataLogSetConfig, status);
