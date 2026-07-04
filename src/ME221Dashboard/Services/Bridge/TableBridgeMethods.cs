using System.Text.Json;
using System.Text.Json.Nodes;
using ME221.Comms.Messages;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Table Methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Get all table definitions from the loaded calibration. Called from JS: window.HybridWebView.InvokeDotNet('GetTableDefinitions')
    /// </summary>
    public async Task<string> GetTableDefinitions()
    {
        _logger.LogInformation("GetTableDefinitions called");
        try
        {
            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var tables = calResult.Data?.Tables ?? [];
            return JsonSerializer.Serialize(new
            {
                tables = tables.Where(t => t.ViewInTree).Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    category = t.Category,
                    viewInTree = t.ViewInTree,
                    enabled = t.Enabled,
                    tableType = t.TableType,
                    cols = t.Cols,
                    rows = t.Rows,
                    input0Name = t.Input0Name,
                    input1Name = t.Input1Name,
                    outputName = t.OutputName,
                    input0LinkId = t.Input0LinkId,
                    input1LinkId = t.Input1LinkId,
                    outputLinkId = t.OutputLinkId,
                    incrementValue = t.IncrementValue,
                    defaultValue = t.DefaultValue,
                }),
            }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTableDefinitions failed");
            return JsonSerializer.Serialize(new { tables = Array.Empty<object>(), error = ex.Message });
        }
    }

    /// <summary>
    /// Read current table data from the ECU. Called from JS: window.HybridWebView.InvokeDotNet('ReadTableData', [tableId])
    /// </summary>
    public async Task<string> ReadTableData(ushort tableId)
    {
        _logger.LogInformation("ReadTableData called");
        try
        {
            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var tableDef = calResult.Data?.Tables?.FirstOrDefault(t => t.Id == tableId);
            if (tableDef is null)
                return JsonSerializer.Serialize(new { success = false, error = $"Table {tableId} not found" });

            var request = new GetTableRequest(tableId);
            var protocol = _connection.GetProtocolService();
            var response = await protocol.SendAsync<GetTableResponse>(request).ConfigureAwait(false);
            if (response.Status != ME221.Comms.Messages.MessageStatus.Success)
                return JsonSerializer.Serialize(new { success = false, error = $"ECU returned status {response.Status}" });

            var wireData = ME221.Data.Infrastructure.TableSerializer.Deserialize(tableDef, response.SerializedTable.Span);
            return JsonSerializer.Serialize(new
            {
                success = true,
                enabled = wireData.Enabled,
                rows = tableDef.Rows,
                cols = tableDef.Cols,
                tableType = tableDef.TableType,
                input0 = wireData.Input0,
                input1 = wireData.Input1,
                output = wireData.Output,
            }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReadTableData failed for table {TableId}", tableId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Read multiple tables in one call. Called from JS: window.HybridWebView.InvokeDotNet('ReadTableDataBatch', [tableIdArray])
    /// </summary>
    public async Task<string> ReadTableDataBatch(string tableIdsJson)
    {
        try
        {
            var ids = JsonSerializer.Deserialize<ushort[]>(tableIdsJson) ?? [];
            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var allTables = calResult.Data?.Tables ?? [];
            var protocol = _connection.GetProtocolService();

            var results = new Dictionary<int, object>();
            foreach (var tableId in ids)
            {
                var tableDef = allTables.FirstOrDefault(t => t.Id == tableId);
                if (tableDef is null) continue;

                try
                {
                    var request = new GetTableRequest(tableId);
                    var response = await protocol.SendAsync<GetTableResponse>(request).ConfigureAwait(false);
                    if (response.Status != ME221.Comms.Messages.MessageStatus.Success) continue;

                    var wireData = ME221.Data.Infrastructure.TableSerializer.Deserialize(tableDef, response.SerializedTable.Span);
                    results[tableId] = new
                    {
                        success = true,
                        enabled = wireData.Enabled,
                        input0 = wireData.Input0,
                        input1 = wireData.Input1,
                        output = wireData.Output,
                    };
                }
                catch
                {
                    // skip failed tables
                }
            }

            return JsonSerializer.Serialize(new { results }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReadTableDataBatch failed");
            return JsonSerializer.Serialize(new { results = new Dictionary<int, object>(), error = ex.Message });
        }
    }

    /// <summary>
    /// Write modified table data to the ECU. Called from JS: window.HybridWebView.InvokeDotNet('WriteTableData', [jsonPayload])
    /// </summary>
    public async Task<string> WriteTableData(string jsonPayload)
    {
        _logger.LogInformation("WriteTableData called");
        try
        {
            var payload = JsonNode.Parse(jsonPayload);
            var tableId = (ushort)(payload?["tableId"]?.GetValue<ushort>() ?? 0);
            var input0 = payload?["input0"]?.Deserialize<float[]>() ?? [];
            var input1 = payload?["input1"]?.Deserialize<float[]>() ?? [];
            var output = payload?["output"]?.Deserialize<float[]>() ?? [];

            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var tableDef = calResult.Data?.Tables?.FirstOrDefault(t => t.Id == tableId);
            if (tableDef is null)
                return JsonSerializer.Serialize(new { success = false, error = $"Table {tableId} not found" });

            var serialized = ME221.Data.Infrastructure.TableSerializer.Serialize(tableDef, true, input0, input1, output);
            var request = new SetTableRequest(tableId, serialized);
            var protocol = _connection.GetProtocolService();
            var response = await protocol.SendAsync<SetTableResponse>(request).ConfigureAwait(false);

            if (response.Status != ME221.Comms.Messages.MessageStatus.Success)
                return JsonSerializer.Serialize(new { success = false, error = $"ECU returned status {response.Status}" });

            var storeRequest = new StoreTableRequest(tableId);
            var storeResponse = await protocol.SendAsync<StoreTableResponse>(storeRequest).ConfigureAwait(false);

            if (storeResponse.Status != ME221.Comms.Messages.MessageStatus.Success)
                return JsonSerializer.Serialize(new { success = false, error = $"ECU store failed with status {storeResponse.Status}" });

            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WriteTableData failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
