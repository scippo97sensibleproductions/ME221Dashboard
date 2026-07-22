using System.Globalization;
using System.Xml.Linq;
using ME221.Data.Models;

namespace ME221.Data.Infrastructure;

public static class DefXmlParser
{
    private static readonly CultureInfo FloatCulture = CultureInfo.InvariantCulture;

    public static CalibrationData Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidDataException("Missing root <ecu> element");

        var result = new CalibrationData
        {
            Metadata = ParseMetadata(root),
            DataLinks = [.. ParseDataLinks(root)],
            Tables = [.. ParseTables(root)],
            Drivers = [.. ParseDrivers(root)],
        };

        return result;
    }

    private static DeviceInfoMetadata ParseMetadata(XElement root)
    {
        var info = root.Element("DeviceDataInformationModel");
        return new DeviceInfoMetadata
        {
            ProductName = info?.Element("ProductName")?.Value ?? "",
            ModelName = info?.Element("ModelName")?.Value ?? "",
            Version = info?.Element("Version")?.Value ?? "",
        };
    }

    private static IEnumerable<DataLinkDefinition> ParseDataLinks(XElement root)
    {
        var links = root.Element("links");
        if (links is null) yield break;

        foreach (var elem in links.Elements("DataLinkModel"))
        {
            yield return new DataLinkDefinition
            {
                Id = (ushort)(int)elem.Element("id")!,
                Name = (string?)elem.Element("name") ?? "",
                Category = (string?)elem.Element("category") ?? "",
                ViewInTree = (bool?)elem.Element("ViewInTree") ?? false,
                StandardLogging = (bool?)elem.Element("StandardLogging") ?? false,
                MeasureUnit = ParseMeasureUnit(elem),
                MeasurementUnitTypes = MeasurementUnitConverter.ParseUnitTypes(elem.Element("MeasurementUnitTypes")),
                DataTypeSet = ParseDataType(elem),
                MinValue = ParseFloat(elem.Element("MinValue")) ?? 0f,
                MaxValue = ParseFloat(elem.Element("MaxValue")) ?? 0f,
                DataKey = (string?)elem.Element("DataKey"),
                TextValues = [.. ParseTextValues(elem)],
                Feedbacks = [.. ParseFeedbacks(elem)],
            };
        }
    }

    private static DataType ParseDataType(XElement elem)
    {
        var dataTypeElement = elem.Element("DataTypeSet");
        if (dataTypeElement is null) return DataType.Normal;

        return dataTypeElement.Value switch
        {
            "TrimModPercent" => DataType.TrimModPercent,
            "Percent" => DataType.Percent,
            "ADCRaw" => DataType.ADCRaw,
            _ => DataType.Normal,
        };
    }

    private static IEnumerable<TextValueMapping> ParseTextValues(XElement parent)
    {
        var textValues = parent.Element("TextValues");
        if (textValues is null) yield break;

        foreach (var tv in textValues.Elements("TextValueModel"))
        {
            yield return new TextValueMapping
            {
                Value = (float)tv.Element("value")!,
                Text = (string?)tv.Element("text") ?? "",
            };
        }
    }

    private static IEnumerable<DataLinkFeedback> ParseFeedbacks(XElement parent)
    {
        var feedbacks = parent.Element("Feedbacks");
        if (feedbacks is null) yield break;

        foreach (var elem in feedbacks.Elements())
        {
            if (!TryParseFeedbackSeverity(elem.Name.LocalName, out var severity))
                continue;

            var constraints = elem.Element("Constraints");
            float? minValue = null;
            float? maxValue = null;

            if (constraints is not null)
            {
                foreach (var c in constraints.Elements())
                {
                    var valStr = c.Attribute("Value")?.Value;
                    if (string.IsNullOrEmpty(valStr)) continue;

                    // Eq constraints have space-separated array values (e.g. "3 4") — skip, no range equivalent
                    if (c.Name.LocalName == "Eq" || valStr.Contains(' ')) continue;

                    if (!float.TryParse(valStr, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var val))
                        continue;

                    switch (c.Name.LocalName)
                    {
                        case "Gt":
                            minValue = val;
                            break;
                        case "GtEq":
                            minValue = minValue.HasValue ? Math.Max(minValue.Value, val) : val;
                            break;
                        case "Lt":
                            maxValue = val;
                            break;
                        case "LtEq":
                            maxValue = maxValue.HasValue ? Math.Min(maxValue.Value, val) : val;
                            break;
                    }
                }
            }

            var flashing = (bool?)elem.Attribute("Flashing") ?? false;

            yield return new DataLinkFeedback
            {
                Severity = severity,
                MinValue = minValue,
                MaxValue = maxValue,
                Flashing = flashing,
            };
        }
    }

    private static bool TryParseFeedbackSeverity(string name, out DataLinkFeedbackSeverity severity)
    {
        switch (name)
        {
            case "Ok":
                severity = DataLinkFeedbackSeverity.Ok;
                return true;
            case "Warning":
                severity = DataLinkFeedbackSeverity.Warning;
                return true;
            case "Alarm":
                severity = DataLinkFeedbackSeverity.Alarm;
                return true;
            default:
                severity = default;
                return false;
        }
    }

    private static IEnumerable<TableDefinition> ParseTables(XElement root)
    {
        var tables = root.Element("tables");
        if (tables is null) yield break;

        foreach (var elem in tables.Elements("TableModel"))
        {
            var tableType = (string?)elem.Element("type") ?? "T16x16";

            yield return new TableDefinition
            {
                Id = (ushort)(int)elem.Element("id")!,
                Name = (string?)elem.Element("name") ?? "",
                Category = (string?)elem.Element("category") ?? "",
                ViewInTree = (bool?)elem.Element("ViewInTree") ?? false,
                Enabled = (bool?)elem.Element("enabled") ?? false,
                TableType = tableType,
                Cols = (ushort)(int)elem.Element("cols")!,
                Rows = (ushort)(int)elem.Element("rows")!,
                Input0LinkId = (ushort)(int)elem.Element("input_0_LinkId")!,
                Input1LinkId = (ushort?)(int?)elem.Element("input_1_LinkId") ?? 0,
                OutputLinkId = (ushort)(int)elem.Element("output_LinkId")!,
                Input0Name = (string?)elem.Element("input_0_name") ?? "",
                Input1Name = (string?)elem.Element("input_1_name") ?? "",
                OutputName = (string?)elem.Element("output_name") ?? "",
                IncrementValue = ParseFloat(elem.Element("incVal")) ?? 0.1f,
                DefaultValue = ParseFloat(elem.Element("defaultValue")),
                Input0 = ParseFloatArray(elem.Element("input_0")),
                Input1 = ParseFloatArray(elem.Element("input_1")),
                Output = ParseFloatArray(elem.Element("output")),
            };
        }
    }

    private static float? ParseFloat(XElement? element)
    {
        if (element is null) return null;
        var text = element.Value;
        if (string.IsNullOrWhiteSpace(text)) return null;
        return float.TryParse(text, NumberStyles.Float, FloatCulture, out var v) ? v : null;
    }

    private static List<float>? ParseFloatArray(XElement? element)
    {
        if (element is null) return null;

        // .mecal format uses <float>value</float> child elements
        var floatElements = element.Elements("float");
        if (floatElements.Any())
        {
            return [.. floatElements.Select(e =>
                float.TryParse(e.Value, NumberStyles.Float, FloatCulture, out var v) ? v : 0f)];
        }

        // Fallback: DEF XML uses space-separated text content
        var text = element.Value;
        if (string.IsNullOrWhiteSpace(text)) return null;

        return [.. text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => float.TryParse(s, NumberStyles.Float, FloatCulture, out var v) ? v : 0f)];
    }

    private static IEnumerable<DriverDefinition> ParseDrivers(XElement root)
    {
        var drivers = root.Element("drivers");
        if (drivers is null) yield break;

        foreach (var elem in drivers.Elements("DriverModel"))
        {
            var configParams = elem.Element("configParams");

            yield return new DriverDefinition
            {
                Id = (ushort)(int)elem.Element("id")!,
                Name = (string?)elem.Element("name") ?? "",
                Category = (string?)elem.Element("category") ?? "",
                ViewInTree = (bool?)elem.Element("ViewInTree") ?? false,
                NumberOfConfigs = (int?)elem.Element("numberOfConfigs") ?? 0,
                Configs = configParams is not null
                    ? [.. configParams.Elements("DriverModelParam").Select(ParseDriverParam)]
                    : [],
                NumberOfOutputs = (int?)elem.Element("numberOfOutputs") ?? 0,
                OutputLinkIds = ParseUshortList(elem.Element("outputLinkIds")),
                EditableOutputs = (bool?)elem.Element("editableOutputs") ?? true,
                OutputNames = ParseStringList(elem.Element("outputNames")),
                NumberOfInputs = (int?)elem.Element("numberOfInputs") ?? 0,
                InputLinkIds = ParseUshortList(elem.Element("inputLinkIds")),
                EditableInputs = (bool?)elem.Element("editableInputs") ?? true,
                InputNames = ParseStringList(elem.Element("inputNames")),
            };
        }
    }

    private static DriverParamDefinition ParseDriverParam(XElement elem)
    {
        // MEITE uses lowercase <min>/<max>, older format used <Min>/<Max>
        var minVal = (float?)elem.Element("min") ?? (float?)elem.Element("Min") ?? 0f;
        var maxVal = (float?)elem.Element("max") ?? (float?)elem.Element("Max") ?? 0f;

        return new DriverParamDefinition
        {
            Name = (string?)elem.Element("name") ?? "",
            DisplayName = (string?)elem.Element("DisplayName") ?? "",
            SectionName = (string?)elem.Element("SectionName") ?? "",
            ParamType = (string?)elem.Element("type") ?? "InputBox",
            ReadOnly = (bool?)elem.Element("readOnly") ?? false,
            RequiresReset = (bool?)elem.Element("RequiresReset") ?? false,
            Value = (float?)elem.Element("value") ?? 0f,
            Min = minVal,
            Max = maxVal,
            CheckRange = (bool?)elem.Element("CheckRange") ?? false,
            ToolTipText = (string?)elem.Element("ToolTipText") ?? "",
            Options = [.. ParseComboOptions(elem)],
            ViewConstraint = ParseViewConstraint(elem.Element("ViewConstraint")),
        };
    }

    private static IEnumerable<ComboOption> ParseComboOptions(XElement parent)
    {
        var options = parent.Element("options");
        if (options is null) yield break;

        foreach (var opt in options.Elements("comboBoxOption"))
        {
            yield return new ComboOption
            {
                Id = (ushort)(int)opt.Element("id")!,
                Name = (string?)opt.Element("name") ?? "",
            };
        }
    }

    private static ViewConstraint? ParseViewConstraint(XElement? element)
    {
        if (element is null) return null;

        var paramIndex = (int?)element.Element("ParamIndex");
        var acceptedValuesElement = element.Element("AcceptedValues");
        if (paramIndex is null || acceptedValuesElement is null) return null;

        // MEITE format uses <float>child elements, older format uses space-separated text
        float[] values;
        var floatElements = acceptedValuesElement.Elements("float");
        if (floatElements.Any())
        {
            values = [.. floatElements.Select(e =>
                float.TryParse(e.Value, NumberStyles.Float, FloatCulture, out var v) ? v : 0f)];
        }
        else
        {
            values = acceptedValuesElement.Value
                .Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => float.TryParse(s, NumberStyles.Float, FloatCulture, out var v) ? v : 0f)
                .ToArray();
        }

        if (values.Length == 0) return null;

        return new ViewConstraint
        {
            ParamIndex = paramIndex.Value,
            AcceptedValues = values,
        };
    }

    private static List<string> ParseStringList(XElement? element)
    {
        if (element is null) return [];

        // MEITE format uses <string>child elements, older format uses space-separated text
        var stringElements = element.Elements("string");
        if (stringElements.Any())
        {
            return [.. stringElements.Select(e => e.Value)];
        }

        var text = element.Value;
        if (string.IsNullOrWhiteSpace(text)) return [];

        return [.. text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)];
    }

    private static List<ushort> ParseUshortList(XElement? element)
    {
        if (element is null) return [];

        // MEITE format uses <unsignedShort>child elements, older format uses space-separated text
        var ushortElements = element.Elements("unsignedShort");
        if (ushortElements.Any())
        {
            return [.. ushortElements.Select(e =>
                ushort.TryParse(e.Value, out var v) ? v : (ushort)0)];
        }

        var text = element.Value;
        if (string.IsNullOrWhiteSpace(text)) return [];

        return [.. text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => ushort.TryParse(s, out var v) ? v : (ushort)0)];
    }

    private static string ParseMeasureUnit(XElement elem)
    {
        // Prefer explicit <measureUnit> if present
        var explicitUnit = (string?)elem.Element("measureUnit");
        if (!string.IsNullOrEmpty(explicitUnit))
            return explicitUnit;

        // Fall back to <MeasurementUnitTypes> — take the first child element's name
        var unitTypes = elem.Element("MeasurementUnitTypes");

        var firstType = unitTypes?.Elements().FirstOrDefault();
        if (firstType is null) return "";

        return firstType.Name.LocalName switch
        {
            "Celsius" => "°C",
            "Fahrenheit" => "°F",
            "Volt" => "V",
            "Ohm" => "Ω",
            "Percent" => "%",
            "Rpm" => "RPM",
            "Deg" => "°",
            "Ms" => "ms",
            "Bar" => "bar",
            "Kpa" => "kPa",
            "Psi" => "PSI",
            "Raw" => "",
            _ => firstType.Name.LocalName,
        };
    }
}
