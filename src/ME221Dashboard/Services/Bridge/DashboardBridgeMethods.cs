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
            return JsonSerializer.Serialize(new { names, activeDashboard = config?.ActiveDashboard ?? "default" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDashboardNames failed");
            return JsonSerializer.Serialize(new { names = new[] { "default" }, activeDashboard = "default" });
        }
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
            name = name?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
                return JsonSerializer.Serialize(new { success = false, error = "Name is required" });

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();

            if (config.Dashboards.ContainsKey(name))
                return JsonSerializer.Serialize(new { success = false, error = "A dashboard with this name already exists" });

            config.Dashboards[name] = new DashboardDefinition();
            config.ActiveDashboard = name;
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);

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
                config.ActiveDashboard = config.Dashboards.Keys.First();

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
                config.ActiveDashboard = newName;

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
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            if (config?.Dashboards is null)
                return JsonSerializer.Serialize(new { success = false, error = "No config found" });

            if (!config.Dashboards.ContainsKey(name))
                return JsonSerializer.Serialize(new { success = false, error = "Dashboard not found" });

            config.ActiveDashboard = name;
            _activeDashboardName = name;
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
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
                    entityLookup[key] = new
                    {
                        name = cust?.CustomName is { Length: > 0 } cn ? cn : link.Name,
                        unit = cust?.CustomUnit is { Length: > 0 } cu ? cu : link.MeasureUnit,
                        minValue = cust?.MinRange.HasValue == true ? (double)cust.MinRange.Value : (double?)null,
                        maxValue = cust?.MaxRange.HasValue == true ? (double)cust.MaxRange.Value : (double?)null,
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
                }).ToList(),
                gridRows = config.GridRows,
                gridColumns = config.GridColumns,
                entities = entityLookup,
                backgroundImagePath = dashboard.BackgroundImagePath,
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
                sensorList.Add(new
                {
                    id = (int)l.Id,
                    name = l.Name,
                    category = l.Category,
                    unit = l.MeasureUnit,
                    inEntityMap = ecuEntityIds.Contains((int)l.Id),
                    isSelected = selectedIds.Contains((int)l.Id),
                    customization = c is not null ? new
                    {
                        customName = c.CustomName,
                        customUnit = c.CustomUnit,
                        minRange = c.MinRange.HasValue ? (double)c.MinRange.Value : (double?)null,
                        maxRange = c.MaxRange.HasValue ? (double)c.MaxRange.Value : (double?)null,
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

            // Derived entities — always available
            sensorList.Add(new { id = -3001, name = "Gear", category = "Derived", unit = "", inEntityMap = true, isSelected = selectedIds.Contains(-3001), customization = (object?)null });
            sensorList.Add(new { id = -3002, name = "True Speed", category = "Derived", unit = "km/h", inEntityMap = true, isSelected = selectedIds.Contains(-3002), customization = (object?)null });
            sensorList.Add(new { id = -3003, name = "Boost Pressure", category = "Derived", unit = "kPa", inEntityMap = true, isSelected = selectedIds.Contains(-3003), customization = (object?)null });
            sensorList.Add(new { id = -3004, name = "Speed Error", category = "Derived", unit = "km/h", inEntityMap = true, isSelected = selectedIds.Contains(-3004), customization = (object?)null });

            var totalCount = links.Count + (_gps is { IsRunning: true } ? 7 : 0) + 4;

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
}
