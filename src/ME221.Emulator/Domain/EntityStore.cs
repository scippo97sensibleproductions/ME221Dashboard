using System.Collections.Concurrent;
using ME221.Data.Models;

namespace ME221.Emulator.Domain;

public sealed class EntityStore
{
    private readonly ConcurrentDictionary<ushort, float[]> _tableOutputs = new();
    private readonly ConcurrentDictionary<ushort, float[]> _tableInput0 = new();
    private readonly ConcurrentDictionary<ushort, float[]> _tableInput1 = new();
    private readonly ConcurrentDictionary<ushort, bool> _tableEnabled = new();
    private readonly ConcurrentDictionary<ushort, float[]> _driverConfigs = new();
    private readonly ConcurrentDictionary<ushort, float> _dataLinkValues = new();
    private readonly ConcurrentDictionary<ushort, byte> _dataLinkReportingTypes = new();

    public CalibrationData Calibration { get; }

    public EntityStore(CalibrationData calibration)
    {
        Calibration = calibration;
        InitializeDefaults();
    }

    private void InitializeDefaults()
    {
        foreach (var table in Calibration.Tables)
        {
            // Only tables with real calibration data are registered. Placeholder
            // tables (no axes/outputs in the calibration) are skipped entirely —
            // generated fake data used to stomp the name-based sensor simulations
            // every tick (e.g. a fabricated CLT curve overwriting the coolant
            // temperature model). Runtime SetTable writes still register data.
            if (table.Input0 is not { Count: > 0 }
                && table.Input1 is not { Count: > 0 }
                && table.Output is not { Count: > 0 })
                continue;

            _tableInput0[table.Id] = [.. table.Input0!];
            _tableInput1[table.Id] = table.Input1 is { Count: > 0 } ? [.. table.Input1] : [];
            _tableOutputs[table.Id] = [.. table.Output!];
            _tableEnabled[table.Id] = table.Enabled;
        }

        foreach (var driver in Calibration.Drivers)
        {
            var configs = new float[driver.NumberOfConfigs];
            for (var i = 0; i < driver.NumberOfConfigs && i < driver.Configs.Count; i++)
            {
                var param = driver.Configs[i];
                configs[i] = param.Options is { Count: > 0 } ? param.Options[0].Id : param.Value;
            }
            _driverConfigs[driver.Id] = configs;
        }

        foreach (var link in Calibration.DataLinks)
        {
            _dataLinkValues[link.Id] = 0f;
            _dataLinkReportingTypes[link.Id] = InferReportingType(link);
        }
    }

    private static byte InferReportingType(DataLinkDefinition link)
    {
        return link.MeasureUnit switch
        {
            "bool" => 5,     // Bool1B
            "int8" => 3,     // Int1B
            "uint8" => 4,    // Uint1B
            "int16" => 1,    // Int2B
            "uint16" => 2,   // Uint2B
            _ => 0,          // Float4B (default)
        };
    }

    public bool TryGetTableOutput(ushort id, out float[]? values) => _tableOutputs.TryGetValue(id, out values);
    public bool TryGetTableInput0(ushort id, out float[]? values) => _tableInput0.TryGetValue(id, out values);
    public bool TryGetTableInput1(ushort id, out float[]? values) => _tableInput1.TryGetValue(id, out values);
    public bool IsTableEnabled(ushort id) => _tableEnabled.TryGetValue(id, out var enabled) && enabled;

    public void SetTableOutput(ushort id, float[] values) => _tableOutputs[id] = values;
    public void SetTableInput0(ushort id, float[] values) => _tableInput0[id] = values;
    public void SetTableInput1(ushort id, float[] values) => _tableInput1[id] = values;
    public void SetTableEnabled(ushort id, bool enabled) => _tableEnabled[id] = enabled;

    public bool TryGetDriverConfigs(ushort id, out float[]? configs) => _driverConfigs.TryGetValue(id, out configs);
    public void SetDriverConfigs(ushort id, float[] configs) => _driverConfigs[id] = configs;

    public float GetDataLinkValue(ushort id) => _dataLinkValues.TryGetValue(id, out var val) ? val : 0f;
    public void SetDataLinkValue(ushort id, float value) => _dataLinkValues[id] = value;
    public byte GetDataLinkReportingType(ushort id) => _dataLinkReportingTypes.TryGetValue(id, out var t) ? t : (byte)0;
    public bool HasDataLink(ushort id) => _dataLinkValues.ContainsKey(id);

    /// <summary>
    /// Bilinear (2D) or linear (1D) interpolation of a table at a given operating point.
    /// Returns null if table data is missing.
    /// </summary>
    public float? InterpolateTable(ushort tableId, float input0Value, float input1Value)
    {
        if (!_tableOutputs.TryGetValue(tableId, out var output) ||
            !_tableInput0.TryGetValue(tableId, out var axis0) ||
            !_tableInput1.TryGetValue(tableId, out var axis1))
            return null;

        var cols = axis0.Length;
        var rows = axis1.Length;

        if (cols == 0 || rows == 0)
            return null;

        // 1D table (single row)
        if (rows == 1)
        {
            return Interpolate1D(axis0, output, input0Value);
        }

        // 2D table — bilinear interpolation
        return Interpolate2D(axis0, axis1, output, cols, rows, input0Value, input1Value);
    }

    private static float Interpolate1D(float[] axis, float[] output, float value)
    {
        var cols = axis.Length;

        // Clamp to axis bounds
        if (value <= axis[0])
            return output[0];
        if (value >= axis[cols - 1])
            return output[cols - 1];

        // Find segment
        for (var i = 0; i < cols - 1; i++)
        {
            if (value >= axis[i] && value <= axis[i + 1])
            {
                var t = (value - axis[i]) / (axis[i + 1] - axis[i]);
                return output[i] + t * (output[i + 1] - output[i]);
            }
        }

        return output[cols - 1];
    }

    private static float Interpolate2D(float[] axis0, float[] axis1, float[] output,
        int cols, int rows, float x, float y)
    {
        // Clamp x to axis0 bounds
        if (x <= axis0[0])
            x = axis0[0];
        else if (x >= axis0[cols - 1])
            x = axis0[cols - 1];

        // Clamp y to axis1 bounds
        if (y <= axis1[0])
            y = axis1[0];
        else if (y >= axis1[rows - 1])
            y = axis1[rows - 1];

        // Find x segment (column)
        var ci = 0;
        for (var i = 0; i < cols - 1; i++)
        {
            if (x >= axis0[i] && x <= axis0[i + 1])
            {
                ci = i;
                break;
            }
        }

        // Find y segment (row)
        var ri = 0;
        for (var i = 0; i < rows - 1; i++)
        {
            if (y >= axis1[i] && y <= axis1[i + 1])
            {
                ri = i;
                break;
            }
        }

        // Bilinear interpolation
        var tx = cols > 1 ? (x - axis0[ci]) / (axis0[ci + 1] - axis0[ci]) : 0f;
        var ty = rows > 1 ? (y - axis1[ri]) / (axis1[ri + 1] - axis1[ri]) : 0f;

        var v00 = output[ri * cols + ci];
        var v10 = output[ri * cols + ci + 1];
        var v01 = output[(ri + 1) * cols + ci];
        var v11 = output[(ri + 1) * cols + ci + 1];

        var top = v00 + tx * (v10 - v00);
        var bot = v01 + tx * (v11 - v01);
        return top + ty * (bot - top);
    }
}
