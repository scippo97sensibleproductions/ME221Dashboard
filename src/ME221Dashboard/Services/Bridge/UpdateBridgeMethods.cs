using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
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
