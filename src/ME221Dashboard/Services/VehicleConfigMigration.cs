namespace ME221Dashboard.Services;

/// <summary>
/// Pure decision logic for the global → per-dashboard vehicle config migration (KTD3).
/// Host-independent: no MAUI, IO, or bridge dependencies, so the rules are unit-testable
/// from a plain test host.
/// </summary>
public static class VehicleConfigMigration
{
    /// <summary>
    /// Resolve the initial seed template: the legacy global value, else the first dashboard
    /// carrying a Vehicle (replicating the removed read-time write-back), else fresh defaults.
    /// </summary>
    public static VehicleConfig ResolveSeed(DashboardConfig config)
    {
#pragma warning disable CS0618 // Intentional: reading the legacy global slot exactly once at migration
        if (config.Vehicle is { } global)
            return Clone(global);
#pragma warning restore CS0618
        if (config.Dashboards != null)
        {
            foreach (var def in config.Dashboards.Values)
            {
                var vc = def?.Vehicle;
                if (vc != null)
                    return Clone(vc);
            }
        }
        return new VehicleConfig();
    }

    /// <summary>
    /// Completeness gate (R7): all gear ratios &gt; 0, tire diameter &gt; 0, final drive &gt; 0.
    /// Incomplete configs never refresh the seed template.
    /// </summary>
    public static bool IsComplete(VehicleConfig vc)
    {
        return vc != null
            && vc.TireDiameterInches > 0
            && vc.FinalDriveRatio > 0
            && vc.GearRatios is { Length: > 0 }
            && vc.GearRatios.All(r => r > 0);
    }

    /// <summary>
    /// Seed refresh gate: only complete configs that differ from the current seed refresh it.
    /// </summary>
    public static bool ShouldRefreshSeed(VehicleConfig? currentSeed, VehicleConfig candidate)
    {
        return IsComplete(candidate) && !ValuesEqual(currentSeed, candidate);
    }

    /// <summary>
    /// Materialize the seed into the active dashboard's Vehicle slot when null,
    /// closing the rollback window for seed-only dashboards.
    /// </summary>
    public static void MaterializeSeed(DashboardConfig config, VehicleConfig seed, string activeDashboardName)
    {
        if (config.Dashboards != null
            && config.Dashboards.TryGetValue(activeDashboardName, out var active)
            && active != null
            && active.Vehicle == null)
        {
            active.Vehicle = Clone(seed);
        }
    }

    /// <summary>
    /// Structural equality on the persisted vehicle-config fields only.
    /// </summary>
    public static bool ValuesEqual(VehicleConfig? a, VehicleConfig? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Enabled != b.Enabled
            || a.TireDiameterInches != b.TireDiameterInches
            || a.FinalDriveRatio != b.FinalDriveRatio
            || a.WheelSlipPercent != b.WheelSlipPercent
            || a.RpmEntityId != b.RpmEntityId
            || a.VssSpeedEntityId != b.VssSpeedEntityId
            || a.MapEntityId != b.MapEntityId
            || a.BaroEntityId != b.BaroEntityId
            || a.GearEntityId != b.GearEntityId)
        {
            return false;
        }
        return a.GearRatios.SequenceEqual(b.GearRatios ?? []);
    }

    public static VehicleConfig Clone(VehicleConfig vc)
    {
        return new VehicleConfig
        {
            Enabled = vc.Enabled,
            TireDiameterInches = vc.TireDiameterInches,
            FinalDriveRatio = vc.FinalDriveRatio,
            GearRatios = (double[])(vc.GearRatios?.Clone() ?? Array.Empty<double>()),
            WheelSlipPercent = vc.WheelSlipPercent,
            RpmEntityId = vc.RpmEntityId,
            VssSpeedEntityId = vc.VssSpeedEntityId,
            MapEntityId = vc.MapEntityId,
            BaroEntityId = vc.BaroEntityId,
            GearEntityId = vc.GearEntityId,
        };
    }
}
