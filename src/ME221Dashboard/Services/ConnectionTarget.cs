namespace ME221Dashboard.Services;

public abstract record ConnectionTarget
{
    private ConnectionTarget() { }

    internal sealed record Tcp(string Host, int Port) : ConnectionTarget;

    internal sealed record Serial(string PortName, int BaudRate, int VendorId = 0, int ProductId = 0) : ConnectionTarget;
}
