using ME221.Data.Models;

namespace ME221.Data.Infrastructure;

/// <summary>
/// Per-severity level + point set produced by one DEF feedback severity group.
/// </summary>
public sealed class DefaultSet
{
    public List<WarningLevel> Levels { get; set; } = [];
    public List<WarningPoint> Points { get; set; } = [];
}

/// <summary>
/// Pure R7/R19 read-time default computation: per-severity level/point sets,
/// replacement rules, point re-attachment, and duplicate collapse.
/// </summary>
public static class WarningDefaults
{
    public const string WarningRole = "warning";
    public const string AlarmRole = "alarm";
    public const string WarningColor = "#f59e0b";
    public const string AlarmColor = "#ef4444";

    /// <summary>
    /// KTD1: deterministic read-time level id, identical across reads for the same
    /// (dataId, severity-role) pair.
    /// </summary>
    public static string DeterministicLevelId(int dataId, string role) => $"read-{dataId}-{role}";

    /// <summary>
    /// R7 default "warning" level. autolog=true only on the R18 zero-bound migration path.
    /// </summary>
    public static WarningLevel BuildR7Default(int dataId, bool autolog)
    {
        return new WarningLevel
        {
            Id = DeterministicLevelId(dataId, WarningRole),
            Name = WarningRole,
            Color = WarningColor,
            Autolog = autolog,
            Flash = false,
            Order = 0,
        };
    }

    /// <summary>
    /// R19 per-severity default set from a datalink's DEF feedbacks (Warning/Alarm only).
    /// Returns null when no Warning/Alarm feedback exists.
    /// </summary>
    public static DefaultSet? BuildDefaults(int dataId, DataLinkDefinition dl, bool markerSet)
    {
        if (dl.Feedbacks is not { Count: > 0 }) return null;

        var levelsByRole = new Dictionary<string, WarningLevel>();
        var points = new List<WarningPoint>();

        foreach (var fb in dl.Feedbacks)
        {
            var role = fb.Severity switch
            {
                DataLinkFeedbackSeverity.Warning => WarningRole,
                DataLinkFeedbackSeverity.Alarm => AlarmRole,
                _ => null,
            };
            if (role is null) continue;

            if (!levelsByRole.TryGetValue(role, out var level))
            {
                var isAlarm = role == AlarmRole;
                level = new WarningLevel
                {
                    Id = DeterministicLevelId(dataId, role),
                    Name = role,
                    Color = isAlarm ? AlarmColor : WarningColor,
                    Autolog = isAlarm,
                    Flash = fb.Flashing ?? isAlarm,
                    Order = isAlarm ? 1 : 0,
                };
                // R18: the upgrade never silently ends history recording.
                if (markerSet && !isAlarm) level.Autolog = true;
                levelsByRole[role] = level;
            }

            if (fb.MinValue is not null)
                points.Add(new WarningPoint { Value = fb.MinValue.Value, Direction = "min", LevelId = level.Id, Enabled = true });
            if (fb.MaxValue is not null)
                points.Add(new WarningPoint { Value = fb.MaxValue.Value, Direction = "max", LevelId = level.Id, Enabled = true });
        }

        if (levelsByRole.Count == 0) return null;

        return new DefaultSet
        {
            Levels = [.. levelsByRole.Values.OrderBy(l => l.Order)],
            Points = points,
        };
    }

    /// <summary>
    /// R19 pure merge: replaces the datalink's levels with the default set, re-attaches
    /// points whose level was replaced to the same-role new level, drops role-less
    /// points (pointsRemoved=true → load-time removal notice), collapses duplicates,
    /// and re-points the migrated-bounds marker at the new "warning"-role level.
    /// </summary>
    public static void ApplyDefaults(DataLinkWarningSetting s, DefaultSet d, out bool pointsRemoved)
    {
        pointsRemoved = false;

        var oldLevelsById = s.Levels.ToDictionary(l => l.Id, l => l);
        var newLevelIds = new HashSet<string>(d.Levels.Select(l => l.Id));
        var newLevelByRole = new Dictionary<string, WarningLevel>();
        foreach (var l in d.Levels)
        {
            var role = WarningMigration.ResolveRole(l);
            if (role != "") newLevelByRole[role] = l;
        }

        s.Levels = d.Levels;

        var kept = new List<WarningPoint>();
        foreach (var p in s.Points)
        {
            if (newLevelIds.Contains(p.LevelId))
            {
                kept.Add(p);
                continue;
            }

            oldLevelsById.TryGetValue(p.LevelId, out var oldLevel);
            var role = oldLevel is null ? "" : WarningMigration.ResolveRole(oldLevel);
            if (role != "" && newLevelByRole.TryGetValue(role, out var target))
            {
                p.LevelId = target.Id;
                kept.Add(p);
            }
            else
            {
                pointsRemoved = true;
            }
        }

        var seen = new HashSet<(float Value, string Direction, string LevelId)>();
        var deduped = new List<WarningPoint>();
        foreach (var p in kept)
        {
            if (seen.Add((p.Value, p.Direction, p.LevelId)))
                deduped.Add(p);
        }
        s.Points = deduped;

        if (s.MigratedBoundsMarkerSet)
        {
            s.MigratedBoundsMarkerLevelId = newLevelByRole.TryGetValue(WarningRole, out var warningLevel) ? warningLevel.Id : null;
        }
    }
}
