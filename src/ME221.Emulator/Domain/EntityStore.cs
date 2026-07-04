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
        var linkLookup = Calibration.DataLinks.ToDictionary(dl => dl.Id);

        foreach (var table in Calibration.Tables)
        {
            _tableInput0[table.Id] = GenerateAxisValues(table.Cols, table.Input0LinkId, table.Input0Name, linkLookup);
            _tableInput1[table.Id] = GenerateAxisValues(table.Rows, table.Input1LinkId, table.Input1Name, linkLookup);
            _tableOutputs[table.Id] = GenerateOutputValues(table,
                _tableInput0[table.Id], _tableInput1[table.Id]);
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

    private static float[] GenerateAxisValues(int size, ushort linkId, string name,
        Dictionary<ushort, DataLinkDefinition> linkLookup)
    {
        float min, max, step;

        if (linkLookup.TryGetValue(linkId, out var link))
        {
            (min, max) = link.MeasureUnit switch
            {
                "%" or "Percent" => (0f, 100f),
                "V" or "Volt" => (8f, 16f),
                "\u00B0C" or "C" or "\u00B0" or "deg" or "degC" => (-20f, 120f),
                "\u00B0F" or "F" or "degF" => (-20f, 250f),
                "ms" => (0f, 20f),
                "kPa" or "bar" or "PSI" or "psi" when name.Contains("Boost", StringComparison.OrdinalIgnoreCase) => (0f, 300f),
                "kPa" or "bar" or "PSI" or "psi" => (0f, 250f),
                "rpm" or "RPM" => (0f, 10000f),
                _ => (0f, 500f),
            };
        }
        else
        {
            (min, max) = name switch
            {
                "RPM" => (0f, 10000f),
                "Pri. Load" or "Sec.. Load" or "Primary Load" or "Secondary Load" or "Load" => (0f, 100f),
                "Batt. Voltage" or "Battery Voltage" => (8f, 16f),
                "CLT" or "Coolant Temp." or "IAT" or "Intake Air Temp." => (-20f, 120f),
                _ => (0f, Math.Max(size * 10f, 100f)),
            };
        }

        var result = new float[size];
        if (size == 1)
        {
            result[0] = (min + max) / 2f;
        }
        else
        {
            step = (max - min) / (size - 1);
            for (var i = 0; i < size; i++)
                result[i] = min + i * step;
        }
        return result;
    }

    private static float[] GenerateOutputValues(TableDefinition table, float[] input0, float[] input1)
    {
        var rows = table.Rows;
        var cols = table.Cols;
        var output = new float[rows * cols];

        if (table.DefaultValue.HasValue && table.DefaultValue.Value != 0f)
        {
            Array.Fill(output, table.DefaultValue.Value);
            return output;
        }

        var (baseVal, range) = GetOutputRange(table);

        if (rows == 1)
        {
            for (var c = 0; c < cols; c++)
            {
                var nx = cols > 1 ? c / (float)(cols - 1) : 0.5f;
                output[c] = MathF.Round((baseVal + range * Curve1D(nx)) * 10f) / 10f;
            }
        }
        else
        {
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var nx = cols > 1 ? c / (float)(cols - 1) : 0.5f;
                    var ny = rows > 1 ? r / (float)(rows - 1) : 0.5f;
                    output[r * cols + c] = MathF.Round((baseVal + range * Surface2D(nx, ny, table)) * 10f) / 10f;
                }
            }
        }

        return output;
    }

    private static (float baseVal, float range) GetOutputRange(TableDefinition table)
    {
        var name = table.Name.AsSpan().Trim().ToString();
        var outName = table.OutputName;

        if (name.Contains("Dwell", StringComparison.OrdinalIgnoreCase))
            return (0.5f, 5f);

        if (name.Contains("Adv", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Angle", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Add", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Trim", StringComparison.OrdinalIgnoreCase))
                return (-3f, 6f);
            return (5f, 25f);
        }

        if (name.Contains("VE", StringComparison.OrdinalIgnoreCase))
            return (30f, 60f);

        if (name.Contains("AFR", StringComparison.OrdinalIgnoreCase))
            return (12f, 4f);

        if (name.Contains("Dead Time", StringComparison.OrdinalIgnoreCase))
            return (0.5f, 2f);

        if (name.Contains("Duty", StringComparison.OrdinalIgnoreCase))
            return (10f, 70f);

        if (name.Contains("Perc", StringComparison.OrdinalIgnoreCase) ||
            outName.Contains("Perc", StringComparison.OrdinalIgnoreCase))
            return (10f, 70f);

        if (name.Contains("CLT", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Temp", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("IAT", StringComparison.OrdinalIgnoreCase))
            return (0f, 10f);

        if (name.Contains("Target", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Base", StringComparison.OrdinalIgnoreCase))
            return (20f, 80f);

        if (name.Contains("Trim", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Correction", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Add", StringComparison.OrdinalIgnoreCase))
            return (-5f, 10f);

        return (0f, 100f);
    }

    private static float Curve1D(float nx)
    {
        return MathF.Sin(nx * MathF.PI) * 0.8f + 0.2f * (1f - nx);
    }

    private static float Surface2D(float nx, float ny, TableDefinition table)
    {
        var name = table.Name.AsSpan().Trim().ToString();

        float cx, cy, sx, sy;

        if (name.Contains("Dwell", StringComparison.OrdinalIgnoreCase))
        {
            cx = 0f; cy = 0.8f; sx = 0.5f; sy = 0.5f;
        }
        else if (name.Contains("Adv", StringComparison.OrdinalIgnoreCase) &&
                 !name.Contains("Add", StringComparison.OrdinalIgnoreCase) &&
                 !name.Contains("Trim", StringComparison.OrdinalIgnoreCase))
        {
            cx = 0.6f; cy = 0.4f; sx = 0.35f; sy = 0.35f;
        }
        else if (name.Contains("Angle", StringComparison.OrdinalIgnoreCase))
        {
            cx = 0.7f; cy = 0.5f; sx = 0.3f; sy = 0.4f;
        }
        else if (name.Contains("VE", StringComparison.OrdinalIgnoreCase))
        {
            cx = 0.5f; cy = 0.8f; sx = 0.4f; sy = 0.25f;
        }
        else if (name.Contains("AFR", StringComparison.OrdinalIgnoreCase))
        {
            return 0.7f - 0.5f * ny;
        }
        else
        {
            cx = 0.5f; cy = 0.5f; sx = 0.4f; sy = 0.4f;
        }

        var dx = (nx - cx) / sx;
        var dy = (ny - cy) / sy;
        var hill = MathF.Exp(-(dx * dx + dy * dy));
        hill += 0.15f * (1f - ny);
        return Math.Clamp(hill, 0f, 1f);
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
