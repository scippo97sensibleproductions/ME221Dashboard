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
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                await Launcher.OpenAsync(uri);
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
