using System.Globalization;
using System.Xml.Linq;
using ME221.Data.Models;

namespace ME221.Data.Infrastructure;

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
        var elem = new XElement("DataLinkModel",
            new XElement("id", dl.Id),
            new XElement("name", dl.Name),
            new XElement("category", dl.Category),
            new XElement("DefaultLocked", false),
            new XElement("BoardSpecific", false),
            new XElement("ViewInTree", dl.ViewInTree),
            new XElement("DataKey", dl.DataKey ?? ""),
            new XElement("measureUnit", dl.MeasureUnit),
            new XElement("StandardLogging", dl.StandardLogging),
            new XElement("DataTypeSet", dl.DataTypeSet.ToString()),
            new XElement("MinValue", FormatFloat(dl.MinValue)),
            new XElement("MaxValue", FormatFloat(dl.MaxValue)));

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

        elem.Add(SerializeMeasurementUnits(dl.MeasurementUnitTypes));

        if (dl.Feedbacks is { Count: > 0 })
        {
            var fbContainer = new XElement("Feedbacks");
            foreach (var fb in dl.Feedbacks)
            {
                fbContainer.Add(SerializeFeedback(fb));
            }
            elem.Add(fbContainer);
        }

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
        return new XElement("TableModel",
            new XElement("id", table.Id),
            new XElement("name", table.Name),
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
            new XElement("output_name", table.OutputName),
            new XElement("input_0", SerializeFloatArray(table.Input0)),
            new XElement("input_1", SerializeFloatArray(table.Input1)),
            new XElement("output", SerializeFloatArray(table.Output)),
            new XElement("incVal", FormatFloat(table.IncrementValue)),
            new XElement("defaultValue", table.DefaultValue.HasValue ? FormatFloat(table.DefaultValue.Value) : ""));
    }

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
        var elem = new XElement("DriverModel",
            new XElement("id", driver.Id),
            new XElement("name", driver.Name),
            new XElement("category", driver.Category),
            new XElement("ViewInTree", driver.ViewInTree),
            new XElement("ConfigValidation", "None"),
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

        elem.Add(new XElement("OutputValidation", "None"));
        elem.Add(new XElement("numberOfOutputs", driver.NumberOfOutputs));
        elem.Add(SerializeUshortList("outputLinkIds", driver.OutputLinkIds));
        elem.Add(new XElement("editableOutputs", driver.EditableOutputs));
        elem.Add(SerializeStringList("outputNames", driver.OutputNames));
        elem.Add(new XElement("InputValidation", "None"));
        elem.Add(new XElement("editableInputs", driver.EditableInputs));
        elem.Add(SerializeStringList("inputNames", driver.InputNames));
        elem.Add(new XElement("numberOfInputs", driver.NumberOfInputs));
        elem.Add(SerializeUshortList("inputLinkIds", driver.InputLinkIds));

        return elem;
    }

    private static XElement SerializeDriverParam(DriverParamDefinition param)
    {
        var elem = new XElement("DriverModelParam",
            new XElement("name", param.Name),
            new XElement("DisplayName", param.DisplayName),
            new XElement("SectionName", param.SectionName),
            new XElement("type", param.ParamType),
            new XElement("readOnly", param.ReadOnly),
            new XElement("RequiresReset", param.RequiresReset),
            new XElement("ToolTipText", param.ToolTipText),
            new XElement("value", FormatFloat(param.Value)),
            new XElement("Min", FormatFloat(param.Min)),
            new XElement("Max", FormatFloat(param.Max)),
            new XElement("CheckRange", param.CheckRange));

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
            var vc = new XElement("ViewConstraint",
                new XElement("ParamIndex", param.ViewConstraint.ParamIndex),
                new XElement("AcceptedValues", string.Join(" ",
                    param.ViewConstraint.AcceptedValues.Select(FormatFloat))));
            elem.Add(vc);
        }

        return elem;
    }

    private static XElement SerializeUshortList(string name, List<ushort> values)
    {
        return new XElement(name,
            string.Join(" ", values.Select(v => v.ToString(FloatCulture))));
    }

    private static XElement SerializeStringList(string name, List<string> values)
    {
        return new XElement(name,
            string.Join(" ", values));
    }

    private static string SerializeFloatArray(List<float>? values)
    {
        if (values is null || values.Count == 0) return "";
        return string.Join(" ", values.Select(FormatFloat));
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("G", FloatCulture);
    }
}
