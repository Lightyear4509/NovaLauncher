using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Achievements;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Achievements;

public sealed class AchievementService(
    IEnumerable<IAchievementProvider> providers,
    IDocumentStore<AchievementsDocument> store,
    LibraryCoordinator library,
    TimeProvider timeProvider,
    string accountFingerprint) : IAchievementService, IDisposable
{
    private static readonly TimeSpan Freshness = TimeSpan.FromHours(24);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AchievementsDocument? _document;

    public bool IsConfigured => providers.Any(static provider => provider.IsConfigured);

    public async Task<AchievementRefreshResult> GetAsync(GameId gameId, bool forceRefresh, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            var game = library.Games.FirstOrDefault(item => item.Id == gameId);
            if (game is null) return new(AchievementRefreshStatus.Failed, null, false, "The selected game no longer exists.");
            var cached = _document!.Games.FirstOrDefault(item => item.Achievements.GameId == gameId)?.Achievements;
            var age = cached is null ? TimeSpan.MaxValue : timeProvider.GetUtcNow() - cached.RefreshedAtUtc;
            if (!forceRefresh && cached is not null && age <= Freshness)
            {
                return new(AchievementRefreshStatus.Success, cached with { IsStale = false }, false, null);
            }

            var request = new AchievementRequest(game.Id, game.Source, game.SourceItemId);
            var provider = providers.FirstOrDefault(item => item.IsConfigured && item.CanHandle(request));
            if (provider is null)
            {
                return cached is not null && age <= Retention
                    ? new(AchievementRefreshStatus.Success, cached with { IsStale = true }, true, "Achievement provider is not configured; showing stale cache.")
                    : new(AchievementRefreshStatus.Unavailable, null, false, "Set NOVALAUNCHER_STEAM_WEB_API_KEY and NOVALAUNCHER_STEAM_ID to enable read-only Steam achievements.");
            }

            var result = await provider.GetAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.Status == AchievementRefreshStatus.Success && result.Achievements is not null)
            {
                var entries = _document.Games.Where(item => item.Achievements.GameId != gameId)
                    .Append(new AchievementCacheEntry(result.Achievements with { IsStale = false })).ToArray();
                var staged = _document with { Games = entries };
                var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
                if (save.Status != DocumentSaveStatus.Saved)
                {
                    return new(AchievementRefreshStatus.Failed, cached, cached is not null, save.Error ?? "Achievement cache could not be saved.");
                }

                _document = staged;
                return new(AchievementRefreshStatus.Success, result.Achievements, false, null);
            }

            return cached is not null && age <= Retention
                ? new(AchievementRefreshStatus.Success, cached with { IsStale = true }, true, result.Error)
                : new(result.Status, null, false, result.Error);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_document is not null) return;
        var load = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        _document = load.Document is { } existing && string.Equals(existing.AccountFingerprint, accountFingerprint, StringComparison.Ordinal)
            ? existing
            : AchievementsDocument.Empty(accountFingerprint);
    }

    public void Dispose() => _gate.Dispose();
}
