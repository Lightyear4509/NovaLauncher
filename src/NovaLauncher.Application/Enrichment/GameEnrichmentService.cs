using NovaLauncher.Application.Library;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Enrichment;

public sealed class GameEnrichmentService : IGameEnrichmentService
{
    private readonly IReadOnlyList<IMetadataProvider> _metadataProviders;
    private readonly IReadOnlyList<IArtworkProvider> _artworkProviders;
    private readonly ProviderCache<MetadataSnapshot[]> _metadataCache;
    private readonly ProviderCache<ArtworkCandidate[]> _artworkCache;
    private readonly IArtworkMaterializer _artworkMaterializer;
    private readonly LibraryCoordinator _library;

    public GameEnrichmentService(
        IEnumerable<IMetadataProvider> metadataProviders,
        IEnumerable<IArtworkProvider> artworkProviders,
        ProviderCache<MetadataSnapshot[]> metadataCache,
        ProviderCache<ArtworkCandidate[]> artworkCache,
        IArtworkMaterializer artworkMaterializer,
        LibraryCoordinator library)
    {
        _metadataProviders = metadataProviders.OrderBy(static provider => provider.Priority).ThenBy(static provider => provider.Id, StringComparer.Ordinal).ToArray();
        _artworkProviders = artworkProviders.OrderBy(static provider => provider.Priority).ThenBy(static provider => provider.Id, StringComparer.Ordinal).ToArray();
        _metadataCache = metadataCache;
        _artworkCache = artworkCache;
        _artworkMaterializer = artworkMaterializer;
        _library = library;
    }

    public async Task<ProviderRefreshResult> RefreshAsync(GameId gameId, bool forceRefresh, CancellationToken cancellationToken)
    {
        var game = _library.Games.FirstOrDefault(item => item.Id == gameId);
        if (game is null)
        {
            return new ProviderRefreshResult(ProviderResultStatus.Failed, null, false, false, [], "The selected game no longer exists.");
        }

        var request = new MetadataRequest(game.Id, game.Source, game.SourceItemId);
        var cacheKey = $"{game.Source.ToUpperInvariant()}:{game.SourceItemId ?? game.Id.ToString()}";
        var metadataLookup = forceRefresh
            ? new CacheLookup<MetadataSnapshot[]>(CacheLookupStatus.Miss, null)
            : _metadataCache.Get(cacheKey);
        var artworkLookup = forceRefresh
            ? new CacheLookup<ArtworkCandidate[]>(CacheLookupStatus.Miss, null)
            : _artworkCache.Get(cacheKey);
        if (!forceRefresh && metadataLookup.Status == CacheLookupStatus.Fresh && artworkLookup.Status == CacheLookupStatus.Fresh)
        {
            return await PublishAsync(game, metadataLookup.Value!, artworkLookup.Value!, true, false, [], cancellationToken).ConfigureAwait(false);
        }

        var failures = new List<string>();
        var snapshots = new List<MetadataSnapshot>();
        foreach (var provider in _metadataProviders.Where(provider => provider.CanHandle(request)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await provider.GetMetadataAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.Status == ProviderResultStatus.Success && result.Snapshot is not null)
            {
                snapshots.Add(result.Snapshot);
            }
            else if (result.Status != ProviderResultStatus.NoData)
            {
                failures.Add($"{provider.Id}: {result.Error ?? result.Status.ToString()}");
            }
        }

        var artwork = new List<ArtworkCandidate>();
        foreach (var provider in _artworkProviders.Where(provider => provider.CanHandle(request)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await provider.GetArtworkAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.Status == ProviderResultStatus.Success)
            {
                artwork.AddRange(result.Candidates);
            }
            else if (result.Status != ProviderResultStatus.NoData)
            {
                failures.Add($"{provider.Id}: {result.Error ?? result.Status.ToString()}");
            }
        }

        var usedStale = false;
        if (snapshots.Count == 0 && metadataLookup.Status == CacheLookupStatus.Stale)
        {
            snapshots.AddRange(metadataLookup.Value!);
            usedStale = true;
        }

        if (artwork.Count == 0 && artworkLookup.Status == CacheLookupStatus.Stale)
        {
            artwork.AddRange(artworkLookup.Value!);
            usedStale = true;
        }

        if (snapshots.Count == 0 && artwork.Count == 0)
        {
            return new ProviderRefreshResult(
                failures.Count > 0 ? ProviderResultStatus.Offline : ProviderResultStatus.NoData,
                game,
                false,
                usedStale,
                failures,
                failures.Count > 0 ? "Providers failed and no usable cache was available." : "No provider data was available.");
        }

        if (!usedStale)
        {
            if (snapshots.Count > 0) _metadataCache.Set(cacheKey, snapshots.ToArray());
            if (artwork.Count > 0) _artworkCache.Set(cacheKey, artwork.ToArray());
        }

        return await PublishAsync(game, snapshots, artwork, false, usedStale, failures, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProviderRefreshResult> PublishAsync(
        LibraryItem game,
        IReadOnlyList<MetadataSnapshot> snapshots,
        IReadOnlyList<ArtworkCandidate> artwork,
        bool usedCache,
        bool usedStale,
        IReadOnlyList<string> failures,
        CancellationToken cancellationToken)
    {
        var materialized = await _artworkMaterializer.MaterializeAsync(game.Id, artwork, cancellationToken).ConfigureAwait(false);
        var allFailures = failures.Concat(materialized.Failures).ToArray();
        var mergedMetadata = MetadataMerger.Merge(game.Metadata, snapshots);
        var mergedArtwork = MergeArtwork(game.Artwork, materialized.Candidates);
        var result = await _library.ApplyEnrichmentAsync(game.Id, mergedMetadata, mergedArtwork, cancellationToken).ConfigureAwait(false);
        if (result.Status != LibraryMutationStatus.Saved)
        {
            await _artworkMaterializer.RollbackAsync(materialized.CreatedFiles, CancellationToken.None).ConfigureAwait(false);
            return new ProviderRefreshResult(ProviderResultStatus.Failed, game, usedCache, usedStale, allFailures, result.Error);
        }

        await _artworkMaterializer.CleanupObsoleteAsync(game.Artwork, mergedArtwork, CancellationToken.None).ConfigureAwait(false);
        return new ProviderRefreshResult(ProviderResultStatus.Success, result.Item, usedCache, usedStale, allFailures, null);
    }

    private static GameArtwork MergeArtwork(GameArtwork? current, IReadOnlyList<ArtworkCandidate> candidates) => new(
        Select(ArtworkKind.Cover, current?.Cover, candidates),
        Select(ArtworkKind.Hero, current?.Hero, candidates),
        Select(ArtworkKind.Logo, current?.Logo, candidates),
        Select(ArtworkKind.Background, current?.Background, candidates));

    private static ArtworkReference Select(ArtworkKind kind, ArtworkReference? current, IReadOnlyList<ArtworkCandidate> candidates)
    {
        if (current?.Provenance.IsManual == true)
        {
            return current;
        }

        var candidate = candidates.FirstOrDefault(item => item.Kind == kind);
        return candidate is null
            ? current ?? Placeholder(kind)
            : new ArtworkReference(
                kind,
                candidate.Location,
                new MetadataProvenance(candidate.ProviderId, candidate.ProviderItemId, candidate.RetrievedAtUtc, false),
                false);
    }

    private static ArtworkReference Placeholder(ArtworkKind kind) =>
        new(kind, $"placeholder://{kind.ToString().ToLowerInvariant()}", new MetadataProvenance("NovaLauncher", null, DateTimeOffset.UnixEpoch, false), true);
}
