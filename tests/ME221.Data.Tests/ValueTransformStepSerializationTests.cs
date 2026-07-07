using System.Text.Json;
using FluentAssertions;
using ME221.Data.Models;
using Xunit;

namespace ME221.Data.Tests;

public class ValueTransformStepSerializationTests
{
    [Fact]
    public void RoundTrip_WithTransformSteps_PreservesAllFields()
    {
        var entry = new GaugeConfigEntry
        {
            Id = 42,
            TransformSteps =
            [
                new ValueTransformStep { Operation = ValueTransformOperation.Multiply, Operand = 0.621371 },
                new ValueTransformStep { Operation = ValueTransformOperation.Add, Operand = -5 },
                new ValueTransformStep { Operation = ValueTransformOperation.Divide, Operand = 2 },
                new ValueTransformStep { Operation = ValueTransformOperation.MinClamp, Operand = 0 },
                new ValueTransformStep { Operation = ValueTransformOperation.MaxClamp, Operand = 100 },
                new ValueTransformStep { Operation = ValueTransformOperation.InvertSign, Operand = 0 },
            ],
            CustomUnitLabel = "mph",
        };

        var json = JsonSerializer.Serialize(entry, CalibrationJsonContext.Default.GaugeConfigEntry);
        var deserialized = JsonSerializer.Deserialize(json, CalibrationJsonContext.Default.GaugeConfigEntry);

        deserialized.Should().NotBeNull();
        deserialized!.TransformSteps.Should().HaveCount(6);
        deserialized.TransformSteps![0].Operation.Should().Be(ValueTransformOperation.Multiply);
        deserialized.TransformSteps[0].Operand.Should().Be(0.621371);
        deserialized.TransformSteps[1].Operation.Should().Be(ValueTransformOperation.Add);
        deserialized.TransformSteps[1].Operand.Should().Be(-5);
        deserialized.TransformSteps[2].Operation.Should().Be(ValueTransformOperation.Divide);
        deserialized.TransformSteps[2].Operand.Should().Be(2);
        deserialized.TransformSteps[3].Operation.Should().Be(ValueTransformOperation.MinClamp);
        deserialized.TransformSteps[3].Operand.Should().Be(0);
        deserialized.TransformSteps[4].Operation.Should().Be(ValueTransformOperation.MaxClamp);
        deserialized.TransformSteps[4].Operand.Should().Be(100);
        deserialized.TransformSteps[5].Operation.Should().Be(ValueTransformOperation.InvertSign);
        deserialized.TransformSteps[5].Operand.Should().Be(0);
        deserialized.CustomUnitLabel.Should().Be("mph");
    }

    [Fact]
    public void RoundTrip_NullTransformSteps_SerializesAsNull()
    {
        var entry = new GaugeConfigEntry
        {
            Id = 1,
            TransformSteps = null,
            CustomUnitLabel = null,
        };

        var json = JsonSerializer.Serialize(entry, CalibrationJsonContext.Default.GaugeConfigEntry);
        var deserialized = JsonSerializer.Deserialize(json, CalibrationJsonContext.Default.GaugeConfigEntry);

        deserialized.Should().NotBeNull();
        deserialized!.TransformSteps.Should().BeNull();
        deserialized.CustomUnitLabel.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_EmptyTransformSteps_PreservesEmptyList()
    {
        var entry = new GaugeConfigEntry
        {
            Id = 1,
            TransformSteps = [],
            CustomUnitLabel = null,
        };

        var json = JsonSerializer.Serialize(entry, CalibrationJsonContext.Default.GaugeConfigEntry);
        var deserialized = JsonSerializer.Deserialize(json, CalibrationJsonContext.Default.GaugeConfigEntry);

        deserialized.Should().NotBeNull();
        deserialized!.TransformSteps.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void RoundTrip_TransformStepsList_PreservesOrder()
    {
        var steps = new List<ValueTransformStep>
        {
            new() { Operation = ValueTransformOperation.InvertSign, Operand = 0 },
            new() { Operation = ValueTransformOperation.Multiply, Operand = 3.14 },
            new() { Operation = ValueTransformOperation.Add, Operand = 42 },
        };

        var entry = new GaugeConfigEntry
        {
            Id = 99,
            TransformSteps = steps,
        };

        var json = JsonSerializer.Serialize(entry, CalibrationJsonContext.Default.GaugeConfigEntry);
        var deserialized = JsonSerializer.Deserialize(json, CalibrationJsonContext.Default.GaugeConfigEntry);

        deserialized!.TransformSteps.Should().HaveCount(3);
        deserialized.TransformSteps![0].Operation.Should().Be(ValueTransformOperation.InvertSign);
        deserialized.TransformSteps[1].Operand.Should().Be(3.14);
        deserialized.TransformSteps[2].Operation.Should().Be(ValueTransformOperation.Add);
        deserialized.TransformSteps[2].Operand.Should().Be(42);
    }

    [Fact]
    public void RoundTrip_ExistingFields_UnaffectedByTransforms()
    {
        var entry = new GaugeConfigEntry
        {
            Id = 7,
            FractionX = 0.25,
            FractionY = 0.75,
            WidthFraction = 0.5,
            HeightFraction = 0.3,
            SweepAngle = 270,
            Scale = 1.5,
            CustomUnitLabel = "psi",
            TransformSteps =
            [
                new ValueTransformStep { Operation = ValueTransformOperation.Multiply, Operand = 0.145038 },
            ],
        };

        var json = JsonSerializer.Serialize(entry, CalibrationJsonContext.Default.GaugeConfigEntry);
        var deserialized = JsonSerializer.Deserialize(json, CalibrationJsonContext.Default.GaugeConfigEntry);

        deserialized!.Id.Should().Be(7);
        deserialized.FractionX.Should().Be(0.25);
        deserialized.FractionY.Should().Be(0.75);
        deserialized.WidthFraction.Should().Be(0.5);
        deserialized.HeightFraction.Should().Be(0.3);
        deserialized.SweepAngle.Should().Be(270);
        deserialized.Scale.Should().Be(1.5);
        deserialized.TransformSteps.Should().HaveCount(1);
        deserialized.CustomUnitLabel.Should().Be("psi");
    }
}
