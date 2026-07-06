namespace ME221Dashboard.Services;

public interface IUpdateCheckerService
{
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default);
}

public record UpdateCheckResult
{
    public bool UpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = "";
    public string LatestVersion { get; init; } = "";
    public string ReleaseUrl { get; init; } = "";
    public string ReleaseName { get; init; } = "";
    public string? PublishedAt { get; init; }
}
