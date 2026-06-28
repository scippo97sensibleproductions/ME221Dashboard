namespace ME221Dashboard.Comms;

public class ChannelOptions
{
    public string? PortName { get; set; }
    public int BaudRate { get; set; } = 230400;
    public int DataBits { get; set; } = 8;
    public int Parity { get; set; }
    public int StopBits { get; set; } = 1;
    public int SendTimeoutMs { get; set; } = 3000;
    public int ReceiveTimeoutMs { get; set; } = 3000;
    public bool Handshake { get; set; }
}
