using FluentAssertions;
using ME221.Comms.Channels;
using ME221.Comms.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ME221.Comms.Tests;

public class MockChannelTests
{
    [Fact]
    public void Status_Initial_IsClosed()
    {
        var channel = new MockChannel(NullLogger<MockChannel>.Instance);
        channel.Status.Should().Be(DeviceStatus.Closed);
        channel.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task OpenAsync_SetsStatusToOpened()
    {
        var channel = new MockChannel(NullLogger<MockChannel>.Instance);
        await channel.OpenAsync();
        channel.Status.Should().Be(DeviceStatus.Opened);
        channel.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task CloseAsync_SetsStatusToClosed()
    {
        var channel = new MockChannel(NullLogger<MockChannel>.Instance);
        await channel.OpenAsync();
        await channel.CloseAsync();
        channel.Status.Should().Be(DeviceStatus.Closed);
        channel.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_NoFailure_DoesNotThrow()
    {
        var channel = new MockChannel(NullLogger<MockChannel>.Instance);
        await channel.OpenAsync();
        ReadOnlyMemory<byte> frame = new byte[] { 0x01, 0x02, 0x03 };

        await channel.Awaiting(x => x.SendAsync(frame)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_FailOnSend_ThrowsInvalidOperationException()
    {
        var channel = new MockChannel(NullLogger<MockChannel>.Instance);
        channel.SetFailOnSend(true);
        ReadOnlyMemory<byte> frame = new byte[] { 0x01, 0x02, 0x03 };

        await channel.Awaiting(x => x.SendAsync(frame))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("MockChannel: configured send failure*");
    }

    [Fact]
    public async Task InjectFrame_FrameAppearsInIncomingFrames()
    {
        var channel = new MockChannel(NullLogger<MockChannel>.Instance);
        await channel.OpenAsync();
        var request = new GetEcuInfoRequest();

        channel.InjectFrame(request);
        await channel.CloseAsync();  // complete the async enumerable

        int count = 0;
        await foreach (var frame in channel.IncomingFrames)
        {
            count++;
            frame.Should().BeSameAs(request);
        }
        count.Should().Be(1);
    }

    [Fact]
    public async Task InjectFrame_MultipleFrames_AllReceived()
    {
        var channel = new MockChannel(NullLogger<MockChannel>.Instance);
        await channel.OpenAsync();

        channel.InjectFrame(new GetEcuInfoRequest());
        channel.InjectFrame(new GetDriverRequest(1));
        channel.InjectFrame(new FactoryResetRequest());
        await channel.CloseAsync();  // complete the async enumerable

        int count = 0;
        await foreach (var _ in channel.IncomingFrames)
        {
            count++;
        }
        count.Should().Be(3);
    }

    [Fact]
    public async Task SendAsync_WithDelay_AwaitsDelay()
    {
        var channel = new MockChannel(NullLogger<MockChannel>.Instance);
        channel.SetSendDelay(TimeSpan.FromMilliseconds(50));
        ReadOnlyMemory<byte> frame = new byte[] { 0x01, 0x02 };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await channel.SendAsync(frame);
        sw.Stop();

        sw.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(40));
    }
}
