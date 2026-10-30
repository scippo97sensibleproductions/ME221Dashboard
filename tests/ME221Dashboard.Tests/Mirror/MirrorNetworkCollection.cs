using Xunit;

namespace ME221Dashboard.Tests.Mirror;

/// <summary>
/// Tests that talk to real loopback sockets (the Fleck WSS server, the viewer
/// transport, and the UDP discovery responder) assert inside short flush and
/// timeout windows. Running them next to the rest of the suite starves those
/// windows on a loaded machine, so the whole group runs on its own.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MirrorNetworkCollection
{
    public const string Name = "Mirror network (serial)";
}
