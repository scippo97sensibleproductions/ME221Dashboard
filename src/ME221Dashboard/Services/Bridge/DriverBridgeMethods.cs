using System.Buffers.Binary;
using System.Text.Json;
using ME221.Comms.Messages;
using ME221.Data.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Driver Methods ──────────────────────────────────────────────────────

    /// <summary>
    /// Get all driver definitions from the loaded calibration.
    /// </summary>
    public async Task<string> GetDriverDefinitions()
    {
        _logger.LogInformation("GetDriverDefinitions called");
        try
        {
            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var drivers = calResult.Data?.Drivers ?? [];
            return JsonSerializer.Serialize(new
            {
                drivers = drivers.Where(d => d.ViewInTree).Select(d => new
                {
                    id = d.Id,
                    name = d.Name,
                    category = d.Category,
                    viewInTree = d.ViewInTree,
                    numberOfConfigs = d.NumberOfConfigs,
                    configs = d.Configs.Select(c => new
                    {
                        name = c.Name,
                        displayName = c.DisplayName,
                        sectionName = c.SectionName,
                        paramType = c.ParamType,
                        readOnly = c.ReadOnly,
                        requiresReset = c.RequiresReset,
                        value = c.Value,
                        min = c.Min,
                        max = c.Max,
                        checkRange = c.CheckRange,
                        toolTipText = c.ToolTipText,
                        options = c.Options?.Select(o => new { id = o.Id, name = o.Name }).ToList(),
                        viewConstraint = c.ViewConstraint is not null ? new
                        {
                            paramIndex = c.ViewConstraint.ParamIndex,
                            acceptedValues = c.ViewConstraint.AcceptedValues,
                        } : null,
                    }).ToList(),
                    numberOfOutputs = d.NumberOfOutputs,
                    outputLinkIds = d.OutputLinkIds,
                    editableOutputs = d.EditableOutputs,
                    outputNames = d.OutputNames,
                    numberOfInputs = d.NumberOfInputs,
                    inputLinkIds = d.InputLinkIds,
                    editableInputs = d.EditableInputs,
                    inputNames = d.InputNames,
                }),
            }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDriverDefinitions failed");
            return JsonSerializer.Serialize(new { drivers = Array.Empty<object>(), error = ex.Message });
        }
    }

    /// <summary>
    /// Read current driver data from the ECU.
    /// </summary>
    public async Task<string> ReadDriverData(string jsonPayload)
    {
        _logger.LogInformation("ReadDriverData called");
        try
        {
            var payload = System.Text.Json.Nodes.JsonNode.Parse(jsonPayload);
            var driverId = (ushort)(payload?["driverId"]?.GetValue<ushort>() ?? 0);

            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var driverDef = calResult.Data?.Drivers?.FirstOrDefault(d => d.Id == driverId);
            if (driverDef is null)
                return JsonSerializer.Serialize(new { error = $"Driver {driverId} not found" });

            var request = new GetDriverRequest(driverId);
            var protocol = _connection.GetProtocolService();
            var response = await protocol.SendAsync<GetDriverResponse>(request).ConfigureAwait(false);
            if (response.Status != MessageStatus.Success)
                return JsonSerializer.Serialize(new { error = $"ECU returned status {response.Status}" });

            var wireData = DriverSerializer.Deserialize(response.SerializedDriver.Span);
            return JsonSerializer.Serialize(new
            {
                configs = wireData.Configs,
                outputLinkIds = wireData.OutputIds,
                inputLinkIds = wireData.InputIds,
            }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReadDriverData failed for driver {DriverId}", jsonPayload);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Write driver config to the ECU.
    /// </summary>
    public async Task<string> SetDriverConfig(string jsonPayload)
    {
        _logger.LogInformation("SetDriverConfig called");
        try
        {
            var payload = System.Text.Json.Nodes.JsonNode.Parse(jsonPayload);
            var driverId = (ushort)(payload?["driverId"]?.GetValue<ushort>() ?? 0);
            var configs = payload?["configs"]?.Deserialize<float[]>() ?? [];
            var outputLinkIds = payload?["outputLinkIds"]?.Deserialize<List<ushort>>() ?? [];
            var inputLinkIds = payload?["inputLinkIds"]?.Deserialize<List<ushort>>() ?? [];

            var serialized = DriverSerializer.Serialize(configs, outputLinkIds, inputLinkIds);
            var request = new SetDriverRequest(driverId, serialized);
            var protocol = _connection.GetProtocolService();
            var response = await protocol.SendAsync<SetDriverResponse>(request).ConfigureAwait(false);

            if (response.Status != MessageStatus.Success)
                return JsonSerializer.Serialize(new { success = false, error = $"ECU returned status {response.Status}" });

            var storeRequest = new StoreDriverRequest(driverId);
            var storeResponse = await protocol.SendAsync<StoreDriverResponse>(storeRequest).ConfigureAwait(false);

            if (storeResponse.Status != MessageStatus.Success)
                return JsonSerializer.Serialize(new { success = false, error = $"ECU store failed with status {storeResponse.Status}" });

            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SetDriverConfig failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Store driver in ECU non-volatile memory.
    /// </summary>
    public async Task<string> StoreDriver(string jsonPayload)
    {
        _logger.LogInformation("StoreDriver called");
        try
        {
            var payload = System.Text.Json.Nodes.JsonNode.Parse(jsonPayload);
            var driverId = (ushort)(payload?["driverId"]?.GetValue<ushort>() ?? 0);

            var request = new StoreDriverRequest(driverId);
            var protocol = _connection.GetProtocolService();
            var response = await protocol.SendAsync<StoreDriverResponse>(request).ConfigureAwait(false);

            if (response.Status != MessageStatus.Success)
                return JsonSerializer.Serialize(new { success = false, error = $"ECU returned status {response.Status}" });

            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StoreDriver failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get all data link definitions from the loaded calibration.
    /// </summary>
    public async Task<string> GetDataLinks()
    {
        _logger.LogInformation("GetDataLinks called");
        try
        {
            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var dataLinks = calResult.Data?.DataLinks ?? [];
            return JsonSerializer.Serialize(new
            {
                dataLinks = dataLinks.Select(dl =>
                {
                    var (defMin, defMax) = GetUnitDefaults(dl.MeasureUnit);
                    var effectiveMin = dl.MinValue != 0 || dl.MaxValue != 0 ? dl.MinValue : (float)defMin;
                    var effectiveMax = dl.MinValue != 0 || dl.MaxValue != 0 ? dl.MaxValue : (float)defMax;
                    return new
                    {
                        id = dl.Id,
                        name = dl.Name,
                        category = dl.Category,
                        measureUnit = dl.MeasureUnit,
                        minValue = effectiveMin,
                        maxValue = effectiveMax,
                    };
                }),
            }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDataLinks failed");
            return JsonSerializer.Serialize(new { dataLinks = Array.Empty<object>(), error = ex.Message });
        }
    }
}
