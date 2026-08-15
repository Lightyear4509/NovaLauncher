using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Enrichment;

public enum ProviderResultStatus
{
    Success,
    NoData,
    Offline,
    RateLimited,
    InvalidResponse,
    Failed,
}

public sealed record MetadataRequest(GameId GameId, string Source, string? SourceItemId);

public sealed record MetadataSnapshot(
    string ProviderId,
    string? ProviderItemId,
    string? Description,
    IReadOnlyList<string>? Genres,
    IReadOnlyList<string>? Developers,
    IReadOnlyList<string>? Publishers,
    DateOnly? ReleaseDate,
    decimal? Rating,
    DateTimeOffset RetrievedAtUtc);

public sealed record MetadataProviderResult(
    string ProviderId,
    ProviderResultStatus Status,
    MetadataSnapshot? Snapshot,
    string? Error);

public interface IMetadataProvider
{
    string Id { get; }

    int Priority { get; }

    bool CanHandle(MetadataRequest request);

    Task<MetadataProviderResult> GetMetadataAsync(MetadataRequest request, CancellationToken cancellationToken);
}

public sealed record ArtworkCandidate(
    ArtworkKind Kind,
    string Location,
    string ProviderId,
    string? ProviderItemId,
    DateTimeOffset RetrievedAtUtc);

public sealed record ArtworkProviderResult(
    string ProviderId,
    ProviderResultStatus Status,
    IReadOnlyList<ArtworkCandidate> Candidates,
    string? Error);

public interface IArtworkProvider
{
    string Id { get; }

    int Priority { get; }

    bool CanHandle(MetadataRequest request);

    Task<ArtworkProviderResult> GetArtworkAsync(MetadataRequest request, CancellationToken cancellationToken);
}

public sealed record ArtworkMaterializationResult(
    IReadOnlyList<ArtworkCandidate> Candidates,
    IReadOnlyList<string> CreatedFiles,
    IReadOnlyList<string> Failures);

public interface IArtworkMaterializer
{
    Task<ArtworkMaterializationResult> MaterializeAsync(
        GameId gameId,
        IReadOnlyList<ArtworkCandidate> candidates,
        CancellationToken cancellationToken);

    Task RollbackAsync(IReadOnlyList<string> createdFiles, CancellationToken cancellationToken);

    Task CleanupObsoleteAsync(
        GameArtwork? previous,
        GameArtwork current,
        CancellationToken cancellationToken);
}

public sealed record ManualCoverImportResult(ArtworkReference? Cover, string? CreatedPath, string? Error);

public interface IManualCoverService
{
    Task<ManualCoverImportResult> ImportAsync(GameId gameId, string sourcePath, CancellationToken cancellationToken);

    Task DeleteManagedAsync(string location, CancellationToken cancellationToken);
}

public sealed record ProviderRefreshResult(
    ProviderResultStatus Status,
    LibraryItem? Item,
    bool UsedCache,
    bool UsedStaleCache,
    IReadOnlyList<string> ProviderFailures,
    string? Error);

public interface IGameEnrichmentService
{
    Task<ProviderRefreshResult> RefreshAsync(
        GameId gameId,
        bool forceRefresh,
        CancellationToken cancellationToken);
}
