using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    /// <summary>
    /// Open a URL in the device's default browser.
    /// Called from JS: window.HybridWebView.InvokeDotNet('OpenExternalUrl', [url])
    /// </summary>
    public async Task OpenExternalUrl(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                _logger.LogWarning("OpenExternalUrl: invalid URL '{Url}'", url);
                return;
            }

#if ANDROID
            // Launcher.OpenAsync silently fails when invoked off the UI thread in
            // some MAUI contexts. Build the Intent ourselves and dispatch it on
            // the main thread — this is the canonical Android way to open a
            // browser and matches what PermissionBridgeMethods does for the
            // storage permission settings activity.
            var context = global::Android.App.Application.Context;
            var intent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionView);
            intent.SetData(global::Android.Net.Uri.Parse(uri.ToString()));
            intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    context.StartActivity(intent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "StartActivity failed for URL: {Url}", url);
                }
            });
#else
            await Launcher.OpenAsync(uri);
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open external URL: {Url}", url);
        }
    }

    /// <summary>
    /// Check for a new release on GitHub.
    /// Called from JS: window.HybridWebView.InvokeDotNet('CheckForUpdate')
    /// </summary>
    public async Task<string> CheckForUpdate()
    {
        if (_updateChecker is null)
        {
            return JsonSerializer.Serialize(new UpdateCheckResult
            {
                UpdateAvailable = false,
                CurrentVersion = AppInfo.Current.VersionString,
            });
        }

        try
        {
            var result = await _updateChecker.CheckForUpdateAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(result, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckForUpdate failed");
            return JsonSerializer.Serialize(new UpdateCheckResult
            {
                UpdateAvailable = false,
                CurrentVersion = AppInfo.Current.VersionString,
            });
        }
    }
}
