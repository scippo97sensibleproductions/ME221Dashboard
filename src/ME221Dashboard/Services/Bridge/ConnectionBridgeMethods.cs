using System.IO.Ports;
using System.Text.Json;
using ME221.Comms;
using ME221.Comms.Messages;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    private ConnectionTarget? _lastTarget;
    /// <summary>
    /// Connect to an ECU via TCP. Called from JS: window.HybridWebView.InvokeDotNet('ConnectTcp', [host, port])
    /// </summary>
    public async Task<string> ConnectTcp(string host, int port)
    {
        try
        {
            _logger.LogInformation("Connecting to TCP {Host}:{Port}", host, port);
            var target = new ConnectionTarget.Tcp(host, port);
            _lastTarget = target;
            var success = await _connection.ConnectAsync(target).ConfigureAwait(false);
            if (!success)
            {
                var error = _connection.LastError ?? "TCP connection failed.";
                return JsonSerializer.Serialize(new { success = false, error, state = _connection.State.ToString() });
            }
            return JsonSerializer.Serialize(new { success, state = _connection.State.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCP connection failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Connect to an ECU via Serial/USB. Called from JS: window.HybridWebView.InvokeDotNet('ConnectSerial', [portName, baudRate])
    /// </summary>
    public async Task<string> ConnectSerial(string portName, int baudRate)
    {
        try
        {
            _logger.LogInformation("ConnectSerial: connecting to '{PortName}' at {BaudRate} baud", portName, baudRate);

#if ANDROID
            var context = global::Android.App.Application.Context;
            var usbManager = (global::Android.Hardware.Usb.UsbManager?)context.GetSystemService(global::Android.Content.Context.UsbService);
            _logger.LogInformation("ConnectSerial: usbManager is {IsNull}", usbManager == null ? "NULL" : "OK");

            if (usbManager != null)
            {
                // Log all USB devices for debugging
                var devices = usbManager.DeviceList;
                _logger.LogInformation("ConnectSerial: DeviceList has {Count} device(s)", devices?.Count ?? 0);
                if (devices != null)
                {
                    foreach (var entry in devices)
                    {
                        var d = entry.Value;
                        _logger.LogInformation("ConnectSerial:   Device: Name={DeviceName}, VID={VendorId:X4}, PID={ProductId:X4}, HasPermission={HasPermission}",
                            d?.DeviceName, d?.VendorId ?? 0, d?.ProductId ?? 0, d != null && usbManager.HasPermission(d));
                    }
                }

                // Find the device by name
                global::Android.Hardware.Usb.UsbDevice? targetDevice = null;
                foreach (var entry in usbManager!.DeviceList!)
                {
                    if (entry.Value.DeviceName == portName ||
                        $"{entry.Value.VendorId:X4}:{entry.Value.ProductId:X4}" == portName)
                    {
                        targetDevice = entry.Value;
                        _logger.LogInformation("ConnectSerial: matched device '{DeviceName}' (VID={VendorId:X4}, PID={ProductId:X4})",
                            targetDevice.DeviceName, targetDevice.VendorId, targetDevice.ProductId);
                        break;
                    }
                }

                if (targetDevice == null)
                {
                    _logger.LogWarning("ConnectSerial: device '{PortName}' not found in DeviceList", portName);
                }

                if (targetDevice != null && !usbManager.HasPermission(targetDevice))
                {
                    _logger.LogInformation("ConnectSerial: no USB permission for '{PortName}', requesting...", portName);
                    var granted = await RequestUsbPermissionAsync(usbManager, targetDevice).ConfigureAwait(false);
                    _logger.LogInformation("ConnectSerial: USB permission result: {Granted}", granted);
                    if (!granted)
                    {
                        return JsonSerializer.Serialize(new { success = false, error = "USB permission denied. Please grant permission to access the ECU." });
                    }
                }

                // Inject UsbManager into ChannelFactory for UsbSerialChannel
                if (_channelFactory is ChannelFactory factory)
                {
                    _logger.LogInformation("ConnectSerial: setting UsbManager on ChannelFactory");
                    factory.SetUsbManager(usbManager);
                }
                else
                {
                    _logger.LogWarning("ConnectSerial: _channelFactory is not a ChannelFactory instance, cannot set UsbManager");
                }
            }
#endif

            var target = new ConnectionTarget.Serial(portName, baudRate);
            _lastTarget = target;
            _logger.LogInformation("ConnectSerial: calling _connection.ConnectAsync");
            var success = await _connection.ConnectAsync(target).ConfigureAwait(false);
            if (!success)
            {
                var error = _connection.LastError ?? "Connection failed. The device may not be supported.";
                _logger.LogWarning("ConnectSerial: connection failed — {Error}", error);
                return JsonSerializer.Serialize(new { success = false, error, state = _connection.State.ToString() });
            }
            _logger.LogInformation("ConnectSerial: connected successfully");
            return JsonSerializer.Serialize(new { success, state = _connection.State.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Serial connection failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Disconnect from the ECU. Called from JS: window.HybridWebView.InvokeDotNet('Disconnect')
    /// </summary>
    public async Task<string> Disconnect()
    {
        _logger.LogInformation("Disconnect called");
        try
        {
            // Force-persist odometer before disconnecting
            if (_activeDashboardName != null && _odometerByDashboard.TryGetValue(_activeDashboardName, out var odoState))
            {
                try { await PersistOdometerAsync(_activeDashboardName, odoState).ConfigureAwait(false); }
                catch (Exception odoEx) { _logger.LogWarning(odoEx, "Failed to persist odometer on disconnect"); }
            }

            await _connection.DisconnectAsync().ConfigureAwait(false);
            _lastTarget = null;
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disconnect failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get current connection state. Called from JS: window.HybridWebView.InvokeDotNet('GetConnectionState')
    /// </summary>
    public string GetConnectionState()
    {
        return JsonSerializer.Serialize(new
        {
            state = _connection.State.ToString(),
            connectionType = _lastTarget switch
            {
                ConnectionTarget.Tcp => "tcp",
                ConnectionTarget.Serial => "serial",
                _ => (string?)null
            },
            connectionDetail = _lastTarget switch
            {
                ConnectionTarget.Tcp t => $"{t.Host}:{t.Port}",
                ConnectionTarget.Serial s => s.PortName,
                _ => (string?)null
            },
            protocolInfo = _connection.ProtocolInfo != null ? new
            {
                product = _connection.ProtocolInfo.ProductName,
                model = _connection.ProtocolInfo.ModelName,
                version = _connection.ProtocolInfo.FirmwareVersion,
                reportingVersion = (int)_connection.ProtocolInfo.ReportingVersion,
                entityCount = _connection.ProtocolInfo.EntityMap.Count
            } : null,
            lastError = _connection.LastError
        });
    }

    /// <summary>
    /// Get available serial ports. Called from JS: window.HybridWebView.InvokeDotNet('GetAvailablePorts')
    /// </summary>
    public string GetAvailablePorts()
    {
        try
        {
#if ANDROID
            var context = global::Android.App.Application.Context;
            var usbManager = (global::Android.Hardware.Usb.UsbManager?)context.GetSystemService(global::Android.Content.Context.UsbService);
            if (usbManager == null)
            {
                return JsonSerializer.Serialize(new { ports = Array.Empty<object>() });
            }

            // Use UsbSerialProber to find compatible serial devices
            var prober = Anotherlab.UsbSerialForAndroid.Driver.UsbSerialProber.GetDefaultProber();
            var drivers = prober.FindAllDrivers(usbManager);

            var portList = new List<object>();
            foreach (var driver in drivers)
            {
                var device = driver.Device;
                if (device == null) continue;

                foreach (var port in driver.Ports)
                {
                    portList.Add(new
                    {
                        name = device.DeviceName,
                        description = $"{driver.GetType().Name.Replace("SerialDriver", "")} ({device.VendorId:X4}:{device.ProductId:X4})",
                        productId = device.ProductId,
                        vendorId = device.VendorId,
                        hasPermission = usbManager.HasPermission(device),
                    });
                }
            }

            // Also include unsupported USB devices so the user can see something is connected
            foreach (var entry in usbManager!.DeviceList!)
            {
                var device = entry.Value;
                if (!portList.Any(p => ((dynamic)p).name == device.DeviceName))
                {
                    portList.Add(new
                    {
                        name = device.DeviceName,
                        description = $"Unknown USB device ({device.VendorId:X4}:{device.ProductId:X4})",
                        productId = device.ProductId,
                        vendorId = device.VendorId,
                        hasPermission = usbManager.HasPermission(device),
                    });
                }
            }

            return JsonSerializer.Serialize(new { ports = portList.ToArray() });
#else
            var ports = SerialPort.GetPortNames();
            var portList = ports.Select(p => new { name = p }).ToArray();
            return JsonSerializer.Serialize(new { ports = portList });
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get serial ports");
            return JsonSerializer.Serialize(new { ports = Array.Empty<object>(), error = ex.Message });
        }
    }

    /// <summary>
    /// Enable ECU reporting and start receiving live data.
    /// Called from JS: window.HybridWebView.InvokeDotNet('EnableReporting')
    /// </summary>
    public async Task<string> EnableReporting()
    {
        _logger.LogInformation("EnableReporting called");
        try
        {
            var protocolInfo = await _connection.EnableReportingAsync().ConfigureAwait(false);

            // Start live data service
            var entityMap = protocolInfo.EntityMap.ToDictionary(
                e => e.Id,
                e => (e.Type, GetEntitySize(e.Type)));
            await _liveData.StartAsync(
                _connection.GetProtocolService(),
                (int)protocolInfo.ReportingVersion,
                entityMap).ConfigureAwait(false);

            return JsonSerializer.Serialize(new
            {
                success = true,
                reportingVersion = (int)protocolInfo.ReportingVersion,
                entityCount = protocolInfo.EntityMap.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable reporting");
            await _liveData.StopAsync().ConfigureAwait(false);
            try { await _connection.DisconnectAsync().ConfigureAwait(false); }
            catch (Exception disconnectEx) { _logger.LogWarning(disconnectEx, "Disconnect after reporting failure failed"); }
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Disable ECU reporting. Called from JS: window.HybridWebView.InvokeDotNet('DisableReporting')
    /// </summary>
    public async Task<string> DisableReporting()
    {
        _logger.LogInformation("DisableReporting called");
        try
        {
            await _liveData.StopAsync().ConfigureAwait(false);
            await _connection.DisableReportingAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable reporting");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private static int GetEntitySize(ReportingType type) => type switch
    {
        ReportingType.Float4B => 4,
        ReportingType.Int2B => 2,
        ReportingType.Uint2B => 2,
        ReportingType.Int1B => 1,
        ReportingType.Uint1B => 1,
        ReportingType.Bool1B => 1,
        _ => 4
    };
}
