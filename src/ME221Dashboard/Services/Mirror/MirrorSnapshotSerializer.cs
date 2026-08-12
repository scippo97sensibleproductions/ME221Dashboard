using ME221.Comms;
using ME221.Comms.Messages;
using ME221.Data.Infrastructure;
using ME221.Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Mirror;

/// <summary>
/// Builds the full A-to-B snapshot (U3, KTD-4): calibration (all table and driver
/// data), dashboard definitions (active dashboard, warning config/history,
/// preferences, odometer), ECU info, and embedded image bytes read from the paths
/// referenced by dashboards and gauges (the .mez precedent).
/// </summary>
public sealed class MirrorSnapshotSerializer(
    ICalibrationService calibration,
    IEcuConnectionService connection,
    Func<string>? appVersion = null,
    ILogger<MirrorSnapshotSerializer>? logger = null)
{
    private readonly ILogger<MirrorSnapshotSerializer> _logger = logger ?? NullLogger<MirrorSnapshotSerializer>.Instance;

    public async Task<MirrorSnapshot> BuildAsync(long seq)
    {
        CalibrationData? calibrationData = null;
        DashboardConfig? dashboardConfig = null;
        try
        {
            var cal = await calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            if (cal.Type == CalibrationResultType.Found)
                calibrationData = cal.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load calibration for the mirror snapshot");
        }

        try
        {
            dashboardConfig = await calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load dashboard config for the mirror snapshot");
        }

        if (calibrationData is not null)
        {
            // KTD-4: the snapshot must carry current table and driver VALUES so B's
            // read-only editors render real values. Real calibrations store structure
            // only (the arrays live on the ECU), so while connected each entity is
            // read over the wire into a deep copy — the live calibration object is
            // never mutated, and a failing read leaves that entity definitions-only
            // (B renders those tables empty, per the plan's edge case).
            if (connection.State == ConnectionState.Connected)
            {
                try
                {
                    calibrationData = await CaptureLiveValuesAsync(calibrationData).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Mirror snapshot: ECU value capture failed; snapshot carries definitions only");
                }
            }
            else
            {
                calibrationData = CloneCalibration(calibrationData);
            }
        }

        var ecuInfo = connection.ProtocolInfo is { } info && !string.IsNullOrEmpty(info.ProductName)
            ? new DeviceInfoMetadata { ProductName = info.ProductName, ModelName = info.ModelName, Version = info.FirmwareVersion }
            : null;

        var images = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (dashboardConfig is not null)
            CollectImages(dashboardConfig, images);

        return new MirrorSnapshot
        {
            Seq = seq,
            AppVersion = appVersion?.Invoke() ?? AppInfo.Current.VersionString,
            EcuConnected = connection.State == ConnectionState.Connected,
            EcuInfo = ecuInfo,
            Calibration = calibrationData ?? new CalibrationData(),
            DashboardConfig = dashboardConfig ?? new DashboardConfig(),
            Images = images,
        };
    }

    private void CollectImages(DashboardConfig config, Dictionary<string, string> images)
    {
        foreach (var dashboard in config.Dashboards.Values)
        {
            TryAddImage(dashboard.BackgroundImagePath, images);
            foreach (var gauge in dashboard.Gauges)
                TryAddImage(gauge.TexturePath, images);
        }
    }

    private void TryAddImage(string? path, Dictionary<string, string> images)
    {
        if (string.IsNullOrEmpty(path) || images.ContainsKey(path))
            return;
        try
        {
            if (!File.Exists(path))
                return;
            images[path] = Convert.ToBase64String(File.ReadAllBytes(path));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to embed image '{Path}' into the mirror snapshot", path);
        }
    }

    // ── ECU value capture (KTD-4) ───────────────────────────────────────────

    private async Task<CalibrationData> CaptureLiveValuesAsync(CalibrationData source)
    {
        var links = source.DataLinks.ToDictionary(l => l.Id);
        var protocol = connection.GetProtocolService();

        var tables = new List<TableDefinition>(source.Tables.Count);
        foreach (var table in source.Tables)
        {
            var input0 = table.Input0;
            var input1 = table.Input1;
            var output = table.Output;
            var enabled = table.Enabled;
            try
            {
                var response = await protocol.SendAsync<GetTableResponse>(new GetTableRequest(table.Id)).ConfigureAwait(false);
                if (response.Status == MessageStatus.Success)
                {
                    var wire = TableSerializer.Deserialize(table, response.SerializedTable.Span);
                    input0 = ConvertUnits(wire.Input0, table.Input0LinkId, links);
                    input1 = ConvertUnits(wire.Input1, table.Input1LinkId, links);
                    output = ConvertUnits(wire.Output, table.OutputLinkId, links);
                    enabled = wire.Enabled;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Mirror snapshot: table {TableId} value read failed — definitions only", table.Id);
            }
            tables.Add(CopyTable(table, input0, input1, output, enabled));
        }

        var drivers = new List<DriverDefinition>(source.Drivers.Count);
        foreach (var driver in source.Drivers)
        {
            var copy = CopyDriver(driver);
            try
            {
                var response = await protocol.SendAsync<GetDriverResponse>(new GetDriverRequest(driver.Id)).ConfigureAwait(false);
                if (response.Status == MessageStatus.Success)
                    copy = CopyDriverWithWire(driver, response.SerializedDriver.Span);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Mirror snapshot: driver {DriverId} value read failed — definitions only", driver.Id);
            }
            drivers.Add(copy);
        }

        return new CalibrationData
        {
            Metadata = source.Metadata,
            DataLinks = [.. source.DataLinks],
            Tables = tables,
            Drivers = drivers,
        };
    }

    /// <summary>Display-unit conversion identical to the ECU read path (TableBridgeMethods).</summary>
    private static List<float> ConvertUnits(float[] raw, ushort linkId, IReadOnlyDictionary<ushort, DataLinkDefinition> links)
    {
        var values = linkId == 0 || !links.TryGetValue(linkId, out var link)
            ? raw
            : MeasurementUnitConverter.FromRawArray(raw, link.MeasurementUnitTypes);
        return [.. values];
    }

    private static CalibrationData CloneCalibration(CalibrationData source) => new()
    {
        Metadata = source.Metadata,
        DataLinks = [.. source.DataLinks],
        Tables = [.. source.Tables.Select(t => CopyTable(t, t.Input0, t.Input1, t.Output, t.Enabled))],
        Drivers = [.. source.Drivers.Select(CopyDriver)],
    };

    private static TableDefinition CopyTable(TableDefinition t, List<float>? input0, List<float>? input1, List<float>? output, bool enabled) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Category = t.Category,
        ViewInTree = t.ViewInTree,
        Enabled = enabled,
        TableType = t.TableType,
        Cols = t.Cols,
        Rows = t.Rows,
        Input0LinkId = t.Input0LinkId,
        Input1LinkId = t.Input1LinkId,
        OutputLinkId = t.OutputLinkId,
        Input0Name = t.Input0Name,
        Input1Name = t.Input1Name,
        OutputName = t.OutputName,
        IncrementValue = t.IncrementValue,
        DefaultValue = t.DefaultValue,
        Input0 = input0,
        Input1 = input1,
        Output = output,
    };

    private static DriverDefinition CopyDriver(DriverDefinition d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Category = d.Category,
        ViewInTree = d.ViewInTree,
        NumberOfConfigs = d.NumberOfConfigs,
        Configs = d.Configs,
        NumberOfOutputs = d.NumberOfOutputs,
        OutputLinkIds = [.. d.OutputLinkIds],
        EditableOutputs = d.EditableOutputs,
        OutputNames = d.OutputNames,
        NumberOfInputs = d.NumberOfInputs,
        InputLinkIds = [.. d.InputLinkIds],
        EditableInputs = d.EditableInputs,
        InputNames = d.InputNames,
    };

    /// <summary>Driver copy with live config values and link ids (wire layout: [configs][outputIds][inputIds]).</summary>
    private static DriverDefinition CopyDriverWithWire(DriverDefinition d, ReadOnlySpan<byte> wireBytes)
    {
        var wire = DriverSerializer.Deserialize(wireBytes);
        return new DriverDefinition
        {
            Id = d.Id,
            Name = d.Name,
            Category = d.Category,
            ViewInTree = d.ViewInTree,
            NumberOfConfigs = d.NumberOfConfigs,
            Configs = wire.Configs.Length == d.Configs.Count
                ? d.Configs.Select((p, i) => CopyParam(p, wire.Configs[i])).ToList()
                : d.Configs,
            NumberOfOutputs = d.NumberOfOutputs,
            OutputLinkIds = [.. wire.OutputIds],
            EditableOutputs = d.EditableOutputs,
            OutputNames = d.OutputNames,
            NumberOfInputs = d.NumberOfInputs,
            InputLinkIds = [.. wire.InputIds],
            EditableInputs = d.EditableInputs,
            InputNames = d.InputNames,
        };
    }

    private static DriverParamDefinition CopyParam(DriverParamDefinition p, float value) => new()
    {
        Name = p.Name,
        DisplayName = p.DisplayName,
        SectionName = p.SectionName,
        ParamType = p.ParamType,
        ReadOnly = p.ReadOnly,
        RequiresReset = p.RequiresReset,
        Value = value,
        Min = p.Min,
        Max = p.Max,
        CheckRange = p.CheckRange,
        ToolTipText = p.ToolTipText,
        MeasurementUnitTypes = p.MeasurementUnitTypes,
        Options = p.Options,
        ViewConstraint = p.ViewConstraint,
    };
}