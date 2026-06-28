using System.Text.Json;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Permission Methods (Android Welcome Page) ───────────────────────────

    /// <summary>
    /// Get the current platform name. Called from JS: window.HybridWebView.InvokeDotNet('GetPlatform')
    /// </summary>
    public string GetPlatform()
    {
        return JsonSerializer.Serialize(new { platform = DeviceInfo.Platform.ToString() });
    }

    /// <summary>
    /// Get current permission status (USB, location, storage).
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetPermissionStatus')
    /// </summary>
    public async Task<string> GetPermissionStatus()
    {
        _logger.LogInformation("GetPermissionStatus called");
        var isAndroid = DeviceInfo.Platform == DevicePlatform.Android;
        var usbHostAvailable = false;
        var usbPermissionGranted = false;
        var locationGranted = false;
        var storageGranted = false;

#if ANDROID
        var context = global::Android.App.Application.Context;
        var pm = context.PackageManager!;
        usbHostAvailable = pm.HasSystemFeature(global::Android.Content.PM.PackageManager.FeatureUsbHost);

        if (usbHostAvailable)
        {
            var usbManager = (global::Android.Hardware.Usb.UsbManager)context.GetSystemService(global::Android.Content.Context.UsbService)!;
            var devices = usbManager.DeviceList;
            if (devices != null && devices.Count > 0)
            {
                foreach (var entry in devices)
                {
                    if (usbManager.HasPermission(entry.Value))
                    {
                        usbPermissionGranted = true;
                        break;
                    }
                }
            }
        }

        // MAUI Permissions API must be called on the main thread
        var locCheck = await MainThread.InvokeOnMainThreadAsync(() =>
            Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>()).ConfigureAwait(false);
        locationGranted = locCheck == PermissionStatus.Granted;

        // Storage
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            storageGranted = global::Android.OS.Environment.IsExternalStorageManager;
        }
        else
        {
            var storageCheck = await MainThread.InvokeOnMainThreadAsync(() =>
                Permissions.CheckStatusAsync<Permissions.StorageWrite>()).ConfigureAwait(false);
            storageGranted = storageCheck == PermissionStatus.Granted;
        }
#endif

        return JsonSerializer.Serialize(new
        {
            isAndroid,
            usbHostAvailable,
            usbPermissionGranted,
            locationGranted,
            storageGranted,
            // USB permission is per-device runtime consent (UsbManager.RequestPermission),
            // not a pre-grantable gate — handled at connection time.
            // Location + Storage are the actual gating runtime permissions.
            allGranted = !isAndroid || (locationGranted && storageGranted)
        });
    }

    /// <summary>
    /// Request USB device permission on Android. Called from JS: window.HybridWebView.InvokeDotNet('RequestUsbPermission')
    /// </summary>
    public async Task<string> RequestUsbPermission()
    {
        _logger.LogInformation("RequestUsbPermission called");
#if ANDROID
        try
        {
            var context = global::Android.App.Application.Context;
            var usbManager = (global::Android.Hardware.Usb.UsbManager)context.GetSystemService(global::Android.Content.Context.UsbService)!;
            var devices = usbManager.DeviceList;

            if (devices == null || devices.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    granted = false,
                    error = "No USB device detected. Please connect your ECU."
                });
            }

            foreach (var entry in devices)
            {
                if (!usbManager.HasPermission(entry.Value))
                {
                    var granted = await RequestUsbPermissionAsync(usbManager, entry.Value);
                    return JsonSerializer.Serialize(new { granted, error = granted ? null : "USB permission denied" });
                }
            }

            return JsonSerializer.Serialize(new { granted = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RequestUsbPermission failed");
            return JsonSerializer.Serialize(new { granted = false, error = ex.Message });
        }
#else
        return JsonSerializer.Serialize(new { granted = true });
#endif
    }

    /// <summary>
    /// Request location (ACCESS_FINE_LOCATION) permission. Called from JS: window.HybridWebView.InvokeDotNet('RequestLocationPermission')
    /// </summary>
    public async Task<string> RequestLocationPermission()
    {
        _logger.LogInformation("RequestLocationPermission called");
#if ANDROID
        try
        {
            // MAUI Permissions API must be called on the main thread
            var status = await MainThread.InvokeOnMainThreadAsync(() =>
                Permissions.RequestAsync<Permissions.LocationWhenInUse>()).ConfigureAwait(false);
            return JsonSerializer.Serialize(new
            {
                granted = status == PermissionStatus.Granted,
                status = status.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RequestLocationPermission failed");
            return JsonSerializer.Serialize(new { granted = false, error = ex.Message });
        }
#else
        return JsonSerializer.Serialize(new { granted = true });
#endif
    }

    /// <summary>
    /// Request storage permission (MANAGE_EXTERNAL_STORAGE on 11+, legacy on older).
    /// Called from JS: window.HybridWebView.InvokeDotNet('RequestStoragePermission')
    /// </summary>
    public async Task<string> RequestStoragePermission()
    {
        _logger.LogInformation("RequestStoragePermission called");
#if ANDROID
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var context = global::Android.App.Application.Context;
                var intent = new global::Android.Content.Intent(
                    global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                intent.SetData(global::Android.Net.Uri.Parse("package:" + context.PackageName));
                intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
                context.StartActivity(intent);
                return JsonSerializer.Serialize(new
                {
                    granted = false,
                    action = "open_settings",
                    message = "Toggle \"Allow access to manage all files\" and come back."
                });
            }
            else
            {
                // MAUI Permissions API must be called on the main thread
                var status = await MainThread.InvokeOnMainThreadAsync(() =>
                    Permissions.RequestAsync<Permissions.StorageWrite>()).ConfigureAwait(false);
                return JsonSerializer.Serialize(new
                {
                    granted = status == PermissionStatus.Granted,
                    status = status.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RequestStoragePermission failed");
            return JsonSerializer.Serialize(new { granted = false, error = ex.Message });
        }
#else
        return JsonSerializer.Serialize(new { granted = true });
#endif
    }

#if ANDROID
    private const string ActionUsbPermission = "com.me221.dashboard.USB_PERMISSION";

    private static async Task<bool> RequestUsbPermissionAsync(
        global::Android.Hardware.Usb.UsbManager usbManager,
        global::Android.Hardware.Usb.UsbDevice device)
    {
        var tcs = new TaskCompletionSource<bool>();
        var context = global::Android.App.Application.Context;

        var intent = new global::Android.Content.Intent(ActionUsbPermission);
        intent.SetPackage(context.PackageName);

        var flags = global::Android.App.PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
            flags |= global::Android.App.PendingIntentFlags.Mutable;

        var pendingIntent = global::Android.App.PendingIntent.GetBroadcast(context, 0, intent, flags)!;

        var receiver = new UsbPermissionReceiver(device, pendingIntent, tcs);

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            context.RegisterReceiver(receiver,
                new global::Android.Content.IntentFilter(ActionUsbPermission),
                global::Android.Content.ReceiverFlags.NotExported);
        }
        else
        {
            context.RegisterReceiver(receiver, new global::Android.Content.IntentFilter(ActionUsbPermission));
        }

        usbManager.RequestPermission(device, pendingIntent);

        try
        {
            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        }
        finally
        {
            try { context.UnregisterReceiver(receiver); } catch { }
        }
    }

    private sealed class UsbPermissionReceiver(
        global::Android.Hardware.Usb.UsbDevice device,
        global::Android.App.PendingIntent pendingIntent,
        TaskCompletionSource<bool> tcs)
        : global::Android.Content.BroadcastReceiver
    {
        public override void OnReceive(global::Android.Content.Context? context, global::Android.Content.Intent? intent)
        {
            if (intent?.Action != ActionUsbPermission) return;

            global::Android.Hardware.Usb.UsbDevice? device1;
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                device1 = intent.GetParcelableExtra(
                    global::Android.Hardware.Usb.UsbManager.ExtraDevice,
                    Java.Lang.Class.FromType(typeof(global::Android.Hardware.Usb.UsbDevice)))
                    as global::Android.Hardware.Usb.UsbDevice;
            }
            else
            {
                device1 = intent.GetParcelableExtra(
                    global::Android.Hardware.Usb.UsbManager.ExtraDevice)
                    as global::Android.Hardware.Usb.UsbDevice;
            }

            var granted = intent.GetBooleanExtra(
                global::Android.Hardware.Usb.UsbManager.ExtraPermissionGranted, false);

            if (device1 != null && device1.DeviceId == device.DeviceId && granted)
                tcs.TrySetResult(true);
            else
                tcs.TrySetResult(false);

            pendingIntent.Cancel();
        }
    }
#endif
}
