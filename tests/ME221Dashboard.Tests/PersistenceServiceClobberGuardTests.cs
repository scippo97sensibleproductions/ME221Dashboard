using System.Text.Json;
using FluentAssertions;
using ME221Dashboard.Services;
using Xunit;

namespace ME221Dashboard.Tests;

public sealed class PersistenceServiceClobberGuardTests
{
    private readonly Microsoft.Extensions.Logging.ILogger<PersistenceService> _logger =
        Microsoft.Extensions.Logging.Abstractions.NullLogger<PersistenceService>.Instance;

    [Fact]
    public void Fresh_default_config_detected()
    {
        var fresh = new DashboardConfig();
        PersistenceService.IsFreshDefaultOnly(fresh).Should().BeTrue();
    }

    [Fact]
    public void Config_with_real_dashboard_not_fresh()
    {
        var config = new DashboardConfig
        {
            ActiveDashboard = "Normal Dash",
        };
        config.Dashboards.Remove("default");
        config.Dashboards["Normal Dash"] = new DashboardDefinition();
        config.Dashboards["Normal Dash"].Gauges.Add(new ME221.Data.Models.GaugeConfigEntry { Id = 1 });

        PersistenceService.IsFreshDefaultOnly(config).Should().BeFalse();
    }

    [Fact]
    public void Default_named_dashboard_with_gauges_not_fresh()
    {
        var config = new DashboardConfig();
        config.Dashboards["default"].Gauges.Add(new ME221.Data.Models.GaugeConfigEntry { Id = 2 });

        PersistenceService.IsFreshDefaultOnly(config).Should().BeFalse();
    }

    [Fact]
    public void Has_real_dashboards_only_when_file_has_content()
    {
        var dir = Path.Combine(Path.GetTempPath(), "me221-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var missing = Path.Combine(dir, "missing.json");
            PersistenceService.HasRealDashboards(missing).Should().BeFalse();

            var freshPath = Path.Combine(dir, "fresh.json");
            File.WriteAllText(freshPath, JsonSerializer.Serialize(new DashboardConfig(), V2JsonContext.Default.DashboardConfig));
            PersistenceService.HasRealDashboards(freshPath).Should().BeFalse();

            var real = new DashboardConfig { ActiveDashboard = "Normal Dash" };
            real.Dashboards.Remove("default");
            real.Dashboards["Normal Dash"] = new DashboardDefinition();
            real.Dashboards["Normal Dash"].Gauges.Add(new ME221.Data.Models.GaugeConfigEntry { Id = 1 });
            var realPath = Path.Combine(dir, "real.json");
            File.WriteAllText(realPath, JsonSerializer.Serialize(real, V2JsonContext.Default.DashboardConfig));
            PersistenceService.HasRealDashboards(realPath).Should().BeTrue();

            var garbage = Path.Combine(dir, "garbage.json");
            File.WriteAllText(garbage, "{ not json");
            PersistenceService.HasRealDashboards(garbage).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Save_refuses_to_clobber_real_config_with_fresh_default()
    {
        var dir = Path.Combine(Path.GetTempPath(), "me221-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "dashboard-config.json");
            var real = new DashboardConfig { ActiveDashboard = "Normal Dash" };
            real.Dashboards.Remove("default");
            real.Dashboards["Normal Dash"] = new DashboardDefinition();
            real.Dashboards["Normal Dash"].Gauges.Add(new ME221.Data.Models.GaugeConfigEntry { Id = 1 });
            var svc = new PersistenceService(_logger);
            await svc.SaveDashboardConfigAsync(real, path);

            var before = File.ReadAllText(path);

            // The exact failure mode observed 2026-08-10 23:58:36: a caller whose
            // load failed transiently saves `new DashboardConfig()` over the real file.
            await svc.SaveDashboardConfigAsync(new DashboardConfig(), path);

            File.ReadAllText(path).Should().Be(before);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Save_fresh_default_allowed_when_no_real_config_exists()
    {
        var dir = Path.Combine(Path.GetTempPath(), "me221-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "dashboard-config.json");
            var svc = new PersistenceService(_logger);
            await svc.SaveDashboardConfigAsync(new DashboardConfig(), path);

            File.Exists(path).Should().BeTrue();
            var loaded = JsonSerializer.Deserialize(await File.ReadAllTextAsync(path), V2JsonContext.Default.DashboardConfig);
            loaded!.Dashboards.Keys.Should().Contain("default");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
