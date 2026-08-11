using FluentAssertions;
using ME221.Data.Infrastructure;
using ME221.Data.Models;
using Xunit;

namespace ME221.Data.Tests;

public class WarningDefaultsTests
{
    private static DataLinkDefinition Datalink(int id, params DataLinkFeedback[] feedbacks)
        => new() { Id = (ushort)id, Name = $"Link {id}", Feedbacks = feedbacks.ToList() };

    [Fact]
    public void BuildDefaults_WarningAndAlarmWithoutFlashing_AppliesSeverityDefaults()
    {
        var dl = Datalink(7,
            new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Warning, MinValue = 0.8f, MaxValue = 1.5f },
            new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Alarm, MinValue = 0.5f, MaxValue = 2.0f });

        var set = WarningDefaults.BuildDefaults(7, dl, markerSet: false)!;

        set.Levels.Should().HaveCount(2);
        var warning = set.Levels.Single(l => l.Name == "warning");
        warning.Autolog.Should().BeFalse();
        warning.Flash.Should().BeFalse();
        warning.Color.Should().Be("#f59e0b");
        warning.Order.Should().Be(0);
        var alarm = set.Levels.Single(l => l.Name == "alarm");
        alarm.Autolog.Should().BeTrue();
        alarm.Flash.Should().BeTrue();
        alarm.Color.Should().Be("#ef4444");
        alarm.Order.Should().Be(1);

        set.Points.Should().HaveCount(4);
        set.Points.Should().Contain(p => p.Direction == "min" && p.Value == 0.8f && p.LevelId == warning.Id);
        set.Points.Should().Contain(p => p.Direction == "max" && p.Value == 1.5f && p.LevelId == warning.Id);
        set.Points.Should().Contain(p => p.Direction == "min" && p.Value == 0.5f && p.LevelId == alarm.Id);
        set.Points.Should().Contain(p => p.Direction == "max" && p.Value == 2.0f && p.LevelId == alarm.Id);
        set.Points.Should().OnlyContain(p => p.Enabled);
    }

    [Fact]
    public void BuildDefaults_FlashingAttributes_HonorPresence()
    {
        var dl = Datalink(8,
            new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Warning, Flashing = true },
            new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Alarm, Flashing = false });

        var set = WarningDefaults.BuildDefaults(8, dl, markerSet: false)!;

        set.Levels.Single(l => l.Name == "warning").Flash.Should().BeTrue();
        set.Levels.Single(l => l.Name == "alarm").Flash.Should().BeFalse();
    }

    [Fact]
    public void BuildDefaults_LevelIds_AreDeterministicAcrossCalls()
    {
        var dl = Datalink(7,
            new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Warning, MaxValue = 1.5f },
            new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Alarm, MaxValue = 2.0f });

        var first = WarningDefaults.BuildDefaults(7, dl, markerSet: false)!;
        var second = WarningDefaults.BuildDefaults(7, dl, markerSet: false)!;

        first.Levels.Select(l => l.Id).Should().Equal(second.Levels.Select(l => l.Id));
        first.Levels[0].Id.Should().Be("read-7-warning");
        first.Levels[1].Id.Should().Be("read-7-alarm");
    }

    [Fact]
    public void BuildDefaults_MarkerSet_ReinitializesWarningAutolog()
    {
        var dl = Datalink(7,
            new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Warning, MaxValue = 1.5f });

        var set = WarningDefaults.BuildDefaults(7, dl, markerSet: true)!;

        set.Levels.Single(l => l.Name == "warning").Autolog.Should().BeTrue();
    }

    [Fact]
    public void BuildDefaults_AlarmOnly_CompleteSetIsAlarm()
    {
        var dl = Datalink(9, new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Alarm, MaxValue = 2.0f });

        var set = WarningDefaults.BuildDefaults(9, dl, markerSet: true)!;

        set.Levels.Select(l => l.Name).Should().Equal(["alarm"]);
        set.Levels.Single().Autolog.Should().BeTrue();
    }

    [Fact]
    public void BuildDefaults_NoWarningAlarmFeedback_ReturnsNull()
    {
        var dl = Datalink(9,
            new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Ok, MinValue = 1f, MaxValue = 2f },
            new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Ok, MaxValue = 5f });

        WarningDefaults.BuildDefaults(9, dl, markerSet: false).Should().BeNull();
    }

    [Fact]
    public void BuildDefaults_NoFeedbacks_ReturnsNull()
    {
        var dl = Datalink(9);

        WarningDefaults.BuildDefaults(9, dl, markerSet: false).Should().BeNull();
    }

    [Fact]
    public void ApplyDefaults_ReattachesPointsToSameRoleLevelAndRepointsMarker()
    {
        var s = new DataLinkWarningSetting
        {
            DataId = 7,
            Status = WarningSettingStatus.Typical,
            MigratedBoundsMarkerSet = true,
            MigratedBoundsMarkerLevelId = "old-warning-id",
            Levels =
            [
                new WarningLevel { Id = "old-warning-id", Name = "warning" },
                new WarningLevel { Id = "old-alarm-id", Name = "alarm" },
            ],
            Points =
            [
                new WarningPoint { Id = "p1", Value = 0.8f, Direction = "min", LevelId = "old-warning-id", Enabled = true },
                new WarningPoint { Id = "p2", Value = 2.0f, Direction = "max", LevelId = "old-alarm-id", Enabled = true },
            ],
        };
        var dl = Datalink(7,
            new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Warning, MinValue = 0.8f },
            new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Alarm, MaxValue = 2.0f });
        var set = WarningDefaults.BuildDefaults(7, dl, markerSet: false)!;

        WarningDefaults.ApplyDefaults(s, set, out var pointsRemoved);

        pointsRemoved.Should().BeFalse();
        s.Levels.Select(l => l.Id).Should().Equal(["read-7-warning", "read-7-alarm"]);
        s.Points.Should().HaveCount(2);
        s.Points.Single(p => p.Id == "p1").LevelId.Should().Be("read-7-warning");
        s.Points.Single(p => p.Id == "p2").LevelId.Should().Be("read-7-alarm");
        s.MigratedBoundsMarkerSet.Should().BeTrue();
        s.MigratedBoundsMarkerLevelId.Should().Be("read-7-warning");
    }

    [Fact]
    public void ApplyDefaults_DropsRolelessPointsAndReportsRemoval()
    {
        var s = new DataLinkWarningSetting
        {
            DataId = 7,
            Status = WarningSettingStatus.Typical,
            MigratedBoundsMarkerSet = false,
            Levels = [new WarningLevel { Id = "custom-id", Name = "boost" }],
            Points = [new WarningPoint { Id = "p1", Value = 5f, Direction = "max", LevelId = "custom-id", Enabled = true }],
        };
        var dl = Datalink(7, new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Warning, MaxValue = 1.5f });
        var set = WarningDefaults.BuildDefaults(7, dl, markerSet: false)!;

        WarningDefaults.ApplyDefaults(s, set, out var pointsRemoved);

        pointsRemoved.Should().BeTrue();
        s.Points.Should().BeEmpty();
        s.Levels.Single().Id.Should().Be("read-7-warning");
    }

    [Fact]
    public void ApplyDefaults_CollapsesDuplicatesKeepingFirst()
    {
        var s = new DataLinkWarningSetting
        {
            DataId = 7,
            Status = WarningSettingStatus.Typical,
            MigratedBoundsMarkerSet = false,
            Levels =
            [
                new WarningLevel { Id = "w1", Name = "warning" },
                new WarningLevel { Id = "w2", Name = "warning" },
            ],
            Points =
            [
                new WarningPoint { Id = "p1", Value = 0.8f, Direction = "min", LevelId = "w1", Enabled = true },
                new WarningPoint { Id = "p2", Value = 0.8f, Direction = "min", LevelId = "w2", Enabled = true },
            ],
        };
        var dl = Datalink(7, new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Warning, MinValue = 0.8f });
        var set = WarningDefaults.BuildDefaults(7, dl, markerSet: false)!;

        WarningDefaults.ApplyDefaults(s, set, out var pointsRemoved);

        pointsRemoved.Should().BeFalse();
        s.Points.Should().ContainSingle();
        s.Points[0].Id.Should().Be("p1");
        s.Points[0].LevelId.Should().Be("read-7-warning");
    }

    [Fact]
    public void ApplyDefaults_AlarmOnlyReplacement_NullsMarkerRefWhileMarkerSetPersists()
    {
        var s = new DataLinkWarningSetting
        {
            DataId = 9,
            Status = WarningSettingStatus.Typical,
            MigratedBoundsMarkerSet = true,
            MigratedBoundsMarkerLevelId = "old-alarm-id",
            Levels = [new WarningLevel { Id = "old-alarm-id", Name = "alarm" }],
            Points = [new WarningPoint { Id = "p1", Value = 1f, Direction = "min", LevelId = "old-alarm-id", Enabled = true }],
        };
        var dl = Datalink(9, new DataLinkFeedback { Severity = DataLinkFeedbackSeverity.Alarm, MaxValue = 2.0f });
        var set = WarningDefaults.BuildDefaults(9, dl, markerSet: false)!;

        WarningDefaults.ApplyDefaults(s, set, out var pointsRemoved);

        pointsRemoved.Should().BeFalse();
        s.Levels.Select(l => l.Name).Should().Equal(["alarm"]);
        s.Points.Single().LevelId.Should().Be("read-9-alarm");
        s.MigratedBoundsMarkerSet.Should().BeTrue();
        s.MigratedBoundsMarkerLevelId.Should().BeNull();
    }

    [Fact]
    public void BuildR7Default_UsesDeterministicIdAndFlags()
    {
        var level = WarningDefaults.BuildR7Default(42, autolog: false);

        level.Id.Should().Be("read-42-warning");
        level.Name.Should().Be("warning");
        level.Color.Should().Be("#f59e0b");
        level.Autolog.Should().BeFalse();
        level.Flash.Should().BeFalse();
        level.Order.Should().Be(0);
    }

    [Fact]
    public void BuildR7Default_MarkerPath_UsesAutologTrue()
    {
        WarningDefaults.BuildR7Default(42, autolog: true).Autolog.Should().BeTrue();
    }
}
