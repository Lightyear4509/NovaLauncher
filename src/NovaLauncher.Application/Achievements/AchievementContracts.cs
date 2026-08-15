using NovaLauncher.Domain.Achievements;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Achievements;

public enum AchievementRefreshStatus
{
    Success,
    Unavailable,
    Offline,
    RateLimited,
    InvalidResponse,
    Failed,
}

public sealed record AchievementRequest(GameId GameId, string Source, string? SourceItemId);

public sealed record AchievementProviderResult(
    AchievementRefreshStatus Status,
    GameAchievements? Achievements,
    string? Error);

public interface IAchievementProvider
{
    string Id { get; }

    bool IsConfigured { get; }

    bool CanHandle(AchievementRequest request);

    Task<AchievementProviderResult> GetAsync(AchievementRequest request, CancellationToken cancellationToken);
}

public sealed record AchievementRefreshResult(
    AchievementRefreshStatus Status,
    GameAchievements? Achievements,
    bool UsedStaleCache,
    string? Error);

public interface IAchievementService
{
    bool IsConfigured { get; }

    Task<AchievementRefreshResult> GetAsync(GameId gameId, bool forceRefresh, CancellationToken cancellationToken);
}
