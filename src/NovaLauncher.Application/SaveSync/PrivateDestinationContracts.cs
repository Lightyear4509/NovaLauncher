using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.SaveSync;

public enum PrivateDestinationKind { LocalFolder, WindowsNetworkShare }
public enum DestinationHealthOutcome { Succeeded, FailedClosed, Quarantined, Repaired }

public sealed record PrivateDestinationConfiguration(
    string RootPath,
    long StorageBudgetBytes,
    int RetainedSnapshotsPerGame,
    bool SyncNotesAndTags = false);

public sealed record DestinationPublishPreview(
    Guid PreviewId,
    GameId GameId,
    Guid SnapshotId,
    PrivateDestinationKind Kind,
    string DestinationDisplayPath,
    int FileCount,
    long SnapshotBytes,
    long CurrentDestinationBytes,
    long StorageBudgetBytes,
    IReadOnlyList<Guid> SnapshotsToRemove,
    long BytesToRemove,
    bool CanPublish,
    string Summary);

public sealed record DestinationRepairPreview(
    Guid PreviewId,
    GameId GameId,
    Guid SnapshotId,
    string DestinationDisplayPath,
    bool IsCurrentHead,
    Guid? VerifiedParentSnapshotId,
    string ProposedAction,
    bool CanRepair,
    string Summary);

public sealed record DestinationHealthEvent(
    Guid EventId,
    DateTimeOffset TimestampUtc,
    PrivateDestinationKind Kind,
    string Operation,
    DestinationHealthOutcome Outcome,
    GameId? GameId,
    Guid? SnapshotId,
    long Bytes,
    string Message);

public sealed record PrivateDestinationResult(bool Success, string Message);
public sealed record PrivateMetadataEntry(GameId GameId, string Name, string? Notes, IReadOnlyList<string> Tags, DateTimeOffset UpdatedAtUtc);
public sealed record PrivateMetadataPreview(Guid PreviewId, string Direction, PrivateMetadataEntry Entry, string Summary);

public interface IPrivateSnapshotDestinationService
{
    PrivateDestinationConfiguration? Configuration { get; }
    Task<PrivateDestinationResult> ConfigureAsync(PrivateDestinationConfiguration configuration, CancellationToken cancellationToken);
    Task<(DestinationPublishPreview? Preview, string? Error)> PreviewPublishAsync(GameId gameId, CancellationToken cancellationToken);
    Task<PrivateDestinationResult> PublishAsync(DestinationPublishPreview preview, CancellationToken cancellationToken);
    Task<(DestinationRepairPreview? Preview, string? Error)> PreviewQuarantineAsync(GameId gameId, Guid snapshotId, CancellationToken cancellationToken);
    Task<PrivateDestinationResult> QuarantineAsync(DestinationRepairPreview preview, CancellationToken cancellationToken);
    Task<IReadOnlyList<DestinationHealthEvent>> GetHealthHistoryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SaveSnapshotHistoryItem>> GetDestinationSnapshotsAsync(GameId gameId, CancellationToken cancellationToken);
    Task<(PrivateMetadataPreview? Preview, string? Error)> PreviewMetadataPushAsync(PrivateMetadataEntry entry, CancellationToken cancellationToken);
    Task<(PrivateMetadataPreview? Preview, string? Error)> PreviewMetadataPullAsync(GameId gameId, CancellationToken cancellationToken);
    Task<PrivateDestinationResult> CommitMetadataPushAsync(PrivateMetadataPreview preview, CancellationToken cancellationToken);
    Task<(PrivateMetadataEntry? Entry, string? Error)> CommitMetadataPullAsync(PrivateMetadataPreview preview, CancellationToken cancellationToken);
}
