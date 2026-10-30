using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Fuel trip (average fuel efficiency) ────────────────────────────────
    //
    // The JS side owns the integration (it has the frame clock) and treats these
    // as cumulative totals in km/liters. Storing canonical units — not the
    // configured display unit — means switching km/L ⇄ L/100km ⇄ MPG never
    // discards an in-progress trip.

    /// <summary>
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetFuelTrip', [])
    /// Reads the active dashboard's persisted trip totals.
    /// </summary>
    public async Task<string> GetFuelTrip()
    {
        try
        {
            var config = await GetLocalDashboardConfigAsync().ConfigureAwait(false);
            var name = ResolveActiveDashboardName(config);
            var trip = config?.Dashboards?.TryGetValue(name, out var def) == true
                ? def.FuelTrip
                : null;

            return JsonSerializer.Serialize(new
            {
                success = true,
                distanceKm = trip?.DistanceKm ?? 0,
                fuelLiters = trip?.FuelLiters ?? 0,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFuelTrip failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Called from JS: window.HybridWebView.InvokeDotNet('SetFuelTrip', [distanceKm, fuelLiters])
    /// Writes cumulative trip totals for the active dashboard. Non-finite or
    /// negative inputs are clamped to zero, which is also how a reset is stored.
    /// </summary>
    public async Task<string> SetFuelTrip(double distanceKm, double fuelLiters)
    {
        if (GateWrite("fuelTrip") is { } gated) return gated;
        try
        {
            var config = await GetLocalDashboardConfigAsync().ConfigureAwait(false);
            if (config == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "No dashboard config" });
            }

            var name = ResolveActiveDashboardName(config);
            if (!config.Dashboards.TryGetValue(name, out var def))
            {
                def = new DashboardDefinition();
                config.Dashboards[name] = def;
            }

            def.FuelTrip = new FuelTripConfig
            {
                DistanceKm = double.IsFinite(distanceKm) && distanceKm > 0 ? distanceKm : 0,
                FuelLiters = double.IsFinite(fuelLiters) && fuelLiters > 0 ? fuelLiters : 0,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };

            await SaveLocalDashboardConfigAsync(config).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SetFuelTrip failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
