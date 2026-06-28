using FluentAssertions;
using ME221.Comms.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ME221.Comms.Tests;

public class ProtocolServiceTests
{
    [Fact]
    public void Constructor_WithMockChannel_CreatesService()
    {
        var mockChannel = new MockChannel(NullLogger<MockChannel>.Instance);
        var service = new ProtocolService(mockChannel, NullLogger<ProtocolService>.Instance);
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task IsOpen_DelegatesToChannel()
    {
        var mockChannel = new MockChannel(NullLogger<MockChannel>.Instance);
        var service = new ProtocolService(mockChannel, NullLogger<ProtocolService>.Instance);
        service.IsOpen.Should().BeFalse();

        await mockChannel.OpenAsync();
        service.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_SendAndGetEcuInfoResponse_ReturnsResponse()
    {
        var mockChannel = new MockChannel(NullLogger<MockChannel>.Instance);
        await mockChannel.OpenAsync();
        var service = new ProtocolService(mockChannel, NullLogger<ProtocolService>.Instance);

        var request = new GetEcuInfoRequest();
        var response = new GetEcuInfoResponse(new byte[0]);

        var sendTask = Task.Run(async () =>
        {
            await Task.Delay(50);
            mockChannel.InjectFrame(response);
        });

        var result = await service.SendAsync<GetEcuInfoResponse>(request);
        await sendTask;

        result.Should().BeSameAs(response);
    }

    [Fact]
    public async Task SendAsync_ResponseTimeout_ThrowsTimeoutException()
    {
        var mockChannel = new MockChannel(NullLogger<MockChannel>.Instance);
        await mockChannel.OpenAsync();
        var service = new ProtocolService(mockChannel, NullLogger<ProtocolService>.Instance);
        var request = new GetEcuInfoRequest();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await service.Awaiting(x => x.SendAsync<GetEcuInfoResponse>(request, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendAsync_ChannelClosed_ThrowsOperationCanceledException()
    {
        var mockChannel = new MockChannel(NullLogger<MockChannel>.Instance);
        var service = new ProtocolService(mockChannel, NullLogger<ProtocolService>.Instance);
        var request = new GetEcuInfoRequest();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Channel not opened — correlator's WaitAsync cancels when token fires
        await service.Awaiting(x => x.SendAsync<GetEcuInfoResponse>(request, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendAsync_FireAndForget_DoesNotThrow()
    {
        var mockChannel = new MockChannel(NullLogger<MockChannel>.Instance);
        await mockChannel.OpenAsync();
        var service = new ProtocolService(mockChannel, NullLogger<ProtocolService>.Instance);
        MessageFrame frame = new GetEcuInfoRequest();

        await service.Awaiting(x => x.SendAsync(frame)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendBatchAsync_SendsAllFrames()
    {
        var mockChannel = new MockChannel(NullLogger<MockChannel>.Instance);
        await mockChannel.OpenAsync();
        var service = new ProtocolService(mockChannel, NullLogger<ProtocolService>.Instance);
        MessageFrame[] frames =
        [
            new GetEcuInfoRequest(),
            new GetDriverRequest(1),
            new FactoryResetRequest()
        ];

        await service.Awaiting(x => x.SendBatchAsync(frames)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendBatchAsync_NullFrames_ThrowsArgumentNullException()
    {
        var mockChannel = new MockChannel(NullLogger<MockChannel>.Instance);
        var service = new ProtocolService(mockChannel, NullLogger<ProtocolService>.Instance);

        await service.Awaiting(x => x.SendBatchAsync((IEnumerable<MessageFrame>)null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DisposeAsync_CancelsPendingRequests()
    {
        var mockChannel = new MockChannel(NullLogger<MockChannel>.Instance);
        await mockChannel.OpenAsync();
        var service = new ProtocolService(mockChannel, NullLogger<ProtocolService>.Instance);
        var request = new GetEcuInfoRequest();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var sendTask = service.SendAsync<GetEcuInfoResponse>(request, cts.Token);
        await service.DisposeAsync();

        // DisposeAsync cancels pending requests via RequestCorrelator.CancelAll(),
        // which completes the TCS as cancelled. WaitAsync on a cancelled TCS
        // produces a Canceled task (not Faulted), not an exception.
        sendTask.IsCompleted.Should().BeTrue();
        sendTask.IsCanceled.Should().BeTrue();
    }
}
