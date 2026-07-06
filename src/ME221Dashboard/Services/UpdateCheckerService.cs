using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services;

public class UpdateCheckerService : IUpdateCheckerService
{
    private static readonly HttpClient S_http = new();
    private readonly ILogger<UpdateCheckerService> _logger;

    private const string Owner = "scippo97sensibleproductions";
    private const string Repo = "ME221Dashboard";
    private const string LatestReleaseUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";

    public UpdateCheckerService(ILogger<UpdateCheckerService>? logger = null)
    {
        _logger = logger ?? NullLogger<UpdateCheckerService>.Instance;

        if (!S_http.DefaultRequestHeaders.Contains("User-Agent"))
            S_http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ME221Dashboard");
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var currentVersion = AppInfo.Current.VersionString;
        _logger.LogInformation("Checking for updates (current: {Version})", currentVersion);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            using var response = await S_http.GetAsync(LatestReleaseUrl, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: cts.Token).ConfigureAwait(false);
            if (release is null)
                return new UpdateCheckResult { UpdateAvailable = false, CurrentVersion = currentVersion };

            var latestTag = release.TagName?.TrimStart('v') ?? "";
            var releaseName = release.Name ?? latestTag;
            var releaseUrl = release.HtmlUrl ?? $"https://github.com/{Owner}/{Repo}/releases/latest";
            var publishedAt = release.PublishedAt?.ToString("yyyy-MM-dd");

            if (Version.TryParse(currentVersion, out var current) && Version.TryParse(latestTag, out var latest))
            {
                var available = latest > current;
                _logger.LogInformation("Update check: current={Current}, latest={Latest}, available={Available}",
                    current, latest, available);

                return new UpdateCheckResult
                {
                    UpdateAvailable = available,
                    CurrentVersion = currentVersion,
                    LatestVersion = latestTag,
                    ReleaseUrl = releaseUrl,
                    ReleaseName = releaseName,
                    PublishedAt = publishedAt,
                };
            }

            var simpleAvailable = !string.Equals(currentVersion, latestTag, StringComparison.OrdinalIgnoreCase);
            _logger.LogWarning("Version parsing failed (current={Current}, latest={Latest}), falling back to string comparison",
                currentVersion, latestTag);

            return new UpdateCheckResult
            {
                UpdateAvailable = simpleAvailable,
                CurrentVersion = currentVersion,
                LatestVersion = latestTag,
                ReleaseUrl = releaseUrl,
                ReleaseName = releaseName,
                PublishedAt = publishedAt,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update check failed");
            return new UpdateCheckResult { UpdateAvailable = false, CurrentVersion = currentVersion };
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }
    }
}
