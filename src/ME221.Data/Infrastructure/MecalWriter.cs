using System.Globalization;
using System.Xml.Linq;
using ME221.Data.Models;

namespace ME221.Data.Infrastructure;

/// <summary>
/// Serializes CalibrationData to .mecal XML format compatible with MEITE 3.0.
/// Element order, names, and structure match MEITE's XmlSerializer output exactly.
/// Based on MEITE source: DataModel, DataLinkModel, TableModel, DriverModel, DriverModelParam.
/// </summary>
public static class MecalWriter
{
    private static readonly CultureInfo FloatCulture = CultureInfo.InvariantCulture;

    public static string Serialize(CalibrationData calibration)
    {
        var root = new XElement("ecu");

        root.Add(SerializeMetadata(calibration.Metadata));
        root.Add(SerializeNotes());
        root.Add(SerializeDataLinks(calibration.DataLinks));
        root.Add(SerializeDrivers(calibration.Drivers));
        root.Add(SerializeTables(calibration.Tables));

        return root.ToString(SaveOptions.None);
    }

    private static XElement SerializeMetadata(DeviceInfoMetadata metadata)
    {
        return new XElement("DeviceDataInformationModel",
            new XElement("ProductName", metadata.ProductName),
            new XElement("ModelName", metadata.ModelName),
            new XElement("Version", metadata.Version));
    }

    private static XElement SerializeNotes()
    {
        return new XElement("CalibrationNotes",
            new XElement("Text", ""));
    }

    // ─── DataLinks ────────────────────────────────────────────────────────
    // MEITE order: DefaultLocked, id, name, ViewInTree, BoardSpecific, category,
    //   [DataKey], [Feedbacks], [MeasurementUnitTypes], [measureUnit],
    //   StandardLogging, [TextValues], DataTypeSet, MinValue, MaxValue

    private static XElement SerializeDataLinks(List<DataLinkDefinition> dataLinks)
    {
        var container = new XElement("links");
        foreach (var dl in dataLinks)
        {
            container.Add(SerializeDataLink(dl));
        }
        return container;
    }

    private static XElement SerializeDataLink(DataLinkDefinition dl)
    {
        // DataModel base: DefaultLocked, id, name, ViewInTree, BoardSpecific, category
        var elem = new XElement("DataLinkModel",
            new XElement("DefaultLocked", false),
            new XElement("id", dl.Id),
            new XElement("name", dl.Name),
            new XElement("ViewInTree", dl.ViewInTree),
            new XElement("BoardSpecific", false),
            new XElement("category", dl.Category));

        if (!string.IsNullOrEmpty(dl.DataKey))
            elem.Add(new XElement("DataKey", dl.DataKey));

        if (dl.Feedbacks is { Count: > 0 })
        {
            var fbContainer = new XElement("Feedbacks");
            foreach (var fb in dl.Feedbacks)
            {
                fbContainer.Add(SerializeFeedback(fb));
            }
            elem.Add(fbContainer);
        }

        elem.Add(SerializeMeasurementUnits(dl.MeasurementUnitTypes));

        if (!string.IsNullOrEmpty(dl.MeasureUnit))
            elem.Add(new XElement("measureUnit", dl.MeasureUnit));

        elem.Add(new XElement("StandardLogging", dl.StandardLogging));

        if (dl.TextValues is { Count: > 0 })
        {
            var tvContainer = new XElement("TextValues");
            foreach (var tv in dl.TextValues)
            {
                tvContainer.Add(new XElement("TextValueModel",
                    new XElement("value", FormatFloat(tv.Value)),
                    new XElement("text", tv.Text)));
            }
            elem.Add(tvContainer);
        }

        elem.Add(new XElement("DataTypeSet", dl.DataTypeSet.ToString()));
        elem.Add(new XElement("MinValue", FormatFloat(dl.MinValue)));
        elem.Add(new XElement("MaxValue", FormatFloat(dl.MaxValue)));

        return elem;
    }

    private static XElement SerializeMeasurementUnits(MeasurementUnitType unitTypes)
    {
        var container = new XElement("MeasurementUnitTypes");
        if (unitTypes.HasFlag(MeasurementUnitType.Raw)) container.Add(new XElement("Raw"));
        if (unitTypes.HasFlag(MeasurementUnitType.Ohm)) container.Add(new XElement("Ohm"));
        if (unitTypes.HasFlag(MeasurementUnitType.Volt)) container.Add(new XElement("Volt"));
        if (unitTypes.HasFlag(MeasurementUnitType.KPa)) container.Add(new XElement("KPa"));
        if (unitTypes.HasFlag(MeasurementUnitType.PSI)) container.Add(new XElement("PSI"));
        if (unitTypes.HasFlag(MeasurementUnitType.Celsius)) container.Add(new XElement("Celsius"));
        if (unitTypes.HasFlag(MeasurementUnitType.Fahrenheit)) container.Add(new XElement("Fahrenheit"));
        return container;
    }

    private static XElement SerializeFeedback(DataLinkFeedback fb)
    {
        var severityName = fb.Severity switch
        {
            DataLinkFeedbackSeverity.Ok => "Ok",
            DataLinkFeedbackSeverity.Warning => "Warning",
            DataLinkFeedbackSeverity.Alarm => "Alarm",
            _ => "Default"
        };

        var elem = new XElement(severityName,
            new XAttribute("Flashing", fb.Flashing));

        var constraints = new XElement("Constraints");

        if (fb.MinValue.HasValue)
        {
            constraints.Add(new XElement("GtEq",
                new XAttribute("Value", FormatFloat(fb.MinValue.Value))));
        }
        if (fb.MaxValue.HasValue)
        {
            constraints.Add(new XElement("LtEq",
                new XAttribute("Value", FormatFloat(fb.MaxValue.Value))));
        }

        elem.Add(constraints);
        return elem;
    }

    // ─── Tables ───────────────────────────────────────────────────────────
    // MEITE order: DefaultLocked, id, name, ViewInTree, BoardSpecific, category,
    //   enabled, type, function, cols, rows, input_0_LinkId, input_1_LinkId,
    //   output_LinkId, input_0_name, input_1_name, output_name,
    //   input_0 (float[]), input_1 (float[]), output (float[])

    private static XElement SerializeTables(List<TableDefinition> tables)
    {
        var container = new XElement("tables");
        foreach (var table in tables)
        {
            container.Add(SerializeTable(table));
        }
        return container;
    }

    private static XElement SerializeTable(TableDefinition table)
    {
        // DataModel base: DefaultLocked, id, name, ViewInTree, BoardSpecific, category
        var elem = new XElement("TableModel",
            new XElement("DefaultLocked", false),
            new XElement("id", table.Id),
            new XElement("name", table.Name),
            new XElement("ViewInTree", table.ViewInTree),
            new XElement("BoardSpecific", false),
            new XElement("category", table.Category),
            new XElement("enabled", table.Enabled),
            new XElement("type", table.TableType),
            new XElement("function", "Normal"),
            new XElement("cols", table.Cols),
            new XElement("rows", table.Rows),
            new XElement("input_0_LinkId", table.Input0LinkId),
            new XElement("input_1_LinkId", table.Input1LinkId),
            new XElement("output_LinkId", table.OutputLinkId),
            new XElement("input_0_name", table.Input0Name),
            new XElement("input_1_name", table.Input1Name),
            new XElement("output_name", table.OutputName));

        elem.Add(SerializeFloatArrayElement("input_0", table.Input0));
        elem.Add(SerializeFloatArrayElement("input_1", table.Input1));
        elem.Add(SerializeFloatArrayElement("output", table.Output));

        if (table.IncrementValue != 0.1f)
            elem.Add(new XElement("incVal", FormatFloat(table.IncrementValue)));
        if (table.DefaultValue.HasValue && table.DefaultValue.Value != 0f)
            elem.Add(new XElement("defaultValue", FormatFloat(table.DefaultValue.Value)));

        return elem;
    }

    // ─── Drivers ──────────────────────────────────────────────────────────
    // MEITE order: DefaultLocked, id, name, ViewInTree, BoardSpecific, category,
    //   ConfigValidation, numberOfConfigs, configParams,
    //   OutputValidation, numberOfOutputs, outputLinkIds (ushort[]),
    //   editableOutputs, outputNames (string[]),
    //   InputValidation, editableInputs, inputNames (string[]),
    //   numberOfInputs, inputLinkIds (ushort[])

    private static XElement SerializeDrivers(List<DriverDefinition> drivers)
    {
        var container = new XElement("drivers");
        foreach (var driver in drivers)
        {
            container.Add(SerializeDriver(driver));
        }
        return container;
    }

    private static XElement SerializeDriver(DriverDefinition driver)
    {
        // DataModel base: DefaultLocked, id, name, ViewInTree, BoardSpecific, category
        var elem = new XElement("DriverModel",
            new XElement("DefaultLocked", false),
            new XElement("id", driver.Id),
            new XElement("name", driver.Name),
            new XElement("ViewInTree", driver.ViewInTree),
            new XElement("BoardSpecific", false),
            new XElement("category", driver.Category),
            new XElement("numberOfConfigs", driver.NumberOfConfigs));

        if (driver.Configs is { Count: > 0 })
        {
            var configContainer = new XElement("configParams");
            foreach (var config in driver.Configs)
            {
                configContainer.Add(SerializeDriverParam(config));
            }
            elem.Add(configContainer);
        }

        elem.Add(new XElement("numberOfOutputs", driver.NumberOfOutputs));
        elem.Add(new XElement("editableOutputs", driver.EditableOutputs));
        elem.Add(SerializeUshortArrayElement("outputLinkIds", driver.OutputLinkIds));
        elem.Add(new XElement("numberOfInputs", driver.NumberOfInputs));
        elem.Add(new XElement("editableInputs", driver.EditableInputs));
        elem.Add(SerializeUshortArrayElement("inputLinkIds", driver.InputLinkIds));

        return elem;
    }

    // ─── DriverModelParam ─────────────────────────────────────────────────
    // MEITE order: name, [DisplayName], [SectionName], type, readOnly,
    //   [RequiresReset], [ToolTipText], value, min, max, [CheckRange],
    //   [MeasurementUnitTypes], [options], [ViewConstraint]

    private static XElement SerializeDriverParam(DriverParamDefinition param)
    {
        var elem = new XElement("DriverModelParam",
            new XElement("name", param.Name),
            new XElement("type", param.ParamType),
            new XElement("readOnly", param.ReadOnly),
            new XElement("RequiresReset", param.RequiresReset),
            new XElement("ToolTipText", param.ToolTipText),
            new XElement("value", FormatFloat(param.Value)),
            new XElement("min", FormatFloat(param.Min)),
            new XElement("max", FormatFloat(param.Max)),
            new XElement("CheckRange", param.CheckRange));

        if (!string.IsNullOrEmpty(param.SectionName))
            elem.Element("name")!.AddAfterSelf(new XElement("SectionName", param.SectionName));

        elem.Add(SerializeMeasurementUnits(MeasurementUnitType.Unknown));

        if (param.Options is { Count: > 0 })
        {
            var optContainer = new XElement("options");
            foreach (var opt in param.Options)
            {
                optContainer.Add(new XElement("comboBoxOption",
                    new XElement("id", opt.Id),
                    new XElement("name", opt.Name)));
            }
            elem.Add(optContainer);
        }

        if (param.ViewConstraint is not null)
        {
            var acceptedValuesContainer = new XElement("AcceptedValues");
            foreach (var v in param.ViewConstraint.AcceptedValues)
            {
                acceptedValuesContainer.Add(new XElement("float", FormatFloat(v)));
            }

            var vc = new XElement("ViewConstraint",
                new XElement("ParamIndex", param.ViewConstraint.ParamIndex),
                acceptedValuesContainer);
            elem.Add(vc);
        }

        return elem;
    }

    // ─── Array serialization helpers ──────────────────────────────────────

    private static XElement SerializeFloatArrayElement(string name, List<float>? values)
    {
        var container = new XElement(name);
        if (values is { Count: > 0 })
        {
            foreach (var v in values)
            {
                container.Add(new XElement("float", FormatFloat(v)));
            }
        }
        return container;
    }

    private static XElement SerializeUshortArrayElement(string name, List<ushort> values)
    {
        var container = new XElement(name);
        if (values is { Count: > 0 })
        {
            foreach (var v in values)
            {
                container.Add(new XElement("unsignedShort", v.ToString(FloatCulture)));
            }
        }
        return container;
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("G", FloatCulture);
    }
}
