using System.Text.Json;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    /// <summary>
    /// Get ECU info (product, model, version). Called from JS: window.HybridWebView.InvokeDotNet('GetEcuInfo')
    /// </summary>
    public async Task<string> GetEcuInfo()
    {
        _logger.LogInformation("GetEcuInfo called");
        try
        {
            var protocol = _connection.GetProtocolService();
            var info = await _calibration.GetEcuInfoAsync(protocol).ConfigureAwait(false);
            return JsonSerializer.Serialize(new
            {
                success = true,
                product = info.Product,
                model = info.Model,
                version = info.Version
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetEcuInfo failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get persisted calibration metadata. Called from JS: window.HybridWebView.InvokeDotNet('GetPersistedCalibration')
    /// </summary>
    public async Task<string> GetPersistedCalibration()
    {
        _logger.LogInformation("GetPersistedCalibration called");
        try
        {
            var result = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(new
            {
                type = result.Type.ToString(),
                metadata = result.Data?.Metadata != null ? new
                {
                    productName = result.Data.Metadata.ProductName,
                    modelName = result.Data.Metadata.ModelName,
                    version = result.Data.Metadata.Version
                } : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPersistedCalibration failed");
            return JsonSerializer.Serialize(new { type = "Error", error = ex.Message });
        }
    }

    /// <summary>
    /// Check if saved calibration matches ECU info. Called from JS: window.HybridWebView.InvokeDotNet('CheckCalibrationMatch', [product, model, version])
    /// </summary>
    public async Task<string> CheckCalibrationMatch(string product, string model, string version)
    {
        _logger.LogInformation("CheckCalibrationMatch called");
        try
        {
            var result = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            if (result.Type != CalibrationResultType.Found || result.Data == null)
            {
                return JsonSerializer.Serialize(new { matched = false, hasSaved = false });
            }
            var matched = _calibration.MatchesEcu(result.Data, product, model, version);
            return JsonSerializer.Serialize(new
            {
                matched,
                hasSaved = true,
                metadata = result.Data.Metadata != null ? new
                {
                    productName = result.Data.Metadata.ProductName,
                    modelName = result.Data.Metadata.ModelName,
                    version = result.Data.Metadata.Version
                } : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckCalibrationMatch failed");
            return JsonSerializer.Serialize(new { matched = false, hasSaved = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Open file picker and load a .mefw calibration file. Called from JS: window.HybridWebView.InvokeDotNet('PickAndLoadCalibration')
    /// </summary>
    public async Task<string> PickAndLoadCalibration()
    {
        _logger.LogInformation("PickAndLoadCalibration called");
        _connection.PauseHeartbeat();
        try
        {
            var isCalWindows = DeviceInfo.Current.Platform == DevicePlatform.WinUI;
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select .mefw Calibration File",
                FileTypes = new FilePickerFileType(isCalWindows
                    ? new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, [".mefw"] }
                    }
                    : new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, ["application/octet-stream"] }
                    })
            }).ConfigureAwait(false);

            if (fileResult == null)
                return JsonSerializer.Serialize(new { picked = false });

            await using var stream = await fileResult.OpenReadAsync().ConfigureAwait(false);
            var data = await _calibration.LoadAndParseAsync(stream).ConfigureAwait(false);
            await _calibration.SaveCalibrationAsync(data).ConfigureAwait(false);

            return JsonSerializer.Serialize(new
            {
                picked = true,
                success = true,
                metadata = data.Metadata != null ? new
                {
                    productName = data.Metadata.ProductName,
                    modelName = data.Metadata.ModelName,
                    version = data.Metadata.Version
                } : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PickAndLoadCalibration failed");
            return JsonSerializer.Serialize(new { picked = true, success = false, error = ex.Message });
        }
        finally
        {
            _connection.ResumeHeartbeat();
        }
    }

    /// <summary>
    /// Force use of mismatched calibration. Called from JS: window.HybridWebView.InvokeDotNet('ForceUseCalibration')
    /// </summary>
    public async Task<string> ForceUseCalibration()
    {
        _logger.LogInformation("ForceUseCalibration called");
        try
        {
            await _connection.EnableReportingAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ForceUseCalibration failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
