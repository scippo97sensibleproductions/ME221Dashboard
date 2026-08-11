using System.Text.Json;
using FluentAssertions;
using ME221.Data.Models;
using Xunit;

namespace ME221.Data.Tests;

public class CalibrationJsonSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(CalibrationJsonContext.Default.Options);

    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        return JsonSerializer.Deserialize<T>(json, Options)!;
    }

    [Fact]
    public void CalibrationData_RoundTrip_PreservesAllSections()
    {
        var data = new CalibrationData
        {
            Metadata = new DeviceInfoMetadata { ProductName = "ME221", ModelName = "V7", Version = "3.0" },
            DataLinks = [new DataLinkDefinition { Id = 1, Name = "RPM", MeasureUnit = "RPM" }],
            Tables = [new TableDefinition { Id = 2, TableType = "T16x16", Rows = 16, Cols = 16 }],
            Drivers = [new DriverDefinition { Id = 3, Name = "Boost", NumberOfConfigs = 1 }],
        };

        var result = RoundTrip(data);

        result.Metadata.ProductName.Should().Be("ME221");
        result.DataLinks.Should().ContainSingle(d => d.Id == 1 && d.Name == "RPM");
        result.Tables.Should().ContainSingle(t => t.Id == 2 && t.Rows == 16);
        result.Drivers.Should().ContainSingle(d => d.Id == 3);
    }

    [Fact]
    public void GaugeConfigEntry_RoundTrip_PreservesCustomizationFields()
    {
        var gauge = new GaugeConfigEntry
        {
            Id = 42,
            GridRow = 1,
            GridColumn = 2,
            RowSpan = 2,
            ColumnSpan = 3,
            DisplayType = 4,
            ShapeCategory = 2,
            SweepAngle = 270,
            ArcPosition = 1,
            DigitalStyle = 3,
            FractionX = 0.5,
            FractionY = 0.25,
            WidthFraction = 0.2,
            HeightFraction = 0.3,
            ZIndex = 7,
            ChartLineColor = "#ff0000",
            ChartTimeWindowSec = 45,
            ChartOverlays = [new ChartOverlayEntry { EntityId = 9, Color = "#00ff00", LineWidth = 2, LineStyle = 1 }],
            LinkedEntities = [new LinkedEntityEntry { EntityId = 11, Color = "#0000ff" }],
            TransformSteps = [new ValueTransformStep { Operation = ValueTransformOperation.Multiply, Operand = 1.5 }],
        };

        var result = RoundTrip(gauge);

        result.Id.Should().Be(42);
        result.GridRow.Should().Be(1);
        result.GridColumn.Should().Be(2);
        result.RowSpan.Should().Be(2);
        result.ColumnSpan.Should().Be(3);
        result.ShapeCategory.Should().Be(2);
        result.SweepAngle.Should().Be(270);
        result.ArcPosition.Should().Be(1);
        result.DigitalStyle.Should().Be(3);
        result.FractionX.Should().Be(0.5);
        result.FractionY.Should().Be(0.25);
        result.ZIndex.Should().Be(7);
        result.ChartLineColor.Should().Be("#ff0000");
        result.ChartTimeWindowSec.Should().Be(45);
        result.ChartOverlays.Should().ContainSingle(o => o.EntityId == 9 && o.Color == "#00ff00" && o.LineWidth == 2);
        result.LinkedEntities.Should().ContainSingle(e => e.EntityId == 11);
        result.TransformSteps.Should().ContainSingle(t => t.Operation == ValueTransformOperation.Multiply && t.Operand == 1.5);
    }

    [Fact]
    public void DataLinkDefinition_RoundTrip_PreservesNestedModels()
    {
        var link = new DataLinkDefinition
        {
            Id = 5,
            Name = "Temp",
            DataTypeSet = DataType.Percent,
            MeasurementUnitTypes = MeasurementUnitType.Celsius,
            TextValues = [new TextValueMapping { Value = 0f, Text = "Cold" }],
            Feedbacks = [new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Warning, MinValue = 100, MaxValue = 120, Flashing = true }],
        };

        var result = RoundTrip(link);

        result.DataTypeSet.Should().Be(DataType.Percent);
        result.MeasurementUnitTypes.Should().Be(MeasurementUnitType.Celsius);
        result.TextValues.Should().ContainSingle(t => t.Text == "Cold");
        result.Feedbacks.Should().ContainSingle(f =>
            f.Severity == DataLinkFeedbackSeverity.Warning && f.MinValue == 100f && f.MaxValue == 120f && f.Flashing == true);
    }

    [Fact]
    public void DriverDefinition_RoundTrip_PreservesParamsAndConstraints()
    {
        var driver = new DriverDefinition
        {
            Id = 8,
            Name = "Driver",
            Configs = [new DriverParamDefinition
            {
                Name = "duty",
                Value = 50f,
                Min = 0f,
                Max = 100f,
                Options = [new ComboOption { Id = 1, Name = "On" }],
                ViewConstraint = new ViewConstraint { ParamIndex = 1, AcceptedValues = [5f, 6f] },
            }],
        };

        var result = RoundTrip(driver);

        var param = result.Configs.Single();
        param.Name.Should().Be("duty");
        param.Value.Should().Be(50f);
        param.Options.Should().ContainSingle(o => o.Id == 1 && o.Name == "On");
        param.ViewConstraint.Should().NotBeNull();
        param.ViewConstraint!.ParamIndex.Should().Be(1);
        param.ViewConstraint.AcceptedValues.Should().Equal([5f, 6f]);
    }

    [Fact]
    public void TableDefinition_RoundTrip_PreservesAxisArrays()
    {
        var table = new TableDefinition
        {
            Id = 12,
            TableType = "T16x16",
            Rows = 16,
            Cols = 16,
            Input0 = [0f, 500f, 1000f],
            Input1 = [10f, 20f],
            Output = [0f, 1.5f, -2.5f],
            IncrementValue = 0.5f,
            DefaultValue = 10f,
            Enabled = true,
        };

        var result = RoundTrip(table);

        result.Input0.Should().Equal([0f, 500f, 1000f]);
        result.Input1.Should().Equal([10f, 20f]);
        result.Output.Should().Equal([0f, 1.5f, -2.5f]);
        result.IncrementValue.Should().Be(0.5f);
        result.DefaultValue.Should().Be(10f);
        result.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Serialize_UsesCamelCaseNaming()
    {
        var json = JsonSerializer.Serialize(new DataLinkDefinition { Id = 1, Name = "X" }, Options);

        json.Should().Contain("\"id\"");
        json.Should().Contain("\"name\"");
        json.Should().NotContain("\"Id\"");
    }
}
