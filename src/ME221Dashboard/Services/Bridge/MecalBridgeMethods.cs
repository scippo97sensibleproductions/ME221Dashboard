using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ME221.Comms.Messages;
using ME221.Data.Infrastructure;
using ME221.Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── .mecal Calibration Import/Export ────────────────────────────────────

    /// <summary>
    /// Export calibration as .mecal file. Reads all table/driver data from ECU.
    /// Called from JS: window.HybridWebView.InvokeDotNet('ExportMecal')
    /// </summary>
    public async Task<string> ExportMecal()
    {
        _logger.LogInformation("ExportMecal called");
        _connection.PauseHeartbeat();
        try
        {
            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            if (calResult.Data is null)
                return JsonSerializer.Serialize(new { success = false, error = "No calibration loaded" });

            var protocol = _connection.GetProtocolService();
            var calibration = calResult.Data;

            // Read all table data from ECU
            var tables = calibration.Tables;
            var links = calibration.DataLinks;
            var linkDict = links.ToDictionary(l => l.Id);

            var liveTables = new List<TableDefinition>();
            foreach (var tableDef in tables)
            {
                try
                {
                    var request = new GetTableRequest(tableDef.Id);
                    var response = await protocol.SendAsync<GetTableResponse>(request).ConfigureAwait(false);
                    if (response.Status != MessageStatus.Success) continue;

                    var wireData = TableSerializer.Deserialize(tableDef, response.SerializedTable.Span);

                    liveTables.Add(new TableDefinition
                    {
                        Id = tableDef.Id,
                        Name = tableDef.Name,
                        Category = tableDef.Category,
                        ViewInTree = tableDef.ViewInTree,
                        Enabled = wireData.Enabled,
                        TableType = tableDef.TableType,
                        Cols = tableDef.Cols,
                        Rows = tableDef.Rows,
                        Input0LinkId = tableDef.Input0LinkId,
                        Input1LinkId = tableDef.Input1LinkId,
                        OutputLinkId = tableDef.OutputLinkId,
                        Input0Name = tableDef.Input0Name,
                        Input1Name = tableDef.Input1Name,
                        OutputName = tableDef.OutputName,
                        IncrementValue = tableDef.IncrementValue,
                        DefaultValue = tableDef.DefaultValue,
                        Input0 = [.. wireData.Input0],
                        Input1 = [.. wireData.Input1],
                        Output = [.. wireData.Output],
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read table {TableId} during export", tableDef.Id);
                }
            }

            // Read all driver data from ECU
            var drivers = calibration.Drivers;
            var liveDrivers = new List<DriverDefinition>();
            foreach (var driverDef in drivers)
            {
                try
                {
                    var request = new GetDriverRequest(driverDef.Id);
                    var response = await protocol.SendAsync<GetDriverResponse>(request).ConfigureAwait(false);
                    if (response.Status != MessageStatus.Success) continue;

                    var wireData = DriverSerializer.Deserialize(response.SerializedDriver.Span);

                    liveDrivers.Add(new DriverDefinition
                    {
                        Id = driverDef.Id,
                        Name = driverDef.Name,
                        Category = driverDef.Category,
                        ViewInTree = driverDef.ViewInTree,
                        NumberOfConfigs = driverDef.NumberOfConfigs,
                        Configs = driverDef.Configs,
                        NumberOfOutputs = driverDef.NumberOfOutputs,
                        OutputLinkIds = [.. wireData.OutputIds],
                        EditableOutputs = driverDef.EditableOutputs,
                        OutputNames = driverDef.OutputNames,
                        NumberOfInputs = driverDef.NumberOfInputs,
                        InputLinkIds = [.. wireData.InputIds],
                        EditableInputs = driverDef.EditableInputs,
                        InputNames = driverDef.InputNames,
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read driver {DriverId} during export", driverDef.Id);
                }
            }

            // Build calibration data with live values
            var exportCalibration = new CalibrationData
            {
                Metadata = calibration.Metadata,
                DataLinks = calibration.DataLinks,
                Tables = liveTables,
                Drivers = liveDrivers,
            };

            var mecalXml = MecalWriter.Serialize(exportCalibration);
            var mecalBytes = System.Text.Encoding.UTF8.GetBytes(mecalXml);

            // Generate filename
            var meta = calibration.Metadata;
            var timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm");
            var fileName = $"{meta.ProductName}_{meta.ModelName}_{meta.Version}_{timestamp}.mecal";
            fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));

#if WINDOWS
            var windowsApp = App.Current;
            if (windowsApp?.Windows.Count > 0)
            {
                var savePicker = new Windows.Storage.Pickers.FileSavePicker
                {
                    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
                };
                savePicker.FileTypeChoices.Add("ME221 Calibration File", [".mecal"]);
                savePicker.SuggestedFileName = fileName;

                if (windowsApp.Windows[0]?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
                }

                var file = await savePicker.PickSaveFileAsync();
                if (file == null)
                    return JsonSerializer.Serialize(new { success = false, error = "Save cancelled" });

                using var stream = await file.OpenStreamForWriteAsync().ConfigureAwait(false);
                stream.SetLength(0);
                await stream.WriteAsync(mecalBytes).ConfigureAwait(false);
                _logger.LogInformation("Calibration exported to {Path} ({Size} bytes)", file.Path, mecalBytes.Length);
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    path = file.Path,
                    message = $"Saved to {file.Path}",
                    tables = liveTables.Count,
                    drivers = liveDrivers.Count,
                });
            }

            return JsonSerializer.Serialize(new { success = false, error = "No window available" });
#else
            var cacheDir = FileSystem.CacheDirectory;
            var filePath = Path.Combine(cacheDir, fileName);
            await File.WriteAllBytesAsync(filePath, mecalBytes).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
                Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Export Calibration",
                    File = new ShareFile(filePath)
                })).ConfigureAwait(false);

            _logger.LogInformation("Calibration exported to {Path}", filePath);
            return JsonSerializer.Serialize(new
            {
                success = true,
                tables = liveTables.Count,
                drivers = liveDrivers.Count,
            });
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExportMecal failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
        finally
        {
            _connection.ResumeHeartbeat();
        }
    }

    /// <summary>
    /// Import calibration from .mecal file. Parses and writes to ECU.
    /// Called from JS: window.HybridWebView.InvokeDotNet('ImportMecal')
    /// </summary>
    public async Task<string> ImportMecal()
    {
        _logger.LogInformation("ImportMecal called");
        _connection.PauseHeartbeat();
        try
        {
            var isWindows = DeviceInfo.Current.Platform == DevicePlatform.WinUI;
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Calibration File (.mecal)",
                FileTypes = new FilePickerFileType(isWindows
                    ? new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, [".mecal"] }
                    }
                    : new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, ["text/xml", "application/octet-stream"] }
                    })
            }).ConfigureAwait(false);

            if (fileResult == null)
                return JsonSerializer.Serialize(new { picked = false });

            var xml = await File.ReadAllTextAsync(fileResult.FullPath).ConfigureAwait(false);
            _logger.LogInformation("ImportMecal: read {Bytes} bytes from {Path}", xml.Length, fileResult.FullPath);

            var importCalibration = DefXmlParser.Parse(xml);
            _logger.LogInformation("ImportMecal: parsed — {Tables} tables, {Drivers} drivers, {Links} links",
                importCalibration.Tables.Count, importCalibration.Drivers.Count, importCalibration.DataLinks.Count);

            // Get current calibration for reference
            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            if (calResult.Data is null)
                return JsonSerializer.Serialize(new { picked = true, success = false, error = "No calibration loaded in app" });

            _logger.LogInformation("ImportMecal: current calibration has {Tables} tables, {Drivers} drivers",
                calResult.Data.Tables.Count, calResult.Data.Drivers.Count);

            var protocol = _connection.GetProtocolService();
            var currentCal = calResult.Data;

            // Write tables to ECU
            var tablesWritten = 0;
            var tablesFailed = 0;
            foreach (var importTable in importCalibration.Tables)
            {
                var currentTable = currentCal.Tables.FirstOrDefault(t => t.Id == importTable.Id);
                if (currentTable is null)
                {
                    _logger.LogWarning("Import: table {Id} not found in current calibration, skipping", importTable.Id);
                    tablesFailed++;
                    continue;
                }

                try
                {
                    _logger.LogInformation("Import: writing table {TableId} ({Name}) — {Input0}x{Input1}x{Output}",
                        importTable.Id, importTable.Name,
                        importTable.Input0?.Count ?? 0, importTable.Input1?.Count ?? 0, importTable.Output?.Count ?? 0);

                    var input0 = importTable.Input0?.ToArray() ?? [];
                    var input1 = importTable.Input1?.ToArray() ?? [];
                    var output = importTable.Output?.ToArray() ?? [];

                    var serialized = TableSerializer.Serialize(currentTable, importTable.Enabled, input0, input1, output);
                    _logger.LogInformation("Import: table {TableId} serialized to {Bytes} bytes", importTable.Id, serialized.Length);

                    var setRequest = new SetTableRequest(importTable.Id, serialized);
                    var setResponse = await protocol.SendAsync<SetTableResponse>(setRequest).ConfigureAwait(false);

                    _logger.LogInformation("Import: table {TableId} SET response status={Status}", importTable.Id, setResponse.Status);

                    if (setResponse.Status != MessageStatus.Success)
                    {
                        tablesFailed++;
                        continue;
                    }

                    var storeRequest = new StoreTableRequest(importTable.Id);
                    var storeResponse = await protocol.SendAsync<StoreTableResponse>(storeRequest).ConfigureAwait(false);

                    if (storeResponse.Status != MessageStatus.Success)
                    {
                        tablesFailed++;
                        continue;
                    }

                    tablesWritten++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Import: failed to write table {TableId}", importTable.Id);
                    tablesFailed++;
                }
            }

            // Write drivers to ECU
            var driversWritten = 0;
            var driversFailed = 0;
            foreach (var importDriver in importCalibration.Drivers)
            {
                var currentDriver = currentCal.Drivers.FirstOrDefault(d => d.Id == importDriver.Id);
                if (currentDriver is null)
                {
                    _logger.LogWarning("Import: driver {Id} not found in current calibration, skipping", importDriver.Id);
                    driversFailed++;
                    continue;
                }

                try
                {
                    var configs = importDriver.Configs.Select(c => c.Value).ToArray();
                    var outputLinkIds = importDriver.OutputLinkIds;
                    var inputLinkIds = importDriver.InputLinkIds;

                    _logger.LogInformation("Import: writing driver {DriverId} ({Name}) — {Configs} configs, {Outputs} outputs, {Inputs} inputs",
                        importDriver.Id, importDriver.Name,
                        configs.Length, outputLinkIds.Count, inputLinkIds.Count);

                    var serialized = DriverSerializer.Serialize(configs, outputLinkIds, inputLinkIds);
                    _logger.LogInformation("Import: driver {DriverId} serialized to {Bytes} bytes", importDriver.Id, serialized.Length);

                    var setRequest = new SetDriverRequest(importDriver.Id, serialized);
                    var setResponse = await protocol.SendAsync<SetDriverResponse>(setRequest).ConfigureAwait(false);

                    _logger.LogInformation("Import: driver {DriverId} SET response status={Status}", importDriver.Id, setResponse.Status);

                    if (setResponse.Status != MessageStatus.Success)
                    {
                        driversFailed++;
                        continue;
                    }

                    var storeRequest = new StoreDriverRequest(importDriver.Id);
                    var storeResponse = await protocol.SendAsync<StoreDriverResponse>(storeRequest).ConfigureAwait(false);

                    if (storeResponse.Status != MessageStatus.Success)
                    {
                        driversFailed++;
                        continue;
                    }

                    driversWritten++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Import: failed to write driver {DriverId}", importDriver.Id);
                    driversFailed++;
                }
            }

            _logger.LogInformation("Import complete: {TablesWritten} tables written, {TablesFailed} failed, {DriversWritten} drivers written, {DriversFailed} failed",
                tablesWritten, tablesFailed, driversWritten, driversFailed);

            return JsonSerializer.Serialize(new
            {
                picked = true,
                success = true,
                metadata = new
                {
                    productName = importCalibration.Metadata.ProductName,
                    modelName = importCalibration.Metadata.ModelName,
                    version = importCalibration.Metadata.Version,
                },
                tablesWritten,
                tablesFailed,
                driversWritten,
                driversFailed,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportMecal failed");
            return JsonSerializer.Serialize(new { picked = true, success = false, error = ex.Message });
        }
        finally
        {
            _connection.ResumeHeartbeat();
        }
    }

    /// <summary>
    /// Pick a .mecal file and return its content without importing.
    /// Called from JS: window.HybridWebView.InvokeDotNet('PickMecalFile')
    /// </summary>
    public async Task<string> PickMecalFile()
    {
        _logger.LogInformation("PickMecalFile called");
        try
        {
            var isWindows = DeviceInfo.Current.Platform == DevicePlatform.WinUI;
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Calibration File (.mecal)",
                FileTypes = new FilePickerFileType(isWindows
                    ? new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, [".mecal"] }
                    }
                    : new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, ["text/xml", "application/octet-stream"] }
                    })
            }).ConfigureAwait(false);

            if (fileResult == null)
                return JsonSerializer.Serialize(new { picked = false });

            var xml = await File.ReadAllTextAsync(fileResult.FullPath).ConfigureAwait(false);
            _logger.LogInformation("PickMecalFile: read {Bytes} bytes from {Path}", xml.Length, fileResult.FullPath);
            return JsonSerializer.Serialize(new { picked = true, content = xml });
        }
        catch (Exception ex)
        {
            _logger.LogError("PickMecalFile failed: {Error}", ex.Message);
            return JsonSerializer.Serialize(new { picked = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get summary of a .mecal file without writing to ECU.
    /// Compared against current calibration so UI can show what would change.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetMecalSummary', [fileContent])
    /// </summary>
    public async Task<string> GetMecalSummary(string fileContentB64)
    {
        _logger.LogInformation("GetMecalSummary called");
        try
        {
            var fileContent = Encoding.UTF8.GetString(Convert.FromBase64String(fileContentB64));
            var importCalibration = DefXmlParser.Parse(fileContent);

            // Get current calibration for comparison
            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var currentCal = calResult.Data;

            var currentTableIds = currentCal != null
                ? new HashSet<ushort>(currentCal.Tables.Select(t => t.Id))
                : new HashSet<ushort>();
            var currentDriverIds = currentCal != null
                ? new HashSet<ushort>(currentCal.Drivers.Select(d => d.Id))
                : new HashSet<ushort>();

            return JsonSerializer.Serialize(new
            {
                success = true,
                metadata = new
                {
                    productName = importCalibration.Metadata.ProductName,
                    modelName = importCalibration.Metadata.ModelName,
                    version = importCalibration.Metadata.Version,
                },
                tableCount = importCalibration.Tables.Count,
                driverCount = importCalibration.Drivers.Count,
                dataLinkCount = importCalibration.DataLinks.Count,
                tables = importCalibration.Tables.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    category = t.Category,
                    tableType = t.TableType,
                    cols = t.Cols,
                    rows = t.Rows,
                    existsInEcu = currentTableIds.Contains(t.Id),
                }),
                drivers = importCalibration.Drivers.Select(d => new
                {
                    id = d.Id,
                    name = d.Name,
                    category = d.Category,
                    numberOfConfigs = d.NumberOfConfigs,
                    existsInEcu = currentDriverIds.Contains(d.Id),
                }),
            }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError("GetMecalSummary failed: {Error}", ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get full import preview with current ECU data for comparison.
    /// Returns both import data and current ECU data side-by-side for each table.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetMecalImportPreview', [fileContentB64])
    /// </summary>
    public async Task<string> GetMecalImportPreview(string fileContentB64)
    {
        _logger.LogInformation("GetMecalImportPreview called");
        _connection.PauseHeartbeat();
        try
        {
            var fileContent = Encoding.UTF8.GetString(Convert.FromBase64String(fileContentB64));
            var importCalibration = DefXmlParser.Parse(fileContent);

            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            if (calResult.Data is null)
                return JsonSerializer.Serialize(new { success = false, error = "No calibration loaded" });

            var protocol = _connection.GetProtocolService();
            var currentCal = calResult.Data;

            // Read current ECU data for all tables in the import file
            var tableResults = new List<object>();
            foreach (var importTable in importCalibration.Tables)
            {
                var currentTable = currentCal.Tables.FirstOrDefault(t => t.Id == importTable.Id);
                if (currentTable is null)
                {
                tableResults.Add(new
                {
                    id = importTable.Id,
                    name = importTable.Name,
                    category = importTable.Category,
                    tableType = importTable.TableType,
                    cols = importTable.Cols,
                    rows = importTable.Rows,
                    existsInEcu = false,
                    input0Name = importTable.Input0Name,
                    input1Name = importTable.Input1Name,
                    outputName = importTable.OutputName,
                    currentInput0Name = (string?)null,
                    currentInput1Name = (string?)null,
                    currentOutputName = (string?)null,
                    import = new
                        {
                            enabled = importTable.Enabled,
                            input0 = importTable.Input0?.ToArray() ?? [],
                            input1 = importTable.Input1?.ToArray() ?? [],
                            output = importTable.Output?.ToArray() ?? [],
                        },
                        current = (object?)null,
                    });
                    continue;
                }

                // Read live data from ECU
                float[] currentInput0 = [], currentInput1 = [], currentOutput = [];
                var readSuccess = false;
                try
                {
                    var request = new GetTableRequest(importTable.Id);
                    var response = await protocol.SendAsync<GetTableResponse>(request).ConfigureAwait(false);
                    if (response.Status == MessageStatus.Success)
                    {
                        var wireData = TableSerializer.Deserialize(currentTable, response.SerializedTable.Span);
                        currentInput0 = wireData.Input0;
                        currentInput1 = wireData.Input1;
                        currentOutput = wireData.Output;
                        readSuccess = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Preview: failed to read table {TableId} from ECU", importTable.Id);
                }

                tableResults.Add(new
                {
                    id = importTable.Id,
                    name = importTable.Name,
                    category = importTable.Category,
                    tableType = importTable.TableType,
                    cols = importTable.Cols,
                    rows = importTable.Rows,
                    existsInEcu = true,
                    input0Name = importTable.Input0Name,
                    input1Name = importTable.Input1Name,
                    outputName = importTable.OutputName,
                    currentInput0Name = currentTable.Input0Name,
                    currentInput1Name = currentTable.Input1Name,
                    currentOutputName = currentTable.OutputName,
                    import = new
                    {
                        enabled = importTable.Enabled,
                        input0 = importTable.Input0?.ToArray() ?? [],
                        input1 = importTable.Input1?.ToArray() ?? [],
                        output = importTable.Output?.ToArray() ?? [],
                    },
                    current = readSuccess ? new
                    {
                        enabled = currentTable.Enabled,
                        input0 = currentInput0,
                        input1 = currentInput1,
                        output = currentOutput,
                    } : null,
                });
            }

            // Drivers — summary only (no live data read for drivers)
            var driverResults = importCalibration.Drivers.Select(d => new
            {
                id = d.Id,
                name = d.Name,
                category = d.Category,
                numberOfConfigs = d.NumberOfConfigs,
                existsInEcu = currentCal.Drivers.Any(cd => cd.Id == d.Id),
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                metadata = new
                {
                    productName = importCalibration.Metadata.ProductName,
                    modelName = importCalibration.Metadata.ModelName,
                    version = importCalibration.Metadata.Version,
                },
                tables = tableResults,
                drivers = driverResults,
                dataLinkCount = importCalibration.DataLinks.Count,
            }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError("GetMecalImportPreview failed: {Error}", ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
        finally
        {
            _connection.ResumeHeartbeat();
        }
    }

    /// <summary>
    /// Apply a .mecal import with selective table/driver selection.
    /// Called from JS: window.HybridWebView.InvokeDotNet('ApplyMecalImport', [fileContent, selectedTableIds, selectedDriverIds])
    /// </summary>
    public async Task<string> ApplyMecalImport(string fileContentB64, string selectedTableIdsJson, string selectedDriverIdsJson)
    {
        _logger.LogInformation("ApplyMecalImport called");
        _connection.PauseHeartbeat();
        try
        {
            var fileContent = Encoding.UTF8.GetString(Convert.FromBase64String(fileContentB64));
            var importCalibration = DefXmlParser.Parse(fileContent);
            var selectedTableIds = JsonSerializer.Deserialize<ushort[]>(selectedTableIdsJson) ?? [];
            var selectedDriverIds = JsonSerializer.Deserialize<ushort[]>(selectedDriverIdsJson) ?? [];
            var selectedTableSet = new HashSet<ushort>(selectedTableIds);
            var selectedDriverSet = new HashSet<ushort>(selectedDriverIds);

            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            if (calResult.Data is null)
                return JsonSerializer.Serialize(new { success = false, error = "No calibration loaded in app" });

            var protocol = _connection.GetProtocolService();
            var currentCal = calResult.Data;

            // Write selected tables to ECU
            var tablesWritten = 0;
            var tablesFailed = 0;
            var tablesSkipped = 0;
            foreach (var importTable in importCalibration.Tables)
            {
                if (!selectedTableSet.Contains(importTable.Id))
                {
                    tablesSkipped++;
                    continue;
                }

                var currentTable = currentCal.Tables.FirstOrDefault(t => t.Id == importTable.Id);
                if (currentTable is null)
                {
                    _logger.LogWarning("Import: table {Id} not found in current calibration, skipping", importTable.Id);
                    tablesFailed++;
                    continue;
                }

                try
                {
                    var input0 = importTable.Input0?.ToArray() ?? [];
                    var input1 = importTable.Input1?.ToArray() ?? [];
                    var output = importTable.Output?.ToArray() ?? [];

                    var serialized = TableSerializer.Serialize(currentTable, importTable.Enabled, input0, input1, output);
                    var setRequest = new SetTableRequest(importTable.Id, serialized);
                    var setResponse = await protocol.SendAsync<SetTableResponse>(setRequest).ConfigureAwait(false);

                    if (setResponse.Status != MessageStatus.Success)
                    {
                        tablesFailed++;
                        continue;
                    }

                    var storeRequest = new StoreTableRequest(importTable.Id);
                    var storeResponse = await protocol.SendAsync<StoreTableResponse>(storeRequest).ConfigureAwait(false);

                    if (storeResponse.Status != MessageStatus.Success)
                    {
                        tablesFailed++;
                        continue;
                    }

                    tablesWritten++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Import: failed to write table {TableId}", importTable.Id);
                    tablesFailed++;
                }
            }

            // Write selected drivers to ECU
            var driversWritten = 0;
            var driversFailed = 0;
            var driversSkipped = 0;
            foreach (var importDriver in importCalibration.Drivers)
            {
                if (!selectedDriverSet.Contains(importDriver.Id))
                {
                    driversSkipped++;
                    continue;
                }

                var currentDriver = currentCal.Drivers.FirstOrDefault(d => d.Id == importDriver.Id);
                if (currentDriver is null)
                {
                    _logger.LogWarning("Import: driver {Id} not found in current calibration, skipping", importDriver.Id);
                    driversFailed++;
                    continue;
                }

                try
                {
                    var configs = importDriver.Configs.Select(c => c.Value).ToArray();
                    var outputLinkIds = importDriver.OutputLinkIds;
                    var inputLinkIds = importDriver.InputLinkIds;

                    var serialized = DriverSerializer.Serialize(configs, outputLinkIds, inputLinkIds);
                    var setRequest = new SetDriverRequest(importDriver.Id, serialized);
                    var setResponse = await protocol.SendAsync<SetDriverResponse>(setRequest).ConfigureAwait(false);

                    if (setResponse.Status != MessageStatus.Success)
                    {
                        driversFailed++;
                        continue;
                    }

                    var storeRequest = new StoreDriverRequest(importDriver.Id);
                    var storeResponse = await protocol.SendAsync<StoreDriverResponse>(storeRequest).ConfigureAwait(false);

                    if (storeResponse.Status != MessageStatus.Success)
                    {
                        driversFailed++;
                        continue;
                    }

                    driversWritten++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Import: failed to write driver {DriverId}", importDriver.Id);
                    driversFailed++;
                }
            }

            _logger.LogInformation("ApplyMecalImport complete: {TablesWritten}/{TablesSelected} tables, {DriversWritten}/{DriversSelected} drivers",
                tablesWritten, selectedTableSet.Count, driversWritten, selectedDriverSet.Count);

            return JsonSerializer.Serialize(new
            {
                success = true,
                tablesWritten,
                tablesFailed,
                tablesSkipped,
                driversWritten,
                driversFailed,
                driversSkipped,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("ApplyMecalImport failed: {Error}", ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
        finally
        {
            _connection.ResumeHeartbeat();
        }
    }

    /// <summary>
    /// Write multiple tables to ECU with pre-computed data.
    /// Called from JS: window.HybridWebView.InvokeDotNet('WriteTableDataBatch', [jsonPayload])
    /// </summary>
    public async Task<string> WriteTableDataBatch(string jsonPayload)
    {
        _logger.LogInformation("WriteTableDataBatch called");
        _connection.PauseHeartbeat();
        try
        {
            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            if (calResult.Data is null)
                return JsonSerializer.Serialize(new { success = false, error = "No calibration loaded" });

            var protocol = _connection.GetProtocolService();
            var linkDict = calResult.Data.DataLinks.ToDictionary(l => l.Id);

            var payload = JsonNode.Parse(jsonPayload);
            var tablesArray = payload?.AsArray() ?? [];

            var written = 0;
            var failed = 0;

            foreach (var entry in tablesArray)
            {
                var tableId = (ushort)(entry?["tableId"]?.GetValue<ushort>() ?? 0);
                var input0 = entry?["input0"]?.Deserialize<float[]>() ?? [];
                var input1 = entry?["input1"]?.Deserialize<float[]>() ?? [];
                var output = entry?["output"]?.Deserialize<float[]>() ?? [];

                var tableDef = calResult.Data.Tables.FirstOrDefault(t => t.Id == tableId);
                if (tableDef is null)
                {
                    _logger.LogWarning("WriteTableDataBatch: table {TableId} not found", tableId);
                    failed++;
                    continue;
                }

                try
                {
                    // Convert display values back to raw
                    if (linkDict.TryGetValue(tableDef.Input0LinkId, out var input0Link))
                        input0 = MeasurementUnitConverter.ToRawArray(input0, input0Link.MeasurementUnitTypes);
                    if (linkDict.TryGetValue(tableDef.Input1LinkId, out var input1Link))
                        input1 = MeasurementUnitConverter.ToRawArray(input1, input1Link.MeasurementUnitTypes);
                    if (linkDict.TryGetValue(tableDef.OutputLinkId, out var outputLink))
                        output = MeasurementUnitConverter.ToRawArray(output, outputLink.MeasurementUnitTypes);

                    var serialized = TableSerializer.Serialize(tableDef, true, input0, input1, output);
                    var setRequest = new SetTableRequest(tableId, serialized);
                    var setResponse = await protocol.SendAsync<SetTableResponse>(setRequest).ConfigureAwait(false);

                    if (setResponse.Status != MessageStatus.Success)
                    {
                        _logger.LogWarning("WriteTableDataBatch: table {TableId} SET failed: {Status}", tableId, setResponse.Status);
                        failed++;
                        continue;
                    }

                    var storeRequest = new StoreTableRequest(tableId);
                    var storeResponse = await protocol.SendAsync<StoreTableResponse>(storeRequest).ConfigureAwait(false);

                    if (storeResponse.Status != MessageStatus.Success)
                    {
                        _logger.LogWarning("WriteTableDataBatch: table {TableId} STORE failed: {Status}", tableId, storeResponse.Status);
                        failed++;
                        continue;
                    }

                    written++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "WriteTableDataBatch: table {TableId} failed", tableId);
                    failed++;
                }
            }

            _logger.LogInformation("WriteTableDataBatch complete: {Written} written, {Failed} failed", written, failed);
            return JsonSerializer.Serialize(new { success = true, written, failed });
        }
        catch (Exception ex)
        {
            _logger.LogError("WriteTableDataBatch failed: {Error}", ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
        finally
        {
            _connection.ResumeHeartbeat();
        }
    }
}
