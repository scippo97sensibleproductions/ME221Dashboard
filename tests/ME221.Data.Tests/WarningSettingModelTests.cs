using System.Text.Json;
using FluentAssertions;
using ME221.Data.Models;
using Xunit;

namespace ME221.Data.Tests;

public class WarningSettingModelTests
{
    private static DataLinkWarningSetting RoundTrip(DataLinkWarningSetting value)
    {
        var json = JsonSerializer.Serialize(value, CalibrationJsonContext.Default.DataLinkWarningSetting);
        return JsonSerializer.Deserialize(json, CalibrationJsonContext.Default.DataLinkWarningSetting)!;
    }

    [Fact]
    public void DataLinkWarningSetting_RoundTrip_PreservesLevelsAndPoints()
    {
        var setting = new DataLinkWarningSetting
        {
            DataId = 7,
            Enabled = true,
            Name = "Boost",
            Unit = "kPa",
            Category = "Boost",
            Status = WarningSettingStatus.Custom,
            Levels =
            [
                new WarningLevel { Id = "lvl-1", Name = "Soft", Color = "#ffcc00", Autolog = true, Flash = false, Order = 1 },
            ],
            Points =
            [
                new WarningPoint { Id = "pt-1", Value = 1.5f, Direction = "min", LevelId = "lvl-1", Enabled = true },
            ],
        };

        var result = RoundTrip(setting);

        result.DataId.Should().Be(7);
        result.Enabled.Should().BeTrue();
        result.Name.Should().Be("Boost");
        result.Unit.Should().Be("kPa");
        result.Category.Should().Be("Boost");
        result.Status.Should().Be(WarningSettingStatus.Custom);
        result.Levels.Should().ContainSingle();
        result.Levels[0].Id.Should().Be("lvl-1");
        result.Levels[0].Name.Should().Be("Soft");
        result.Levels[0].Color.Should().Be("#ffcc00");
        result.Levels[0].Autolog.Should().BeTrue();
        result.Levels[0].Flash.Should().BeFalse();
        result.Levels[0].Order.Should().Be(1);
        result.Points.Should().ContainSingle();
        result.Points[0].Id.Should().Be("pt-1");
        result.Points[0].Value.Should().Be(1.5f);
        result.Points[0].Direction.Should().Be("min");
        result.Points[0].LevelId.Should().Be("lvl-1");
        result.Points[0].Enabled.Should().BeTrue();
    }

    [Fact]
    public void DataLinkWarningSetting_RoundTrip_PreservesMigratedBoundsMarkerLevelId()
    {
        var setting = new DataLinkWarningSetting
        {
            DataId = 3,
            Levels = [new WarningLevel { Id = "lvl-2", Name = "Hard" }],
            MigratedBoundsMarkerLevelId = "lvl-2",
        };

        var result = RoundTrip(setting);

        result.MigratedBoundsMarkerLevelId.Should().Be("lvl-2");
        result.Levels.Should().ContainSingle(l => l.Id == "lvl-2");
    }

    [Fact]
    public void DataLinkWarningSetting_RoundTrip_EmptyLevelsAndPointsRemainEmpty()
    {
        var setting = new DataLinkWarningSetting { DataId = 1 };

        var result = RoundTrip(setting);

        result.Levels.Should().BeEmpty();
        result.Points.Should().BeEmpty();
    }

    [Fact]
    public void DataLinkWarningSetting_DeserializesLegacyMinMaxIntoObsoleteProperties()
    {
        const string json =
            """{"dataId": 1, "enabled": true, "minWarning": 0.8, "maxWarning": 1.5, "status": "Typical", "name": "x"}""";

        var result = JsonSerializer.Deserialize(json, CalibrationJsonContext.Default.DataLinkWarningSetting)!;

#pragma warning disable CS0618 // Intentional: asserting legacy shadow properties
        result.MinWarning.Should().Be(0.8f);
        result.MaxWarning.Should().Be(1.5f);
#pragma warning restore CS0618
        result.Levels.Should().BeEmpty();
        result.Points.Should().BeEmpty();
    }

    [Fact]
    public void BatchLedger_RoundTrip_EmptyAndPopulated()
    {
        var emptyJson = JsonSerializer.Serialize(
            new List<BatchLedgerEntry>(), CalibrationJsonContext.Default.ListBatchLedgerEntry);
        var empty = JsonSerializer.Deserialize(emptyJson, CalibrationJsonContext.Default.ListBatchLedgerEntry)!;
        empty.Should().BeEmpty();

        var ledger = new List<BatchLedgerEntry>
        {
            new() { DataId = 1, Outcome = "success", Timestamp = 1000 },
            new() { DataId = 2, Outcome = "failed", Timestamp = 2000 },
            new() { DataId = 3, Outcome = "skipped", Timestamp = 3000 },
        };

        var json = JsonSerializer.Serialize(ledger, CalibrationJsonContext.Default.ListBatchLedgerEntry);
        var result = JsonSerializer.Deserialize(json, CalibrationJsonContext.Default.ListBatchLedgerEntry)!;

        result.Should().HaveCount(3);
        result[0].DataId.Should().Be(1);
        result[0].Outcome.Should().Be("success");
        result[0].Timestamp.Should().Be(1000);
        result[1].Outcome.Should().Be("failed");
        result[2].Outcome.Should().Be("skipped");
    }

    [Fact]
    public void QueuedBanner_RoundTrip_PreservesDataIdsAndMessage()
    {
        var banner = new QueuedBanner { DataIds = [1, 2, 3], Kind = "info", Message = "Applied", Timestamp = 1234 };

        var json = JsonSerializer.Serialize(banner, CalibrationJsonContext.Default.QueuedBanner);
        var result = JsonSerializer.Deserialize(json, CalibrationJsonContext.Default.QueuedBanner)!;

        result.DataIds.Should().Equal([1, 2, 3]);
        result.Kind.Should().Be("info");
        result.Message.Should().Be("Applied");
        result.Timestamp.Should().Be(1234);
    }

    [Fact]
    public void UndoExpiryNotice_RoundTrip_PreservesDataIdAndTimestamp()
    {
        var notice = new UndoExpiryNotice { DataId = 9, Timestamp = 5678 };

        var json = JsonSerializer.Serialize(notice, CalibrationJsonContext.Default.UndoExpiryNotice);
        var result = JsonSerializer.Deserialize(json, CalibrationJsonContext.Default.UndoExpiryNotice)!;

        result.DataId.Should().Be(9);
        result.Timestamp.Should().Be(5678);
    }
}
