using ME221.Comms.Channels;
using ME221Dashboard.Comms;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public sealed class ChannelFactory(ILoggerFactory? loggerFactory = null) : IChannelFactory
{
    private readonly ILogger<ChannelFactory>? _logger = loggerFactory?.CreateLogger<ChannelFactory>();

#if ANDROID
    private global::Android.Hardware.Usb.UsbManager? _usbManager;

    public void SetUsbManager(global::Android.Hardware.Usb.UsbManager usbManager)
    {
        _usbManager = usbManager;
    }
#endif

    public IChannel Create(ConnectionTarget target)
    {
        return target switch
        {
            ConnectionTarget.Tcp t => new TcpChannel(t.Host, t.Port, loggerFactory?.CreateLogger<TcpChannel>()),
#if ANDROID
            ConnectionTarget.Serial s => CreateUsbSerial(s),
#else
            ConnectionTarget.Serial s => new SerialPortChannel(
                new ChannelOptions { PortName = s.PortName, BaudRate = s.BaudRate },
                loggerFactory?.CreateLogger<SerialPortChannel>()),
#endif
            _ => throw new ArgumentException($"Unsupported connection target: {target.GetType().Name}")
        };
    }

#if ANDROID
    private IChannel CreateUsbSerial(ConnectionTarget.Serial s)
    {
        _logger?.LogInformation("CreateUsbSerial: looking for device '{PortName}' at {BaudRate} baud", s.PortName, s.BaudRate);

        if (_usbManager == null)
        {
            _logger?.LogError("CreateUsbSerial: _usbManager is null — SetUsbManager() was never called");
            throw new InvalidOperationException("USB Manager not available. Call SetUsbManager() first.");
        }

        // ── Enumerate all USB devices ──
        var allDevices = _usbManager.DeviceList;
        _logger?.LogInformation("CreateUsbSerial: usbManager.DeviceList has {Count} device(s)", allDevices?.Count ?? 0);

        global::Android.Hardware.Usb.UsbDevice? targetDevice = null;

        if (allDevices != null)
        {
            foreach (var entry in allDevices)
            {
                var d = entry.Value;
                _logger?.LogInformation("  Device: DeviceName={DeviceName}, VID={VendorId:X4}, PID={ProductId:X4}, HasPermission={HasPermission}",
                    d?.DeviceName, d?.VendorId ?? 0, d?.ProductId ?? 0, d != null && _usbManager.HasPermission(d));

                if (d == null) continue;

                if (d.DeviceName == s.PortName ||
                    $"{d.VendorId:X4}:{d.ProductId:X4}" == s.PortName)
                {
                    targetDevice = d;
                    break;
                }
            }
        }

        if (targetDevice == null)
        {
            _logger?.LogError("CreateUsbSerial: device '{PortName}' not found", s.PortName);
            throw new ArgumentException($"USB serial device '{s.PortName}' not found.");
        }

        // ── Try the default prober first (recognizes CP210x, CH340, FTDI, etc.) ──
        var prober = Anotherlab.UsbSerialForAndroid.Driver.UsbSerialProber.GetDefaultProber();
        var drivers = prober.FindAllDrivers(_usbManager);
        if (drivers != null)
        {
            foreach (var driver in drivers)
            {
                if (driver.Device?.DeviceName == targetDevice.DeviceName && driver.Ports.Count > 0)
                {
                    _logger?.LogInformation("CreateUsbSerial: matched prober driver {DriverType}", driver.GetType().Name);
                    return new UsbSerialChannel(
                        _usbManager,
                        driver.Ports[0],
                        new ChannelOptions { PortName = s.PortName, BaudRate = s.BaudRate },
                        loggerFactory?.CreateLogger<UsbSerialChannel>());
                }
            }
        }

        // ── Fallback: Create CdcAcmSerialDriver directly for CDC-ACM devices ──
        // This handles the ME221 ECU (VID=1FC9, PID=823D) and other CDC-ACM
        // devices that aren't in the prober's lookup table.
        _logger?.LogInformation("CreateUsbSerial: prober did not match, trying CdcAcmSerialDriver fallback");

        Anotherlab.UsbSerialForAndroid.Driver.UsbSerialPort? serialPort = FindCdcAcmPort(targetDevice);
        if (serialPort != null)
        {
            _logger?.LogInformation("CreateUsbSerial: created UsbSerialChannel from CDC-ACM fallback");
            return new UsbSerialChannel(
                _usbManager,
                serialPort,
                new ChannelOptions { PortName = s.PortName, BaudRate = s.BaudRate },
                loggerFactory?.CreateLogger<UsbSerialChannel>());
        }

        _logger?.LogError("CreateUsbSerial: no compatible serial interface found on device '{PortName}'", s.PortName);
        throw new ArgumentException($"USB serial device '{s.PortName}' is not supported. No CDC-ACM interface found.");
    }

    /// <summary>
    /// Creates a CdcAcmSerialDriver for a device with CDC-ACM interfaces.
    /// Scans for Comm (0x02) + CdcData (0x0A) interface pairs.
    /// </summary>
    private Anotherlab.UsbSerialForAndroid.Driver.UsbSerialPort? FindCdcAcmPort(global::Android.Hardware.Usb.UsbDevice device)
    {
        _logger?.LogInformation("FindCdcAcmPort: scanning {InterfaceCount} interface(s) on {DeviceName}",
            device.InterfaceCount, device.DeviceName);

        bool hasCommInterface = false;
        bool hasDataInterface = false;

        for (int i = 0; i < device.InterfaceCount; i++)
        {
            var iface = device.GetInterface(i);
            if (iface == null) continue;

            var ifaceClass = (byte)iface.InterfaceClass;
            _logger?.LogInformation("  Interface [{Index}]: Id={Id}, Class=0x{Class:X2}, Endpoints={EndpointCount}",
                i, iface.Id, ifaceClass, iface.EndpointCount);

            if (ifaceClass == 0x02) hasCommInterface = true;
            if (ifaceClass == 0x0A) hasDataInterface = true;
        }

        if (hasCommInterface && hasDataInterface)
        {
            _logger?.LogInformation("FindCdcAcmPort: found CDC-ACM (Comm + Data) interfaces, creating CdcAcmSerialDriver");
            try
            {
                // enableAsyncReads=false: use synchronous bulkTransfer which respects
                // timeouts. The async path uses requestWait() which can block indefinitely.
                var driver = new Anotherlab.UsbSerialForAndroid.Driver.CdcAcmSerialDriver(device, enableAsyncReads: false);
                if (driver.Ports.Count > 0)
                {
                    _logger?.LogInformation("FindCdcAcmPort: CdcAcmSerialDriver created with {Count} port(s)", driver.Ports.Count);
                    return driver.Ports[0];
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "FindCdcAcmPort: CdcAcmSerialDriver creation failed");
            }
        }

        _logger?.LogWarning("FindCdcAcmPort: no CDC-ACM interface pair found on {DeviceName}", device.DeviceName);
        return null;
    }
#endif
}
