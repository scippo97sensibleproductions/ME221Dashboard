using FluentAssertions;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ME221.Comms.Tests;

public class CorrelationTests
{
    [Fact]
    public void Register_GivenRequest_CreatesTcs()
    {
        // Arrange
        var correlator = new RequestCorrelator(NullLogger<RequestCorrelator>.Instance);
        var request = new GetEcuInfoRequest();

        // Act
        var tcs = correlator.Register<GetEcuInfoResponse>(request);

        // Assert
        tcs.Task.Should().NotBeNull();
        tcs.Task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task TryCorrelate_MatchingResponse_CompletesTcs()
    {
        // Arrange
        var correlator = new RequestCorrelator(NullLogger<RequestCorrelator>.Instance);
        var request = new GetEcuInfoRequest();
        var tcs = correlator.Register<GetEcuInfoResponse>(request);
        var response = new GetEcuInfoResponse(new byte[] { (byte)MessageStatus.Success });

        // Act
        var result = correlator.TryCorrelate(response);

        // Assert
        result.Should().BeTrue();
        var taskResult = await tcs.Task;  // await satisfies xUnit1031; task already completed
        taskResult.Should().BeSameAs(response);
    }

    [Fact]
    public void TryCorrelate_MismatchedResponse_ReturnsFalse()
    {
        // Arrange
        var correlator = new RequestCorrelator(NullLogger<RequestCorrelator>.Instance);
        var request = new GetEcuInfoRequest();
        correlator.Register<GetEcuInfoResponse>(request);
        var response = new SetDriverResponse(new byte[] { (byte)MessageStatus.Success, 0x01, 0x00 });

        // Act
        var result = correlator.TryCorrelate(response);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryCorrelate_NoPendingRequest_ReturnsFalse()
    {
        // Arrange
        var correlator = new RequestCorrelator(NullLogger<RequestCorrelator>.Instance);
        var response = new GetEcuInfoResponse(new byte[] { (byte)MessageStatus.Success });

        // Act
        var result = correlator.TryCorrelate(response);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Register_MultipleRequests_CorrelatesEachCorrectly()
    {
        // Arrange
        var correlator = new RequestCorrelator(NullLogger<RequestCorrelator>.Instance);
        var tcs1 = correlator.Register<GetEcuInfoResponse>(new GetEcuInfoRequest());
        var tcs2 = correlator.Register<GetDriverResponse>(new GetDriverRequest(1));
        var response1 = new GetEcuInfoResponse(new byte[] { (byte)MessageStatus.Success });
        var response2 = new GetDriverResponse(new byte[] { (byte)MessageStatus.Success, 0x01, 0x00 });

        // Act
        var result1 = correlator.TryCorrelate(response1);
        var result2 = correlator.TryCorrelate(response2);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        var r1 = await tcs1.Task;  // await satisfies xUnit1031
        var r2 = await tcs2.Task;  // await satisfies xUnit1031
        r1.Should().NotBeNull();
        r2.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_DuplicateKey_OverwritesPreviousTcs()
    {
        // Arrange
        var correlator = new RequestCorrelator(NullLogger<RequestCorrelator>.Instance);
        var tcs1 = correlator.Register<GetEcuInfoResponse>(new GetEcuInfoRequest());
        var tcs2 = correlator.Register<GetEcuInfoResponse>(new GetEcuInfoRequest());
        var response = new GetEcuInfoResponse(new byte[] { (byte)MessageStatus.Success });

        // Act
        var result = correlator.TryCorrelate(response);

        // Assert
        result.Should().BeTrue();
        var r2 = await tcs2.Task;
        r2.Should().NotBeNull();

        // Old TCS should be canceled (not leaked) by the AddOrUpdate guard
        tcs1.Task.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public void CancelAll_CancelsAllPendingTcs()
    {
        // Arrange
        var correlator = new RequestCorrelator(NullLogger<RequestCorrelator>.Instance);
        var tcs1 = correlator.Register<GetEcuInfoResponse>(new GetEcuInfoRequest());
        var tcs2 = correlator.Register<GetDriverResponse>(new GetDriverRequest(1));

        // Act
        correlator.CancelAll();

        // Assert
        tcs1.Task.IsCanceled.Should().BeTrue();
        tcs2.Task.IsCanceled.Should().BeTrue();
    }
}
