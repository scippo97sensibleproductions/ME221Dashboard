using System.Text.Json;
using System.Text.Json.Nodes;
using ME221.Data.Models;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Dashboard Name CRUD ─────────────────────────────────────────────────

    /// <summary>
    /// Get the list of dashboard names.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetDashboardNames')
    /// </summary>
    public async Task<string> GetDashboardNames()
    {
        _logger.LogInformation("GetDashboardNames called");
        try
        {
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            var names = config?.Dashboards?.Keys.ToList() ?? [];
            if (names.Count == 0) names = ["default"];
            return JsonSerializer.Serialize(new { names, activeDashboard = ResolveEffectiveActiveDashboard(config) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDashboardNames failed");
            return JsonSerializer.Serialize(new { names = new[] { "default" }, activeDashboard = "default" });
        }
    }

    /// <summary>
    /// Resolve the effective active dashboard from the persisted config:
    /// the persisted ActiveDashboard when that dashboard exists, else the FIRST
    /// dashboard in insertion order, else "default" (a fresh config always
    /// contains one). Never invents a name that is not in the config — a stale
    /// or missing ActiveDashboard must not make pages query "default".
    /// </summary>
    internal static string ResolveEffectiveActiveDashboard(DashboardConfig? config)
    {
        if (config?.Dashboards is { Count: > 0 } dashboards)
        {
            if (config.ActiveDashboard is { Length: > 0 } active && dashboards.ContainsKey(active))
                return active;
            return dashboards.Keys.First();
        }
        return "default";
    }

    /// <summary>
    /// Create a new empty dashboard.
    /// Called from JS: window.HybridWebView.InvokeDotNet('CreateDashboard', [name])
    /// </summary>
    public async Task<string> CreateDashboard(string name)
    {
        _logger.LogInformation("CreateDashboard called");
        try
        {
            await EnsureVehicleMigrationAsync().ConfigureAwait(false);
            name = name?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
                return JsonSerializer.Serialize(new { success = false, error = "Name is required" });

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();

            if (config.Dashboards.ContainsKey(name))
                return JsonSerializer.Serialize(new { success = false, error = "A dashboard with this name already exists" });

            // Seed the new dashboard's vehicle from the hidden template (R7); shifter
            // settings always initialize to zero, never seed values (R9).
            var seeded = config.VehicleSeedTemplate != null
                ? VehicleConfigMigration.Clone(config.VehicleSeedTemplate)
                : null;
            config.Dashboards[name] = new DashboardDefinition
            {
                Vehicle = seeded,
                ShifterConfig = new ShifterConfig(),
            };
            config.ActiveDashboard = name;
            _activeDashboardName = name;
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);

            // The created dashboard becomes active — keep the emulator feed on it (R10).
            await WriteEmulatorVehicleFileAsync(config, name).ConfigureAwait(false);

            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateDashboard failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a dashboard.
    /// Called from JS: window.HybridWebView.InvokeDotNet('DeleteDashboard', [name])
    /// </summary>
    public async Task<string> DeleteDashboard(string name)
    {
        _logger.LogInformation("DeleteDashboard called");
        try
        {
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            if (config?.Dashboards is null)
                return JsonSerializer.Serialize(new { success = false, error = "No config found" });

            if (!config.Dashboards.ContainsKey(name))
                return JsonSerializer.Serialize(new { success = false, error = "Dashboard not found" });

            if (config.Dashboards.Count <= 1)
                return JsonSerializer.Serialize(new { success = false, error = "Cannot delete the last dashboard" });

            config.Dashboards.Remove(name);
            if (config.ActiveDashboard == name)
            {
                config.ActiveDashboard = config.Dashboards.Keys.First();
                _activeDashboardName = config.ActiveDashboard;
            }

            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true, activeDashboard = config.ActiveDashboard });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteDashboard failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Rename a dashboard.
    /// Called from JS: window.HybridWebView.InvokeDotNet('RenameDashboard', [oldName, newName])
    /// </summary>
    public async Task<string> RenameDashboard(string oldName, string newName)
    {
        _logger.LogInformation("RenameDashboard called");
        try
        {
            newName = newName?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(newName))
                return JsonSerializer.Serialize(new { success = false, error = "Name is required" });

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            if (config?.Dashboards is null)
                return JsonSerializer.Serialize(new { success = false, error = "No config found" });

            if (!config.Dashboards.ContainsKey(oldName))
                return JsonSerializer.Serialize(new { success = false, error = "Dashboard not found" });

            if (config.Dashboards.ContainsKey(newName))
                return JsonSerializer.Serialize(new { success = false, error = "A dashboard with this name already exists" });

            var def = config.Dashboards[oldName];
            config.Dashboards.Remove(oldName);
            config.Dashboards[newName] = def;

            if (config.ActiveDashboard == oldName)
            {
                config.ActiveDashboard = newName;
                _activeDashboardName = newName;
            }

            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true, activeDashboard = config.ActiveDashboard });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RenameDashboard failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Set the active dashboard.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SetActiveDashboard', [name])
    /// </summary>
    public async Task<string> SetActiveDashboard(string name)
    {
        _logger.LogInformation("SetActiveDashboard called");
        try
        {
            await EnsureVehicleMigrationAsync().ConfigureAwait(false);
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            if (config?.Dashboards is null)
                return JsonSerializer.Serialize(new { success = false, error = "No config found" });

            if (!config.Dashboards.ContainsKey(name))
                return JsonSerializer.Serialize(new { success = false, error = "Dashboard not found" });

            config.ActiveDashboard = name;
            _activeDashboardName = name;
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);

            // The active dashboard changed — keep the emulator feed on it (R10).
            await WriteEmulatorVehicleFileAsync(config, name).ConfigureAwait(false);

            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SetActiveDashboard failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    // ─── Dashboard Config Methods ────────────────────────────────────────────

    /// <summary>
    /// Get the persisted dashboard gauge configuration.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetDashboardConfig', [dashboardName])
    /// </summary>
    public async Task<string> GetDashboardConfig(string dashboardName)
    {
        _logger.LogInformation("GetDashboardConfig called");
        try
        {
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            if (config?.Dashboards?.TryGetValue(dashboardName, out var dashboard) != true || dashboard!.Gauges.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    found = false,
                    gauges = Array.Empty<object>(),
                    gridRows = 4,
                    gridColumns = 7
                });
            }

            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var links = calResult.Data?.DataLinks ?? [];
            var linksById = links.ToDictionary(l => (int)l.Id);

            var entityLookup = new Dictionary<string, object>();

            foreach (var g in dashboard.Gauges)
            {
                var key = g.Id.ToString();
                if (entityLookup.ContainsKey(key)) continue;

                dashboard.Customizations.TryGetValue(g.Id, out var cust);

                if (linksById.TryGetValue(g.Id, out var link))
                {
                    var unit = cust?.CustomUnit is { Length: > 0 } cu ? cu : link.MeasureUnit;
                    var (defMin, defMax) = GetUnitDefaults(unit);

                    var minVal = cust?.MinRangeBypass == true
                        ? (double?)null
                        : cust?.MinRange.HasValue == true
                            ? (double)cust.MinRange.Value
                            : link.MinValue != 0 || link.MaxValue != 0
                                ? (double)link.MinValue
                                : defMin;
                    var maxVal = cust?.MaxRangeBypass == true
                        ? (double?)null
                        : cust?.MaxRange.HasValue == true
                            ? (double)cust.MaxRange.Value
                            : link.MinValue != 0 || link.MaxValue != 0
                                ? (double)link.MaxValue
                                : defMax;

                    entityLookup[key] = new
                    {
                        name = cust?.CustomName is { Length: > 0 } cn ? cn : link.Name,
                        unit,
                        minValue = minVal,
                        maxValue = maxVal,
                    };
                }
                else if (S_gpsDefaults.TryGetValue(g.Id, out var gps))
                {
                    entityLookup[key] = new
                    {
                        name = cust?.CustomName is { Length: > 0 } cn ? cn : gps.Name,
                        unit = cust?.CustomUnit is { Length: > 0 } cu ? cu : gps.Unit,
                        minValue = cust?.MinRange.HasValue == true ? (double)cust.MinRange.Value : gps.Min,
                        maxValue = cust?.MaxRange.HasValue == true ? (double)cust.MaxRange.Value : gps.Max,
                    };
                }
                else if (S_odometerDefaults.TryGetValue(g.Id, out var odo))
                {
                    entityLookup[key] = new
                    {
                        name = cust?.CustomName is { Length: > 0 } cn ? cn : odo.Name,
                        unit = cust?.CustomUnit is { Length: > 0 } cu ? cu : odo.Unit,
                        minValue = cust?.MinRange.HasValue == true ? (double)cust.MinRange.Value : odo.Min,
                        maxValue = cust?.MaxRange.HasValue == true ? (double)cust.MaxRange.Value : odo.Max,
                    };
                }
                else if (S_derivedDefaults.TryGetValue(g.Id, out var derived))
                {
                    entityLookup[key] = new
                    {
                        name = cust?.CustomName is { Length: > 0 } cn ? cn : derived.Name,
                        unit = cust?.CustomUnit is { Length: > 0 } cu ? cu : derived.Unit,
                        minValue = cust?.MinRange.HasValue == true ? (double)cust.MinRange.Value : derived.Min,
                        maxValue = cust?.MaxRange.HasValue == true ? (double)cust.MaxRange.Value : derived.Max,
                    };
                }
            }

            // Second pass: ensure linked entities (Multi-Ring channels) are in entityLookup
            // even if they don't have their own gauge entry in the dashboard.
            foreach (var g in dashboard.Gauges)
            {
                if (g.LinkedEntities == null) continue;
                foreach (var le in g.LinkedEntities)
                {
                    var key = le.EntityId.ToString();
                    if (entityLookup.ContainsKey(key)) continue;

                    dashboard.Customizations.TryGetValue(le.EntityId, out var cust);

                    if (linksById.TryGetValue(le.EntityId, out var link))
                    {
                        var unit = cust?.CustomUnit is { Length: > 0 } cu ? cu : link.MeasureUnit;
                        var (defMin, defMax) = GetUnitDefaults(unit);

                        var minVal = cust?.MinRangeBypass == true
                            ? (double?)null
                            : cust?.MinRange.HasValue == true
                                ? (double)cust.MinRange.Value
                                : link.MinValue != 0 || link.MaxValue != 0
                                    ? (double)link.MinValue
                                    : defMin;
                        var maxVal = cust?.MaxRangeBypass == true
                            ? (double?)null
                            : cust?.MaxRange.HasValue == true
                                ? (double)cust.MaxRange.Value
                                : link.MinValue != 0 || link.MaxValue != 0
                                    ? (double)link.MaxValue
                                    : defMax;

                        entityLookup[key] = new
                        {
                            name = cust?.CustomName is { Length: > 0 } cn ? cn : link.Name,
                            unit,
                            minValue = minVal,
                            maxValue = maxVal,
                        };
                    }
                    else if (S_gpsDefaults.TryGetValue(le.EntityId, out var gps))
                    {
                        entityLookup[key] = new
                        {
                            name = cust?.CustomName is { Length: > 0 } cn ? cn : gps.Name,
                            unit = cust?.CustomUnit is { Length: > 0 } cu ? cu : gps.Unit,
                            minValue = cust?.MinRange.HasValue == true ? (double)cust.MinRange.Value : gps.Min,
                            maxValue = cust?.MaxRange.HasValue == true ? (double)cust.MaxRange.Value : gps.Max,
                        };
                    }
                    else if (S_odometerDefaults.TryGetValue(le.EntityId, out var odo))
                    {
                        entityLookup[key] = new
                        {
                            name = cust?.CustomName is { Length: > 0 } cn ? cn : odo.Name,
                            unit = cust?.CustomUnit is { Length: > 0 } cu ? cu : odo.Unit,
                            minValue = cust?.MinRange.HasValue == true ? (double)cust.MinRange.Value : odo.Min,
                            maxValue = cust?.MaxRange.HasValue == true ? (double)cust.MaxRange.Value : odo.Max,
                        };
                    }
                    else if (S_derivedDefaults.TryGetValue(le.EntityId, out var derived))
                    {
                        entityLookup[key] = new
                        {
                            name = cust?.CustomName is { Length: > 0 } cn ? cn : derived.Name,
                            unit = cust?.CustomUnit is { Length: > 0 } cu ? cu : derived.Unit,
                            minValue = cust?.MinRange.HasValue == true ? (double)cust.MinRange.Value : derived.Min,
                            maxValue = cust?.MaxRange.HasValue == true ? (double)cust.MaxRange.Value : derived.Max,
                        };
                    }
                }
            }

            return JsonSerializer.Serialize(new
            {
                found = true,
                gauges = dashboard.Gauges.Select(g => new
                {
                    entityId = g.Id,
                    gridRow = g.GridRow,
                    gridColumn = g.GridColumn,
                    rowSpan = g.RowSpan,
                    columnSpan = g.ColumnSpan,
                    displayType = g.DisplayType,
                    shapeCategory = g.ShapeCategory,
                    sweepAngle = g.SweepAngle,
                    arcPosition = g.ArcPosition,
                    iconName = g.IconName,
                    iconOffsetX = g.IconOffsetX,
                    iconOffsetY = g.IconOffsetY,
                    iconSize = g.IconSize,
                    digitalStyle = g.DigitalStyle,
                    wedgeStyle = g.WedgeStyle,
                    texturePath = g.TexturePath,
                    needleStartAngle = g.NeedleStartAngle,
                    needleEndAngle = g.NeedleEndAngle,
                    needleOffsetX = g.NeedleOffsetX,
                    needleOffsetY = g.NeedleOffsetY,
                    needleWidth = g.NeedleWidth,
                    needleLength = g.NeedleLength,
                    needleCurve = g.NeedleCurve?.Select(p => new { rawValue = p.RawValue, angle = p.Angle }).ToList(),
                    scale = g.Scale,
                    fontSizeScale = g.FontSizeScale,
                    labelVerticalOffset = g.LabelVerticalOffset,
                    showName = g.ShowName,
                    showUnit = g.ShowUnit,
                    showValue = g.ShowValue,
                    barValuePosition = g.BarValuePosition,
                    barUnitPosition = g.BarUnitPosition,
                    barNamePosition = g.BarNamePosition,
                    smoothingEnabled = g.SmoothingEnabled,
                    smoothingFactor = g.SmoothingFactor,
                    smoothingResponseMs = g.SmoothingResponseMs,
                    spikeGatePercent = g.SpikeGatePercent,
                    colorStops = g.ColorStops?.Select(c => new { fraction = c.Fraction, r = c.R, g = c.G, b = c.B }).ToList(),
                    colorHysteresis = g.ColorHysteresis,
                    x = g.X,
                    y = g.Y,
                    width = g.Width,
                    height = g.Height,
                    fractionX = g.FractionX,
                    fractionY = g.FractionY,
                    widthFraction = g.WidthFraction,
                    heightFraction = g.HeightFraction,
                    chartTimeWindowSec = g.ChartTimeWindowSec,
                    chartYMin = g.ChartYMin,
                    chartYMax = g.ChartYMax,
                    chartLineColor = g.ChartLineColor,
                    chartLineWidth = g.ChartLineWidth,
                    chartShowGrid = g.ChartShowGrid,
                    chartFillUnder = g.ChartFillUnder,
                    chartShowLabels = g.ChartShowLabels,
                    chartPrecision = g.ChartPrecision,
                    textColor = g.TextColor,
                    zIndex = g.ZIndex,
                    transformSteps = g.TransformSteps?.Select(t => new { operation = (int)t.Operation, operand = t.Operand }).ToList(),
                    customUnitLabel = g.CustomUnitLabel,
                    showHistogram = g.ShowHistogram,
                    linkedEntities = g.LinkedEntities?.Select(le => new { entityId = le.EntityId, color = le.Color }).ToList(),
                    tickCount = g.TickCount,
                    tickLabels = g.TickLabels,
                    tickLabelEvery = g.TickLabelEvery,
                    tickSide = g.TickSide,
                    redlineStart = g.RedlineStart,
                    redlineWidth = g.RedlineWidth,
                    redlineColor = g.RedlineColor,
                    needleShape = g.NeedleShape,
                    barOrientation = g.BarOrientation,
                    barThickness = g.BarThickness,
                    barTicks = g.BarTicks,
                    barMinMaxLabels = g.BarMinMaxLabels,
                    barRedlineStart = g.BarRedlineStart,
                    barRedlineColor = g.BarRedlineColor,
                    colorStopColoring = g.ColorStopColoring,
                    panelStyle = g.PanelStyle,
                    flashThreshold = g.FlashThreshold,
                    ledColor = g.LedColor,
                    digitBgColor = g.DigitBgColor,
                    glowStrength = g.GlowStrength,
                    digitDecimals = g.DigitDecimals,
                    zeroPadding = g.ZeroPadding,
                    minDigitCount = g.MinDigitCount,
                    rollAnimation = g.RollAnimation,
                    rollSpeedMs = g.RollSpeedMs,
                    segmentCount = g.SegmentCount,
                    segmentGap = g.SegmentGap,
                    ringStartAngle = g.RingStartAngle,
                    ringSweepAngle = g.RingSweepAngle,
                    amberThreshold = g.AmberThreshold,
                    redThreshold = g.RedThreshold,
                    ringCount = g.RingCount,
                    ringWidth = g.RingWidth,
                    ringGap = g.RingGap,
                    peakHoldEnabled = g.PeakHoldEnabled,
                    peakHoldAutoResetSec = g.PeakHoldAutoResetSec,
                    wedgeSegmentCount = g.WedgeSegmentCount,
                    wedgeRedlineStart = g.WedgeRedlineStart,
                    rampWidthRpm = g.RampWidthRpm,
                    zoneCount = g.ZoneCount,
                    chartOverlays = g.ChartOverlays?.Select(o => new { entityId = o.EntityId, color = o.Color, lineWidth = o.LineWidth, lineStyle = o.LineStyle }).ToList(),
                    overlayPillPosition = g.OverlayPillPosition,
                    overlayFontScale = g.OverlayFontScale,
                    chartLineStyle = g.ChartLineStyle,
                    chartBackgroundColor = g.ChartBackgroundColor,
                }).ToList(),
                tables = (dashboard.Tables ?? []).Select(t => new
                {
                    tableId = t.TableId,
                    fractionX = t.FractionX,
                    fractionY = t.FractionY,
                    widthFraction = t.WidthFraction,
                    heightFraction = t.HeightFraction,
                    zIndex = t.ZIndex,
                }).ToList(),
                gridRows = config.GridRows,
                gridColumns = config.GridColumns,
                entities = entityLookup,
                backgroundImagePath = dashboard.BackgroundImagePath,
                headerVisible = dashboard.HeaderVisible,
                sidebarVisible = dashboard.SidebarVisible,
                layoutLocked = dashboard.LayoutLocked,
                customizations = dashboard.Customizations?.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => new
                    {
                        id = kvp.Value.Id,
                        customName = kvp.Value.CustomName,
                        customUnit = kvp.Value.CustomUnit,
                        minRange = kvp.Value.MinRange.HasValue ? (double)kvp.Value.MinRange.Value : (double?)null,
                        maxRange = kvp.Value.MaxRange.HasValue ? (double)kvp.Value.MaxRange.Value : (double?)null,
                    })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDashboardConfig failed");
            return JsonSerializer.Serialize(new { found = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Save updated dashboard gauge layout from JS drag repositioning.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveDashboardLayout', [jsonPayload])
    /// payload contains: { dashboardName, gauges: [{ entityId, fractionX, fractionY, widthFraction, heightFraction, ... }] }
    /// </summary>
    public async Task<string> SaveDashboardLayout(string jsonPayload)
    {
        _logger.LogInformation("SaveDashboardLayout called");
        try
        {
            var data = JsonNode.Parse(jsonPayload)!;
            var dashboardName = data["dashboardName"]?.GetValue<string>() ?? "default";

            var gaugeUpdates = data["gauges"]?.AsArray();
            if (gaugeUpdates == null || gaugeUpdates.Count == 0)
                return JsonSerializer.Serialize(new { success = false, error = "No gauge updates provided" });

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            config ??= new DashboardConfig();

            if (!config.Dashboards.TryGetValue(dashboardName, out var dashboard))
            {
                dashboard = new DashboardDefinition();
                config.Dashboards[dashboardName] = dashboard;
            }

            foreach (var g in gaugeUpdates)
            {
                if (g is null) continue;
                var id = g["entityId"]?.GetValue<int>() ?? 0;
                var existing = dashboard.Gauges.FirstOrDefault(x => x.Id == id);
                if (existing != null)
                {
                    if (g["fractionX"] is JsonValue) existing.FractionX = g["fractionX"]!.GetValue<double>();
                    if (g["fractionY"] is JsonValue) existing.FractionY = g["fractionY"]!.GetValue<double>();
                    if (g["widthFraction"] is JsonValue) existing.WidthFraction = g["widthFraction"]!.GetValue<double>();
                    if (g["heightFraction"] is JsonValue) existing.HeightFraction = g["heightFraction"]!.GetValue<double>();
                    if (g["sweepAngle"] is JsonValue) existing.SweepAngle = g["sweepAngle"]!.GetValue<double>();
                    if (g["arcPosition"] is JsonValue) existing.ArcPosition = g["arcPosition"]!.GetValue<int>();
                    if (g["digitalStyle"] is JsonValue) existing.DigitalStyle = g["digitalStyle"]!.GetValue<int>();
                    if (g["wedgeStyle"] is JsonValue) existing.WedgeStyle = g["wedgeStyle"]!.GetValue<int>();
                    if (g["needleStartAngle"] is JsonValue) existing.NeedleStartAngle = g["needleStartAngle"]!.GetValue<double>();
                    if (g["needleEndAngle"] is JsonValue) existing.NeedleEndAngle = g["needleEndAngle"]!.GetValue<double>();
                    if (g["needleOffsetX"] is JsonValue) existing.NeedleOffsetX = g["needleOffsetX"]!.GetValue<double>();
                    if (g["needleOffsetY"] is JsonValue) existing.NeedleOffsetY = g["needleOffsetY"]!.GetValue<double>();
                    if (g["needleWidth"] is JsonValue) existing.NeedleWidth = g["needleWidth"]!.GetValue<double>();
                    if (g["needleLength"] is JsonValue) existing.NeedleLength = g["needleLength"]!.GetValue<double>();
                    if (g["needleCurve"] is JsonArray ncArr)
                    {
                        existing.NeedleCurve = ncArr
                            .Where(p => p is JsonObject)
                            .Select(p => p!.AsObject())
                            .Select(p => new NeedleCurvePoint
                            {
                                RawValue = p["rawValue"]?.GetValue<double>() ?? 0,
                                Angle = p["angle"]?.GetValue<double>() ?? 0,
                            })
                            .ToList();
                    }
                    if (g["scale"] is JsonValue) existing.Scale = g["scale"]!.GetValue<double>();
                    if (g["fontSizeScale"] is JsonValue) existing.FontSizeScale = g["fontSizeScale"]!.GetValue<double>();
                    if (g["labelVerticalOffset"] is JsonValue) existing.LabelVerticalOffset = g["labelVerticalOffset"]!.GetValue<double>();
                    if (g["showName"] is JsonValue) existing.ShowName = g["showName"]!.GetValue<bool>();
                    if (g["showUnit"] is JsonValue) existing.ShowUnit = g["showUnit"]!.GetValue<bool>();
                    if (g["showValue"] is JsonValue) existing.ShowValue = g["showValue"]!.GetValue<bool>();
                    // iconName/texturePath: "key": null in JSON → g["key"] returns C# null,
                    // which fails `is JsonValue`. Use AsObject().ContainsKey() to detect
                    // that the key was sent (even with null value) and clear it.
                    var gObj = g!.AsObject();
                    if (gObj.ContainsKey("iconName"))
                    {
                        var iv = g["iconName"]?.GetValue<string>();
                        existing.IconName = string.IsNullOrEmpty(iv) ? null : iv;
                    }
                    if (g["iconOffsetX"] is JsonValue) existing.IconOffsetX = g["iconOffsetX"]!.GetValue<double>();
                    if (g["iconOffsetY"] is JsonValue) existing.IconOffsetY = g["iconOffsetY"]!.GetValue<double>();
                    if (g["iconSize"] is JsonValue) existing.IconSize = g["iconSize"]!.GetValue<double>();
                    if (g["smoothingEnabled"] is JsonValue) existing.SmoothingEnabled = g["smoothingEnabled"]!.GetValue<bool>();
                    if (g["smoothingFactor"] is JsonValue) existing.SmoothingFactor = g["smoothingFactor"]!.GetValue<double>();
                    if (g["smoothingResponseMs"] is JsonValue) existing.SmoothingResponseMs = g["smoothingResponseMs"]!.GetValue<double>();
                    if (g["spikeGatePercent"] is JsonValue) existing.SpikeGatePercent = g["spikeGatePercent"]!.GetValue<double>();
                    if (g["barValuePosition"] is JsonValue) existing.BarValuePosition = g["barValuePosition"]!.GetValue<int>();
                    if (g["barUnitPosition"] is JsonValue) existing.BarUnitPosition = g["barUnitPosition"]!.GetValue<int>();
                    if (g["barNamePosition"] is JsonValue) existing.BarNamePosition = g["barNamePosition"]!.GetValue<int>();
                    if (g["shapeCategory"] is JsonValue) existing.ShapeCategory = g["shapeCategory"]!.GetValue<int>();
                    if (gObj.ContainsKey("texturePath"))
                    {
                        var tp = g["texturePath"]?.GetValue<string>();
                        existing.TexturePath = string.IsNullOrEmpty(tp) ? null : tp;
                    }
                    if (g["colorStops"] is JsonArray csArr)
                    {
                        existing.ColorStops = csArr
                            .Where(c => c is JsonObject)
                            .Select(c => c!.AsObject())
                            .Select(c => new ColorStop
                            {
                                Fraction = c["fraction"]?.GetValue<double>() ?? 0,
                                R = c["r"]?.GetValue<int>() ?? 0,
                                G = c["g"]?.GetValue<int>() ?? 0,
                                B = c["b"]?.GetValue<int>() ?? 0,
                            })
                            .ToList();
                    }
                    if (g["colorHysteresis"] is JsonValue) existing.ColorHysteresis = g["colorHysteresis"]!.GetValue<double>();
                    if (g["chartTimeWindowSec"] is JsonValue) existing.ChartTimeWindowSec = g["chartTimeWindowSec"]!.GetValue<int>();
                    if (g["chartYMin"] is JsonValue) existing.ChartYMin = g["chartYMin"]!.GetValue<double>();
                    if (g["chartYMax"] is JsonValue) existing.ChartYMax = g["chartYMax"]!.GetValue<double>();
                    if (g["chartLineColor"] is JsonValue) existing.ChartLineColor = g["chartLineColor"]!.GetValue<string>();
                    if (g["chartLineWidth"] is JsonValue) existing.ChartLineWidth = g["chartLineWidth"]!.GetValue<double>();
                    if (g["chartShowGrid"] is JsonValue) existing.ChartShowGrid = g["chartShowGrid"]!.GetValue<bool>();
                    if (g["chartFillUnder"] is JsonValue) existing.ChartFillUnder = g["chartFillUnder"]!.GetValue<bool>();
                    if (g["chartShowLabels"] is JsonValue) existing.ChartShowLabels = g["chartShowLabels"]!.GetValue<bool>();
                    if (g["chartPrecision"] is JsonValue) existing.ChartPrecision = g["chartPrecision"]!.GetValue<int>();
                    if (g["textColor"] is JsonValue) existing.TextColor = g["textColor"]!.GetValue<string>();
                    if (g["zIndex"] is JsonValue) existing.ZIndex = g["zIndex"]!.GetValue<int>();
                    // Always clear first, then re-populate if present (handles empty array → null)
                    if (gObj.ContainsKey("transformSteps"))
                    {
                        existing.TransformSteps = null;
                        if (g["transformSteps"] is JsonArray tsArr && tsArr.Count > 0)
                        {
                            const int maxSteps = 20;
                            existing.TransformSteps = tsArr
                                .Where(t => t is JsonObject)
                                .Take(maxSteps)
                                .Select(t => t!.AsObject())
                                .Select(t =>
                                {
                                    var op = t["operation"] is JsonValue ov && ov.TryGetValue<int>(out var opVal) ? opVal : -1;
                                    if (op < 0 || op > (int)ValueTransformOperation.InvertSign) op = 0;
                                    var operand = t["operand"] is JsonValue jv && jv.TryGetValue<double>(out var opd) ? opd : 0;
                                    return new ValueTransformStep
                                    {
                                        Operation = (ValueTransformOperation)op,
                                        Operand = operand,
                                    };
                                })
                                .ToList();
                            if (existing.TransformSteps.Count == 0) existing.TransformSteps = null;
                        }
                    }
                    if (gObj.ContainsKey("customUnitLabel"))
                    {
                        var cul = g["customUnitLabel"] is JsonValue cv ? cv.GetValue<string>() : null;
                        if (!string.IsNullOrEmpty(cul) && cul.Length > 50) cul = cul[..50];
                        existing.CustomUnitLabel = string.IsNullOrEmpty(cul) ? null : cul;
                    }
                    if (g["showHistogram"] is JsonValue sh) existing.ShowHistogram = sh.GetValue<bool>();
                    // Linked entities (multi-entity gauges: Wedge, LED Ring, Multi-Ring)
                    if (gObj.ContainsKey("linkedEntities"))
                    {
                        existing.LinkedEntities = null;
                        if (g["linkedEntities"] is JsonArray leArr && leArr.Count > 0)
                        {
                            existing.LinkedEntities = leArr
                                .Where(le => le is JsonObject)
                                .Select(le => le!.AsObject())
                                .Select(le => new LinkedEntityEntry
                                {
                                    EntityId = le["entityId"]?.GetValue<int>() ?? 0,
                                    Color = le["color"]?.GetValue<string>(),
                                })
                                .ToList();
                            if (existing.LinkedEntities.Count == 0) existing.LinkedEntities = null;
                        }
                    }
                    // ── Gauge customization v2 ──
                    if (g["tickCount"] is JsonValue) existing.TickCount = g["tickCount"]!.GetValue<int>();
                    if (g["tickLabels"] is JsonValue) existing.TickLabels = g["tickLabels"]!.GetValue<bool>();
                    if (g["tickLabelEvery"] is JsonValue) existing.TickLabelEvery = g["tickLabelEvery"]!.GetValue<int>();
                    if (g["tickSide"] is JsonValue) existing.TickSide = g["tickSide"]!.GetValue<int>();
                    if (g["redlineStart"] is JsonValue) existing.RedlineStart = g["redlineStart"]!.GetValue<double>();
                    if (g["redlineWidth"] is JsonValue) existing.RedlineWidth = g["redlineWidth"]!.GetValue<double>();
                    if (g["redlineColor"] is JsonValue) existing.RedlineColor = g["redlineColor"]!.GetValue<string>();
                    if (g["needleShape"] is JsonValue) existing.NeedleShape = g["needleShape"]!.GetValue<int>();
                    if (g["barOrientation"] is JsonValue) existing.BarOrientation = g["barOrientation"]!.GetValue<int>();
                    if (g["barThickness"] is JsonValue) existing.BarThickness = g["barThickness"]!.GetValue<double>();
                    if (g["barTicks"] is JsonValue) existing.BarTicks = g["barTicks"]!.GetValue<bool>();
                    if (g["barMinMaxLabels"] is JsonValue) existing.BarMinMaxLabels = g["barMinMaxLabels"]!.GetValue<bool>();
                    if (g["barRedlineStart"] is JsonValue) existing.BarRedlineStart = g["barRedlineStart"]!.GetValue<double>();
                    if (g["barRedlineColor"] is JsonValue) existing.BarRedlineColor = g["barRedlineColor"]!.GetValue<string>();
                    if (g["colorStopColoring"] is JsonValue) existing.ColorStopColoring = g["colorStopColoring"]!.GetValue<bool>();
                    if (g["panelStyle"] is JsonValue) existing.PanelStyle = g["panelStyle"]!.GetValue<int>();
                    if (g["flashThreshold"] is JsonValue) existing.FlashThreshold = g["flashThreshold"]!.GetValue<double>();
                    if (g["ledColor"] is JsonValue) existing.LedColor = g["ledColor"]!.GetValue<string>();
                    if (g["digitBgColor"] is JsonValue) existing.DigitBgColor = g["digitBgColor"]!.GetValue<string>();
                    if (g["glowStrength"] is JsonValue) existing.GlowStrength = g["glowStrength"]!.GetValue<double>();
                    if (g["digitDecimals"] is JsonValue) existing.DigitDecimals = g["digitDecimals"]!.GetValue<int>();
                    if (g["zeroPadding"] is JsonValue) existing.ZeroPadding = g["zeroPadding"]!.GetValue<bool>();
                    if (g["minDigitCount"] is JsonValue) existing.MinDigitCount = g["minDigitCount"]!.GetValue<int>();
                    if (g["rollAnimation"] is JsonValue) existing.RollAnimation = g["rollAnimation"]!.GetValue<bool>();
                    if (g["rollSpeedMs"] is JsonValue) existing.RollSpeedMs = g["rollSpeedMs"]!.GetValue<double>();
                    if (g["segmentCount"] is JsonValue) existing.SegmentCount = g["segmentCount"]!.GetValue<int>();
                    if (g["segmentGap"] is JsonValue) existing.SegmentGap = g["segmentGap"]!.GetValue<double>();
                    if (g["ringStartAngle"] is JsonValue) existing.RingStartAngle = g["ringStartAngle"]!.GetValue<double>();
                    if (g["ringSweepAngle"] is JsonValue) existing.RingSweepAngle = g["ringSweepAngle"]!.GetValue<double>();
                    if (g["amberThreshold"] is JsonValue) existing.AmberThreshold = g["amberThreshold"]!.GetValue<double>();
                    if (g["redThreshold"] is JsonValue) existing.RedThreshold = g["redThreshold"]!.GetValue<double>();
                    if (g["ringCount"] is JsonValue) existing.RingCount = g["ringCount"]!.GetValue<int>();
                    if (g["ringWidth"] is JsonValue) existing.RingWidth = g["ringWidth"]!.GetValue<double>();
                    if (g["ringGap"] is JsonValue) existing.RingGap = g["ringGap"]!.GetValue<double>();
                    if (g["peakHoldEnabled"] is JsonValue) existing.PeakHoldEnabled = g["peakHoldEnabled"]!.GetValue<bool>();
                    if (g["peakHoldAutoResetSec"] is JsonValue) existing.PeakHoldAutoResetSec = g["peakHoldAutoResetSec"]!.GetValue<double>();
                    if (g["wedgeSegmentCount"] is JsonValue) existing.WedgeSegmentCount = g["wedgeSegmentCount"]!.GetValue<int>();
                    if (g["wedgeRedlineStart"] is JsonValue) existing.WedgeRedlineStart = g["wedgeRedlineStart"]!.GetValue<double>();
                    if (g["rampWidthRpm"] is JsonValue) existing.RampWidthRpm = g["rampWidthRpm"]!.GetValue<double>();
                    if (g["zoneCount"] is JsonValue) existing.ZoneCount = g["zoneCount"]!.GetValue<int>();
                    if (gObj.ContainsKey("chartOverlays"))
                    {
                        existing.ChartOverlays = null;
                        if (g["chartOverlays"] is JsonArray coArr && coArr.Count > 0)
                        {
                            existing.ChartOverlays = coArr
                                .Where(o => o is JsonObject)
                                .Take(5)
                                .Select(o => o!.AsObject())
                                .Select(o =>
                                {
                                    var style = o["lineStyle"] is JsonValue sv && sv.TryGetValue<int>(out var sVal) ? sVal : 0;
                                    if (style < 0 || style > 2) style = 0;
                                    return new ChartOverlayEntry
                                    {
                                        EntityId = o["entityId"]?.GetValue<int>() ?? 0,
                                        Color = o["color"]?.GetValue<string>(),
                                        LineWidth = o["lineWidth"] is JsonValue lw && lw.TryGetValue<double>(out var wVal) ? wVal : 1.5,
                                        LineStyle = style,
                                    };
                                })
                                .ToList();
                            if (existing.ChartOverlays.Count == 0) existing.ChartOverlays = null;
                        }
                    }
                    if (g["overlayPillPosition"] is JsonValue) existing.OverlayPillPosition = g["overlayPillPosition"]!.GetValue<int>();
                    if (g["overlayFontScale"] is JsonValue) existing.OverlayFontScale = g["overlayFontScale"]!.GetValue<double>();
                    if (g["chartLineStyle"] is JsonValue) existing.ChartLineStyle = g["chartLineStyle"]!.GetValue<int>();
                    if (g["chartBackgroundColor"] is JsonValue) existing.ChartBackgroundColor = g["chartBackgroundColor"]!.GetValue<string>();
                }
            }

            // Handle table layout updates
            var tableUpdates = data["tables"]?.AsArray();
            if (tableUpdates != null && tableUpdates.Count > 0)
            {
                dashboard.Tables ??= [];
                foreach (var t in tableUpdates)
                {
                    if (t is null) continue;
                    var tableId = t["tableId"]?.GetValue<int>() ?? 0;
                    var existing = dashboard.Tables.FirstOrDefault(x => x.TableId == tableId);
                    if (existing != null)
                    {
                        if (t["fractionX"] is JsonValue) existing.FractionX = t["fractionX"]!.GetValue<double>();
                        if (t["fractionY"] is JsonValue) existing.FractionY = t["fractionY"]!.GetValue<double>();
                        if (t["widthFraction"] is JsonValue) existing.WidthFraction = t["widthFraction"]!.GetValue<double>();
                        if (t["heightFraction"] is JsonValue) existing.HeightFraction = t["heightFraction"]!.GetValue<double>();
                        if (t["zIndex"] is JsonValue) existing.ZIndex = t["zIndex"]!.GetValue<int>();
                        if (t["colorScheme"] is JsonValue cs) existing.ColorScheme = cs.GetValue<string>();
                        if (t["showLabels"] is JsonValue sl) existing.ShowLabels = sl.GetValue<bool>();
                        if (t["showDimensionBadge"] is JsonValue sdb) existing.ShowDimensionBadge = sdb.GetValue<bool>();
                        if (t["traceXLink"] is JsonValue txl) existing.TraceXLink = txl.GetValue<int>();
                        if (t["traceYLink"] is JsonValue tyl) existing.TraceYLink = tyl.GetValue<int>();
                    }
                    else
                    {
                        dashboard.Tables.Add(new DashboardTableEntry
                        {
                            TableId = tableId,
                            FractionX = t["fractionX"]?.GetValue<double>() ?? 0.1,
                            FractionY = t["fractionY"]?.GetValue<double>() ?? 0.1,
                            WidthFraction = t["widthFraction"]?.GetValue<double>() ?? 0.25,
                            HeightFraction = t["heightFraction"]?.GetValue<double>() ?? 0.25,
                            ZIndex = t["zIndex"]?.GetValue<int>() ?? 0,
                            ColorScheme = t["colorScheme"]?.GetValue<string>(),
                            ShowLabels = t["showLabels"]?.GetValue<bool>(),
                            ShowDimensionBadge = t["showDimensionBadge"]?.GetValue<bool>(),
                            TraceXLink = t["traceXLink"]?.GetValue<int>(),
                            TraceYLink = t["traceYLink"]?.GetValue<int>(),
                        });
                    }
                }
            }

            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveDashboardLayout failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Persist per-dashboard view state: top-bar / sidebar visibility and layout lock.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveDashboardViewState', [json])
    /// payload: { dashboardName, headerVisible?, sidebarVisible?, layoutLocked? }
    /// </summary>
    public async Task<string> SaveDashboardViewState(string jsonPayload)
    {
        try
        {
            var data = JsonNode.Parse(jsonPayload)!;
            var dashboardName = data["dashboardName"]?.GetValue<string>() ?? "default";

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            config ??= new DashboardConfig();

            if (!config.Dashboards.TryGetValue(dashboardName, out var dashboard))
            {
                dashboard = new DashboardDefinition();
                config.Dashboards[dashboardName] = dashboard;
            }

            if (data["headerVisible"] is JsonValue hv) dashboard.HeaderVisible = hv.GetValue<bool>();
            if (data["sidebarVisible"] is JsonValue sv) dashboard.SidebarVisible = sv.GetValue<bool>();
            if (data["layoutLocked"] is JsonValue ll) dashboard.LayoutLocked = ll.GetValue<bool>();

            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveDashboardViewState failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    // ─── Sensor Selection / Gauge Config Methods ─────────────────────────────

    /// <summary>
    /// Get all available sensors from calibration with selection state and customizations.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetAvailableSensors', [dashboardName])
    /// </summary>
    public async Task<string> GetAvailableSensors(string dashboardName)
    {
        _logger.LogInformation("GetAvailableSensors called");
        try
        {
            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var links = calResult.Data?.DataLinks ?? [];

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            var savedDef = config?.Dashboards?.GetValueOrDefault(dashboardName);
            var selectedIds = savedDef?.Gauges.Select(g => g.Id).ToHashSet() ?? [];
            var customizations = savedDef?.Customizations ?? [];

            var ecuEntityIds = _connection.ProtocolInfo?.EntityMap?
                .Select(e => (int)e.Id).ToHashSet() ?? [];

            List<object> sensorList = [];
            foreach (var l in links)
            {
                customizations.TryGetValue(l.Id, out var c);
                var (defMin, defMax) = GetUnitDefaults(l.MeasureUnit);
                var effectiveMin = l.MinValue != 0 || l.MaxValue != 0 ? l.MinValue : (float)defMin;
                var effectiveMax = l.MinValue != 0 || l.MaxValue != 0 ? l.MaxValue : (float)defMax;
                sensorList.Add(new
                {
                    id = (int)l.Id,
                    name = l.Name,
                    category = l.Category,
                    unit = l.MeasureUnit,
                    minValue = effectiveMin,
                    maxValue = effectiveMax,
                    inEntityMap = ecuEntityIds.Contains((int)l.Id),
                    isSelected = selectedIds.Contains((int)l.Id),
                    customization = c is not null ? new
                    {
                        customName = c.CustomName,
                        customUnit = c.CustomUnit,
                        minRange = c.MinRange.HasValue ? (double)c.MinRange.Value : (double?)null,
                        maxRange = c.MaxRange.HasValue ? (double)c.MaxRange.Value : (double?)null,
                        minRangeBypass = c.MinRangeBypass,
                        maxRangeBypass = c.MaxRangeBypass,
                    } : null,
                });
            }

            if (_gps is { IsRunning: true })
            {
                sensorList.Add(new { id = -1001, name = "GPS Speed", category = "GPS", unit = "km/h", inEntityMap = true, isSelected = selectedIds.Contains(-1001), customization = (object?)null });
                sensorList.Add(new { id = -1002, name = "GPS Latitude", category = "GPS", unit = "\u00b0", inEntityMap = true, isSelected = selectedIds.Contains(-1002), customization = (object?)null });
                sensorList.Add(new { id = -1003, name = "GPS Longitude", category = "GPS", unit = "\u00b0", inEntityMap = true, isSelected = selectedIds.Contains(-1003), customization = (object?)null });
                sensorList.Add(new { id = -1004, name = "GPS Altitude", category = "GPS", unit = "m", inEntityMap = true, isSelected = selectedIds.Contains(-1004), customization = (object?)null });
                sensorList.Add(new { id = -1005, name = "GPS Course", category = "GPS", unit = "\u00b0", inEntityMap = true, isSelected = selectedIds.Contains(-1005), customization = (object?)null });
                sensorList.Add(new { id = -1006, name = "GPS Accuracy", category = "GPS", unit = "m", inEntityMap = true, isSelected = selectedIds.Contains(-1006), customization = (object?)null });
                sensorList.Add(new { id = -2001, name = "Odometer", category = "Odometer", unit = "km", inEntityMap = true, isSelected = selectedIds.Contains(-2001), customization = (object?)null });
            }

            // Derived entities — always available, with real picker metadata
            // mirroring S_derivedDefaults (R5: the countdown uses the fixed
            // nominal 0–9000 range, independent of any configured shift point).
            // Customizations are read back like the links block: without this,
            // a customized derived sensor (e.g. "Gear" renamed to "True Gear")
            // looks reset after a restart, and the next save rebuilds the dict
            // from the nulled entries — silently DELETING the customization.
            foreach (var (id, meta) in S_derivedDefaults.OrderBy(kv => kv.Key))
            {
                customizations.TryGetValue(id, out var c);
                sensorList.Add(new
                {
                    id,
                    name = meta.Name,
                    category = "Derived",
                    unit = meta.Unit,
                    minValue = meta.Min,
                    maxValue = meta.Max,
                    inEntityMap = true,
                    isSelected = selectedIds.Contains(id),
                    customization = c is not null ? new
                    {
                        customName = c.CustomName,
                        customUnit = c.CustomUnit,
                        minRange = c.MinRange.HasValue ? (double)c.MinRange.Value : (double?)null,
                        maxRange = c.MaxRange.HasValue ? (double)c.MaxRange.Value : (double?)null,
                        minRangeBypass = c.MinRangeBypass,
                        maxRangeBypass = c.MaxRangeBypass,
                    } : null,
                });
            }

            var totalCount = links.Count + (_gps is { IsRunning: true } ? 7 : 0) + S_derivedDefaults.Count;

            return JsonSerializer.Serialize(new
            {
                sensors = sensorList,
                selectedCount = selectedIds.Count,
                totalCount,
                gridRows = config?.GridRows ?? 4,
                gridColumns = config?.GridColumns ?? 7,
                backgroundImagePath = savedDef?.BackgroundImagePath
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAvailableSensors failed");
            return JsonSerializer.Serialize(new
            {
                sensors = Array.Empty<object>(),
                selectedCount = 0,
                totalCount = 0,
                gridRows = 4,
                gridColumns = 7,
                backgroundImagePath = (string?)null
            });
        }
    }

    /// <summary>
    /// Save sensor selection, customizations, and navigate to dashboard.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveSensorSelection', [jsonPayload])
    /// payload contains: { dashboardName, selectedIds, customizations, backgroundImagePath }
    /// </summary>
    public async Task<string> SaveSensorSelection(string jsonPayload)
    {
        _logger.LogInformation("SaveSensorSelection called");
        try
        {
            var data = JsonNode.Parse(jsonPayload)!;

            var dashboardName = data["dashboardName"]?.GetValue<string>() ?? "default";
            var selectedIds = data["selectedIds"]!.AsArray()
                .Select(n => (int)n!).ToHashSet();

            var customizationsDict = new Dictionary<int, SensorCustomization>();
            if (data["customizations"] is JsonObject customObj)
            {
                foreach (var kvp in customObj)
                {
                    var c = kvp.Value;
                    if (c is null) continue;
                    var id = int.Parse(kvp.Key);
                    customizationsDict[id] = new SensorCustomization
                    {
                        Id = id,
                        CustomName = c["customName"]?.GetValue<string>(),
                        CustomUnit = c["customUnit"]?.GetValue<string>(),
                        MinRange = c["minRange"] is JsonValue mv ? mv.GetValue<float>() : (float?)null,
                        MaxRange = c["maxRange"] is JsonValue xv ? xv.GetValue<float>() : (float?)null,
                    };
                }
            }

            string? backgroundImagePath = data["backgroundImagePath"]?.GetValue<string>();

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();
            var existingDef = config.Dashboards.GetValueOrDefault(dashboardName);
            var existingEntries = existingDef?.Gauges ?? [];

            var preserved = existingEntries.Where(e => selectedIds.Contains(e.Id)).ToList();
            var preservedIds = preserved.Select(e => e.Id).ToHashSet();
            var newEntries = selectedIds
                .Where(id => !preservedIds.Contains(id))
                .Select(id => new GaugeConfigEntry { Id = id })
                .ToList();
            var entries = preserved.Concat(newEntries).ToList();

            config.Dashboards[dashboardName] = new DashboardDefinition
            {
                Gauges = new System.Collections.ObjectModel.Collection<GaugeConfigEntry>(entries),
                Customizations = customizationsDict,
                BackgroundImagePath = backgroundImagePath,
                Odometer = existingDef?.Odometer,
                Vehicle = existingDef?.Vehicle,
                ShifterConfig = existingDef?.ShifterConfig,
                WarningHistory = existingDef?.WarningHistory ?? [],
            };

            config.ActiveDashboard = dashboardName;
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);

            // Enable reporting to start live data
            await EnableReporting().ConfigureAwait(false);

            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveSensorSelection failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Save dashboard table entries (add/remove tables from dashboard).
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveDashboardTables', [jsonPayload])
    /// payload contains: { dashboardName, tables: [{ tableId, fractionX, fractionY, widthFraction, heightFraction, zIndex }] }
    /// </summary>
    public async Task<string> SaveDashboardTables(string jsonPayload)
    {
        _logger.LogInformation("SaveDashboardTables called");
        try
        {
            var data = JsonNode.Parse(jsonPayload)!;
            var dashboardName = data["dashboardName"]?.GetValue<string>() ?? "default";

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            config ??= new DashboardConfig();

            if (!config.Dashboards.TryGetValue(dashboardName, out var dashboard))
            {
                dashboard = new DashboardDefinition();
                config.Dashboards[dashboardName] = dashboard;
            }

            var tableArray = data["tables"]?.AsArray();
            dashboard.Tables = [];
            if (tableArray != null)
            {
                foreach (var t in tableArray)
                {
                    if (t is null) continue;
                    dashboard.Tables.Add(new DashboardTableEntry
                    {
                        TableId = t["tableId"]?.GetValue<int>() ?? 0,
                        FractionX = t["fractionX"]?.GetValue<double>() ?? 0.1,
                        FractionY = t["fractionY"]?.GetValue<double>() ?? 0.1,
                        WidthFraction = t["widthFraction"]?.GetValue<double>() ?? 0.25,
                        HeightFraction = t["heightFraction"]?.GetValue<double>() ?? 0.25,
                        ZIndex = t["zIndex"]?.GetValue<int>() ?? 0,
                        TraceXLink = t["traceXLink"]?.GetValue<int>(),
                        TraceYLink = t["traceYLink"]?.GetValue<int>(),
                    });
                }
            }

            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveDashboardTables failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Delete gauges from a dashboard by entity IDs.
    /// Called from JS: window.HybridWebView.InvokeDotNet('DeleteDashboardGauges', [dashboardName, entityIdsJson])
    /// </summary>
    public async Task<string> DeleteDashboardGauges(string dashboardName, string entityIdsJson)
    {
        try
        {
            var ids = JsonSerializer.Deserialize<List<int>>(entityIdsJson);
            if (ids == null || ids.Count == 0)
                return JsonSerializer.Serialize(new { success = true });

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            if (config?.Dashboards.TryGetValue(dashboardName, out var dashboard) != true)
                return JsonSerializer.Serialize(new { success = true });

            var idSet = ids.ToHashSet();
            if (dashboard.Gauges.Count > 0)
            {
                var toRemove = dashboard.Gauges.Where(g => idSet.Contains(g.Id)).ToList();
                foreach (var g in toRemove) dashboard.Gauges.Remove(g);
            }

            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteDashboardGauges failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private static (double Min, double Max) GetUnitDefaults(string unit)
    {
        return unit switch
        {
            "%" => (0, 100),
            "KPa" or "kPa" => (0, 300),
            "PSI" or "psi" => (0, 45),
            "°C" => (-40, 150),
            "°F" => (-40, 300),
            "RPM" or "rpm" => (0, 8000),
            "V" or "v" => (0, 16),
            "°" => (0, 360),
            "ms" => (0, 25),
            "km/h" => (0, 260),
            "km" => (0, 999999),
            "m" => (0, 1000),
            "bar" => (0, 5),
            _ => (0, 10000),
        };
    }
}
