using ME221.Comms.Channels;

namespace ME221Dashboard.Services;

public interface IChannelFactory
{
    IChannel Create(ConnectionTarget target);
}
