using ME221.Data.Models;

namespace ME221.Data.Infrastructure;

/// <summary>
/// Pure R18 migration transforms for legacy warning settings.
/// </summary>
public static class WarningMigration
{
    /// <summary>
    /// True when the record still carries legacy min/max shadow bounds (pre-migration).
    /// </summary>
    public static bool HasLegacyBounds(DataLinkWarningSetting s)
    {
#pragma warning disable CS0618 // Intentional: legacy shadow properties are the migration source
        return s.MinWarning is not null || s.MaxWarning is not null;
#pragma warning restore CS0618
    }

    /// <summary>
    /// R18 in-place migration: materializes a "warning" level with one point per
    /// surviving legacy bound, clears the shadow fields, and marks the datalink as
    /// migrated. Idempotent — re-running on an already-migrated record is a no-op.
    /// </summary>
    public static void MigrateSetting(DataLinkWarningSetting s)
    {
#pragma warning disable CS0618 // Intentional: legacy shadow properties are the migration source
        var min = s.MinWarning;
        var max = s.MaxWarning;
#pragma warning restore CS0618

        if (min is not null || max is not null)
        {
            var levelId = Guid.NewGuid().ToString("N");
            s.Levels.Add(new WarningLevel
            {
                Id = levelId,
                Name = "warning",
                Color = "#f59e0b",
                Autolog = true,
                Flash = false,
                Order = 0,
            });

            if (min is not null)
                s.Points.Add(new WarningPoint { Value = min.Value, Direction = "min", LevelId = levelId, Enabled = true });
            if (max is not null)
                s.Points.Add(new WarningPoint { Value = max.Value, Direction = "max", LevelId = levelId, Enabled = true });

            s.MigratedBoundsMarkerSet = true;
            s.MigratedBoundsMarkerLevelId = levelId;
        }
        else if (!s.MigratedBoundsMarkerSet)
        {
            // Zero-surviving-bound path: marker state persists without a reference.
            s.MigratedBoundsMarkerSet = true;
            s.MigratedBoundsMarkerLevelId = null;
        }

#pragma warning disable CS0618 // Intentional: the clear branch commits legacy fields to null
        s.MinWarning = null;
        s.MaxWarning = null;
#pragma warning restore CS0618

        // R3: status stores only Typical/Custom; Disabled is derived from the Enabled flag.
        if (s.Status == WarningSettingStatus.Disabled)
            s.Status = WarningSettingStatus.Typical;
    }

    /// <summary>
    /// R18 delay clamp: NaN/non-integer → 500, above 60000 → 60000, negative → 0, else pass through.
    /// </summary>
    public static double MigrateDelay(double? legacy)
    {
        if (legacy is null) return 500;
        var v = legacy.Value;
        if (double.IsNaN(v)) return 500;
        if (v != Math.Floor(v)) return 500;
        if (v > 60000) return 60000;
        if (v < 0) return 0;
        return v;
    }

    /// <summary>
    /// Severity-role key from a level name ("warning"/"alarm"), else "" (no role).
    /// </summary>
    public static string ResolveRole(WarningLevel level)
    {
        var key = level.Name.Trim().ToLowerInvariant();
        return key is "warning" or "alarm" ? key : "";
    }
}
