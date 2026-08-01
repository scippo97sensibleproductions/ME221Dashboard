using FluentAssertions;
using ME221Dashboard.Comms;
using Xunit;

namespace ME221Dashboard.Comms.Tests;

public class ChannelOptionsTests
{
    [Fact]
    public void Defaults_Are_Sane()
    {
        var options = new ChannelOptions();
        options.BaudRate.Should().Be(230400);
        options.DataBits.Should().Be(8);
        options.Parity.Should().Be(0);
        options.StopBits.Should().Be(1);
        options.SendTimeoutMs.Should().Be(3000);
        options.ReceiveTimeoutMs.Should().Be(3000);
        options.Handshake.Should().BeFalse();
        options.PortName.Should().BeNull();
    }

    [Fact]
    public void Properties_Are_Settable()
    {
        var options = new ChannelOptions
        {
            PortName = "COM4",
            BaudRate = 115200,
            DataBits = 7,
            Parity = 2,
            StopBits = 2,
            SendTimeoutMs = 1000,
            ReceiveTimeoutMs = 500,
            Handshake = true,
        };
        options.PortName.Should().Be("COM4");
        options.BaudRate.Should().Be(115200);
        options.DataBits.Should().Be(7);
        options.Parity.Should().Be(2);
        options.StopBits.Should().Be(2);
        options.SendTimeoutMs.Should().Be(1000);
        options.ReceiveTimeoutMs.Should().Be(500);
        options.Handshake.Should().BeTrue();
    }
}

public class SerialPortChannelTests
{
    [Fact]
    public void Constructor_Throws_On_Null_Options()
    {
        var act = () => new SerialPortChannel(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_Throws_When_PortName_Missing()
    {
        var act = () => new SerialPortChannel(new ChannelOptions());
        act.Should().Throw<ArgumentException>().WithMessage("*PortName*");
    }

    [Fact]
    public void New_Channel_Is_Closed()
    {
        var channel = new SerialPortChannel(new ChannelOptions { PortName = "COM3" });
        channel.IsOpen.Should().BeFalse();
        channel.Status.Should().Be(ME221.Comms.Channels.DeviceStatus.Closed);
    }

    [Fact]
    public async Task SendAsync_Throws_When_Port_Not_Open()
    {
        var channel = new SerialPortChannel(new ChannelOptions { PortName = "COM3" });
        var act = async () => await channel.SendAsync(new byte[] { 1, 2, 3 });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not open*");
    }

    [Fact]
    public async Task Dispose_While_Closed_Is_Idempotent()
    {
        var channel = new SerialPortChannel(new ChannelOptions { PortName = "COM3" });
        await channel.DisposeAsync();
        await channel.DisposeAsync();
        channel.Status.Should().Be(ME221.Comms.Channels.DeviceStatus.Closed);
    }

    [Fact]
    public async Task IncomingFrames_Completes_After_Close()
    {
        var channel = new SerialPortChannel(new ChannelOptions { PortName = "COM3" });
        await channel.CloseAsync();
        channel.Status.Should().Be(ME221.Comms.Channels.DeviceStatus.Closed);
    }
}
