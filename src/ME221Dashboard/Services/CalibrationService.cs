using System.Text.RegularExpressions;
using ME221.Comms;
using ME221.Comms.Messages;
using ME221.Data.Infrastructure;
using ME221.Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services;

public sealed class CalibrationService(IPersistenceService persistence, ILogger<CalibrationService>? logger = null)
    : ICalibrationService
{
    private readonly ILogger<CalibrationService> _logger = logger ?? NullLogger<CalibrationService>.Instance;

    public async Task<CalibrationData> LoadAndParseAsync(Stream mefwStream)
    {
        ArgumentNullException.ThrowIfNull(mefwStream);

        using var ms = new MemoryStream();
        await mefwStream.CopyToAsync(ms).ConfigureAwait(false);
        var bytes = ms.ToArray();
        return await Task.Run(() =>
        {
            var xml = MefwReader.ReadDefXml(bytes);
            return DefXmlParser.Parse(xml);
        }).ConfigureAwait(false);
    }

    public Task<CalibrationResult> GetPersistedCalibrationAsync() => persistence.LoadCalibrationAsync();
    public Task<DashboardConfig?> GetPersistedDashboardConfigAsync() => persistence.LoadDashboardConfigAsync();
    public Task SaveCalibrationAsync(CalibrationData calibration) => persistence.SaveCalibrationAsync(calibration);
    public Task SaveDashboardConfigAsync(DashboardConfig config) => persistence.SaveDashboardConfigAsync(config);

    public async Task<(string Product, string Model, string Version)> GetEcuInfoAsync(ProtocolService protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        var response = await protocol.SendAsync<GetEcuInfoResponse>(new GetEcuInfoRequest()).ConfigureAwait(false);
        return (response.ProductName, response.ModelName, response.Version);
    }

    public bool MatchesEcu(CalibrationData calibration, string product, string model, string version)
    {
        if (calibration?.Metadata == null) return false;

        string s1 = Clean(calibration.Metadata.ProductName);
        string s2 = Clean(product);
        string m1 = Clean(calibration.Metadata.ModelName);
        string m2 = Clean(model);
        string v1 = Clean(calibration.Metadata.Version);
        string v2 = Clean(version);

        bool prodMatch = s1 == s2 || s1.Contains(s2, StringComparison.Ordinal) || s2.Contains(s1, StringComparison.Ordinal);
        bool verMatch = v1 == v2 || v1.Contains(v2, StringComparison.Ordinal) || v2.Contains(v1, StringComparison.Ordinal);

        bool modelMatch = string.Equals(m1, "GENERIC", StringComparison.Ordinal)
            || string.Equals(m2, "GENERIC", StringComparison.Ordinal)
            || m1 == m2
            || m1.Contains(m2, StringComparison.Ordinal)
            || m2.Contains(m1, StringComparison.Ordinal);

        _logger.LogDebug("Match Result: Prod={P}, Model={M}, Ver={V}", prodMatch, modelMatch, verMatch);
        return prodMatch && modelMatch && verMatch;
    }

    private static string Clean(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var temp = input.ToUpperInvariant().Trim().TrimStart('v');
        return Regex.Replace(temp, "[^A-Z0-9]", "");
    }
}
