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

        // ── Step 1: Enumerate all USB devices from the system ──
        var allDevices = _usbManager.DeviceList;
        _logger?.LogInformation("CreateUsbSerial: usbManager.DeviceList has {Count} device(s)", allDevices?.Count ?? 0);
        if (allDevices != null)
        {
            foreach (var entry in allDevices)
            {
                var d = entry.Value;
                _logger?.LogInformation("  Device: DeviceName={DeviceName}, VID={VendorId:X4}, PID={ProductId:X4}, HasPermission={HasPermission}",
                    d?.DeviceName, d?.VendorId ?? 0, d?.ProductId ?? 0, d != null && _usbManager.HasPermission(d));
            }
        }

        // ── Step 2: Try the prober (recognizes CP210x, CH340, FTDI, PL2303, etc.) ──
        var prober = Anotherlab.UsbSerialForAndroid.Driver.UsbSerialProber.GetDefaultProber();
        var drivers = prober.FindAllDrivers(_usbManager);
        _logger?.LogInformation("CreateUsbSerial: prober.FindAllDrivers returned {Count} driver(s)", drivers?.Count ?? 0);

        if (drivers != null)
        {
            foreach (var driver in drivers)
            {
                var device = driver.Device;
                if (device == null) continue;
                _logger?.LogInformation("  Prober driver: DeviceName={DeviceName}, VID={VendorId:X4}, PID={ProductId:X4}, Ports={PortCount}, DriverType={DriverType}",
                    device.DeviceName, device.VendorId, device.ProductId, driver.Ports.Count, driver.GetType().Name);

                if (device.DeviceName == s.PortName ||
                    $"{device.VendorId:X4}:{device.ProductId:X4}" == s.PortName)
                {
                    _logger?.LogInformation("CreateUsbSerial: matched prober driver for '{PortName}'", s.PortName);
                    if (driver.Ports.Count > 0)
                    {
                        var port = driver.Ports[0];
                        _logger?.LogInformation("CreateUsbSerial: creating UsbSerialChannel with prober port");
                        return new UsbSerialChannel(
                            _usbManager,
                            port,
                            new ChannelOptions { PortName = s.PortName, BaudRate = s.BaudRate },
                            loggerFactory?.CreateLogger<UsbSerialChannel>());
                    }
                    _logger?.LogWarning("CreateUsbSerial: matched driver but driver.Ports.Count is 0 — falling through to DeviceList fallback");
                }
            }
        }

        // ── Step 3: Fallback — device not recognized by prober, try usbManager.DeviceList directly ──
        _logger?.LogInformation("CreateUsbSerial: prober did not match '{PortName}', trying usbManager.DeviceList fallback", s.PortName);
        if (allDevices != null)
        {
            foreach (var entry in allDevices)
            {
                var device = entry.Value;
                if (device == null) continue;

                if (device.DeviceName == s.PortName ||
                    $"{device.VendorId:X4}:{device.ProductId:X4}" == s.PortName)
                {
                    _logger?.LogInformation("CreateUsbSerial: found '{PortName}' in DeviceList (VID={VendorId:X4}, PID={ProductId:X4})",
                        s.PortName, device.VendorId, device.ProductId);

                    if (!_usbManager.HasPermission(device))
                    {
                        _logger?.LogWarning("CreateUsbSerial: no USB permission for '{PortName}' — should have been granted in ConnectSerial", s.PortName);
                    }

                    // Find a compatible serial interface on the device
                    var serialPort = FindSerialInterface(device);
                    if (serialPort != null)
                    {
                        _logger?.LogInformation("CreateUsbSerial: created UsbSerialChannel from DeviceList fallback");
                        return new UsbSerialChannel(
                            _usbManager,
                            serialPort,
                            new ChannelOptions { PortName = s.PortName, BaudRate = s.BaudRate },
                            loggerFactory?.CreateLogger<UsbSerialChannel>());
                    }
                    _logger?.LogWarning("CreateUsbSerial: no compatible serial interface found on device '{PortName}'", s.PortName);
                }
            }
        }

        _logger?.LogError("CreateUsbSerial: device '{PortName}' not found in prober or DeviceList", s.PortName);
        throw new ArgumentException($"USB serial device '{s.PortName}' not found or not supported. Check logs for enumerated devices.");
    }

    /// <summary>
    /// Try to find a serial-compatible USB interface on an unrecognized device.
    /// Looks for CDC-ACM (class 0x02) or vendor-specific (class 0xFF) interfaces.
    /// </summary>
    private Anotherlab.UsbSerialForAndroid.Driver.UsbSerialPort? FindSerialInterface(global::Android.Hardware.Usb.UsbDevice device)
    {
        _logger?.LogInformation("FindSerialInterface: scanning {InterfaceCount} interface(s) on {DeviceName}",
            device.InterfaceCount, device.DeviceName);

        for (int i = 0; i < device.InterfaceCount; i++)
        {
            var iface = device.GetInterface(i);
            if (iface == null) continue;

            _logger?.LogInformation("  Interface [{Index}]: Id={Id}, Class={InterfaceClass}, Subclass={Subclass}, Protocol={Protocol}, EndpointCount={EndpointCount}",
                i, iface.Id, iface.InterfaceClass, iface.InterfaceSubclass, iface.InterfaceProtocol, iface.EndpointCount);

            // CDC-ACM data interface (class 0x0A) or vendor-specific (0xFF)
            var ifaceClass = (byte)iface.InterfaceClass;
            if (ifaceClass == 0x0A || ifaceClass == 0xFF)
            {
                _logger?.LogInformation("FindSerialInterface: found compatible interface {Index} (class 0x{Class:X2})", i, ifaceClass);

                // Try to create a generic UsbSerialPort using the driver factory
                try
                {
                    // Force enableAsyncReads=false: the async path uses
                    // RequestWait() which blocks indefinitely with no timeout,
                    // starving writes and causing connection loss under load.
                    // BulkTransfer respects the timeout parameter and returns
                    // promptly, matching MEITE's SerialPort.Read(350ms) behavior.
                    var genericDriver = new Anotherlab.UsbSerialForAndroid.Driver.CdcAcmSerialDriver(device, enableAsyncReads: false);
                    if (genericDriver.Ports.Count > 0)
                    {
                        _logger?.LogInformation("FindSerialInterface: created CdcAcmSerialDriver with {Count} port(s)", genericDriver.Ports.Count);
                        return genericDriver.Ports[0];
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "FindSerialInterface: CdcAcmSerialDriver failed, trying CH34x");
                }

                try
                {
                    var ch34xDriver = new Anotherlab.UsbSerialForAndroid.Driver.Ch34xSerialDriver(device);
                    if (ch34xDriver.Ports.Count > 0)
                    {
                        _logger?.LogInformation("FindSerialInterface: created Ch34xSerialDriver with {Count} port(s)", ch34xDriver.Ports.Count);
                        return ch34xDriver.Ports[0];
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "FindSerialInterface: Ch34xSerialDriver failed, trying Cp21xx");
                }

                try
                {
                    var cp21xxDriver = new Anotherlab.UsbSerialForAndroid.Driver.Cp21xxSerialDriver(device);
                    if (cp21xxDriver.Ports.Count > 0)
                    {
                        _logger?.LogInformation("FindSerialInterface: created Cp21xxSerialDriver with {Count} port(s)", cp21xxDriver.Ports.Count);
                        return cp21xxDriver.Ports[0];
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "FindSerialInterface: Cp21xxSerialDriver failed");
                }
            }
        }

        _logger?.LogWarning("FindSerialInterface: no compatible serial interface found on {DeviceName}", device.DeviceName);
        return null;
    }
#endif
}
