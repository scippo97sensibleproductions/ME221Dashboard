using FluentAssertions;
using ME221.Data.Infrastructure;
using ME221.Data.Models;
using Xunit;

namespace ME221.Data.Tests;

public class WarningMigrationTests
{
    private static DataLinkWarningSetting LegacySetting(float? min, float? max, WarningSettingStatus status)
    {
        var s = new DataLinkWarningSetting { DataId = 1, Enabled = true, Status = status };
#pragma warning disable CS0618 // Intentional: constructing legacy shadow properties for migration
        s.MinWarning = min;
        s.MaxWarning = max;
#pragma warning restore CS0618
        return s;
    }

    [Fact]
    public void MigrateSetting_WithBothBounds_ProducesTwoPointsAtWarningLevelWithMarker()
    {
        var s = LegacySetting(0.8f, 1.5f, WarningSettingStatus.Typical);

        WarningMigration.MigrateSetting(s);

        s.Levels.Should().ContainSingle();
        var level = s.Levels[0];
        level.Name.Should().Be("warning");
        level.Color.Should().Be("#f59e0b");
        level.Autolog.Should().BeTrue();
        level.Flash.Should().BeFalse();
        level.Order.Should().Be(0);

        s.Points.Should().HaveCount(2);
        var minPoint = s.Points.Single(p => p.Direction == "min");
        minPoint.Value.Should().Be(0.8f);
        minPoint.LevelId.Should().Be(level.Id);
        minPoint.Enabled.Should().BeTrue();
        var maxPoint = s.Points.Single(p => p.Direction == "max");
        maxPoint.Value.Should().Be(1.5f);
        maxPoint.LevelId.Should().Be(level.Id);
        maxPoint.Enabled.Should().BeTrue();

        s.MigratedBoundsMarkerSet.Should().BeTrue();
        s.MigratedBoundsMarkerLevelId.Should().Be(level.Id);
        s.Status.Should().Be(WarningSettingStatus.Typical);
        WarningMigration.HasLegacyBounds(s).Should().BeFalse();
    }

    [Fact]
    public void MigrateSetting_ZeroBoundsTypical_SetsMarkerWithoutLevels()
    {
        var s = LegacySetting(null, null, WarningSettingStatus.Typical);

        WarningMigration.MigrateSetting(s);

        s.Levels.Should().BeEmpty();
        s.Points.Should().BeEmpty();
        s.MigratedBoundsMarkerSet.Should().BeTrue();
        s.MigratedBoundsMarkerLevelId.Should().BeNull();
        s.Status.Should().Be(WarningSettingStatus.Typical);
    }

    [Fact]
    public void MigrateSetting_ZeroBoundsDisabled_ReconstructsTypicalStatus()
    {
        var s = LegacySetting(null, null, WarningSettingStatus.Disabled);

        WarningMigration.MigrateSetting(s);

        s.Levels.Should().BeEmpty();
        s.MigratedBoundsMarkerSet.Should().BeTrue();
        s.MigratedBoundsMarkerLevelId.Should().BeNull();
        s.Status.Should().Be(WarningSettingStatus.Typical);
        s.Enabled.Should().BeTrue();
    }

    [Fact]
    public void MigrateSetting_ZeroBoundsCustom_SetsMarkerWithoutLevels()
    {
        var s = LegacySetting(null, null, WarningSettingStatus.Custom);

        WarningMigration.MigrateSetting(s);

        s.Levels.Should().BeEmpty();
        s.Points.Should().BeEmpty();
        s.MigratedBoundsMarkerSet.Should().BeTrue();
        s.MigratedBoundsMarkerLevelId.Should().BeNull();
        s.Status.Should().Be(WarningSettingStatus.Custom);
    }

    [Fact]
    public void MigrateSetting_WithOnlyMinBound_ProducesSingleMinPoint()
    {
        var s = LegacySetting(0.8f, null, WarningSettingStatus.Custom);

        WarningMigration.MigrateSetting(s);

        s.Points.Should().ContainSingle(p => p.Direction == "min" && p.Value == 0.8f);
        s.Levels.Should().ContainSingle();
        s.Status.Should().Be(WarningSettingStatus.Custom);
    }

    [Fact]
    public void MigrateSetting_IsIdempotent_NoDuplicatePointsOnDoubleRun()
    {
        var s = LegacySetting(0.8f, 1.5f, WarningSettingStatus.Typical);

        WarningMigration.MigrateSetting(s);
        var firstLevels = s.Levels.ToList();
        var firstPoints = s.Points.ToList();
        var markerRef = s.MigratedBoundsMarkerLevelId;

        WarningMigration.MigrateSetting(s);

        s.Levels.Should().Equal(firstLevels, (a, b) => a.Id == b.Id);
        s.Points.Should().Equal(firstPoints, (a, b) => a.Id == b.Id);
        s.Points.Should().HaveCount(2);
        s.MigratedBoundsMarkerSet.Should().BeTrue();
        s.MigratedBoundsMarkerLevelId.Should().Be(markerRef);
    }

    [Fact]
    public void MigrateSetting_AfterMarkerClearedAndLevelDeleted_DoesNotResurrectPoints()
    {
        var s = LegacySetting(0.8f, 1.5f, WarningSettingStatus.Typical);
        WarningMigration.MigrateSetting(s);

        // User cleared the marker and deleted the migrated level with its points (R9/R18 clear path)
        s.Levels.Clear();
        s.Points.Clear();
        s.MigratedBoundsMarkerLevelId = null;
        s.MigratedBoundsMarkerSet = false;

        // Shadows are already null → migration must not re-run (HasLegacyBounds gate)
        WarningMigration.HasLegacyBounds(s).Should().BeFalse();

        WarningMigration.MigrateSetting(s);
        s.Levels.Should().BeEmpty();
        s.Points.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(60000.0, 60000.0)]
    [InlineData(75000.0, 60000.0)]
    [InlineData(-5.0, 0.0)]
    [InlineData(250.5, 500.0)]
    [InlineData(1234.0, 1234.0)]
    public void MigrateDelay_ClampsPerR18(double input, double expected)
    {
        WarningMigration.MigrateDelay(input).Should().Be(expected);
    }

    [Fact]
    public void MigrateDelay_NaN_ReturnsDefault500()
    {
        WarningMigration.MigrateDelay(double.NaN).Should().Be(500);
    }

    [Fact]
    public void MigrateDelay_Null_ReturnsDefault500()
    {
        WarningMigration.MigrateDelay(null).Should().Be(500);
    }

    [Theory]
    [InlineData("warning", "warning")]
    [InlineData("alarm", "alarm")]
    [InlineData(" Warning ", "warning")]
    [InlineData("ALARM", "alarm")]
    [InlineData("boost", "")]
    [InlineData("critical", "")]
    public void ResolveRole_ResolvesSeverityRolesFromName(string name, string expected)
    {
        WarningMigration.ResolveRole(new WarningLevel { Name = name }).Should().Be(expected);
    }
}
