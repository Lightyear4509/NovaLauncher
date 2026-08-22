using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;

namespace NovaLauncher.Application.SaveSync;

public enum SaveSyncStatus
{
    Unchanged,
    SnapshotCreated,
    Applied,
    QueuedOffline,
    Conflict,
    Unavailable,
    Failed,
}

public sealed record SaveSyncResult(SaveSyncStatus Status, string Message, Guid? SnapshotId = null);
public sealed record SaveSnapshotHistoryItem(
    Guid SnapshotId,
    Guid? ParentSnapshotId,
    Guid DeviceId,
    DateTimeOffset CreatedAtUtc,
    int FileCount,
    long TotalBytes,
    bool IsHead,
    bool IntegrityValid,
    string? Error);
public sealed record SaveRestoreHistoryItem(
    Guid OperationId,
    GameId GameId,
    Guid SourceSnapshotId,
    DateTimeOffset CreatedAtUtc,
    int BackedUpFileCount,
    long BackedUpBytes,
    string Outcome);

public enum SaveConflictChoice { KeepLocal, KeepRemote, KeepBoth }

public sealed record SaveSnapshotPayload(
    SaveSnapshotManifest Manifest,
    IReadOnlyDictionary<string, byte[]> ChangedFiles);

public sealed record TransportResult(bool Success, bool Conflict, SaveSnapshotPayload? Snapshot, string? Error);
public sealed record PairingRedemptionResult(bool Success, Guid? PeerDeviceId, byte[]? Secret, string? Error, int RemainingAttempts = 0);
public sealed record SavePushBeginResult(bool Success, IReadOnlyDictionary<string, long> ResumeOffsets, string? Error);
public sealed record SavePushChunkResult(bool Success, long NextOffset, string? Error);
public sealed record SavePullBeginResult(bool Success, SaveSnapshotManifest? Manifest, string? Error);
public sealed record SavePullChunkResult(bool Success, byte[]? Bytes, bool EndOfFile, string? Error);
public sealed record SaveTransferProgress(
    string DeviceName,
    string Direction,
    string FilePath,
    long BytesTransferred,
    long TotalBytes,
    double BytesPerSecond,
    TimeSpan? EstimatedRemaining,
    string Status);
public sealed record SaveConflictComparisonItem(
    string RelativePath,
    long? LocalBytes,
    long? RemoteBytes,
    string Difference);

public interface ISaveSyncTransport : IAsyncDisposable
{
    event Action<SaveTransferProgress>? ProgressChanged;
    bool IsConfigured { get; }
    bool IsListening { get; }
    string ListenerStatus { get; }
    Task StartAsync(ISaveSyncPeerEndpoint endpoint, CancellationToken cancellationToken);
    Task<TransportResult> PullAsync(GameId gameId, Guid? knownHead, CancellationToken cancellationToken);
    Task<TransportResult> PushAsync(SaveSnapshotPayload snapshot, CancellationToken cancellationToken);
    Task<TransportResult> PullAsync(TrustedSaveSyncPeer peer, GameId gameId, Guid? knownHead, CancellationToken cancellationToken);
    Task<TransportResult> PushAsync(TrustedSaveSyncPeer peer, SaveSnapshotPayload snapshot, CancellationToken cancellationToken);
    Task<PairingRedemptionResult> RedeemInvitationAsync(string code, Guid requestingDeviceId, CancellationToken cancellationToken);
    Task<string?> RotatePeerCredentialAsync(TrustedSaveSyncPeer peer, byte[] newSecret, CancellationToken cancellationToken);
    Task<string?> CancelPartialTransfersAsync(CancellationToken cancellationToken);
}

public interface ISaveSyncPeerEndpoint
{
    Task<bool> AuthorizePeerAsync(Guid deviceId, CancellationToken cancellationToken);
    Task<PairingRedemptionResult> RedeemInvitationAsync(string code, Guid requestingDeviceId, CancellationToken cancellationToken);
    Task<TransportResult> ReceivePushAsync(SaveSnapshotPayload snapshot, CancellationToken cancellationToken);
    Task<TransportResult> ServePullAsync(GameId gameId, Guid? knownHead, CancellationToken cancellationToken);
    Task<SavePushBeginResult> BeginIncomingSnapshotAsync(Guid requestingDeviceId, SaveSnapshotManifest manifest, IReadOnlyList<string> changedPaths, CancellationToken cancellationToken);
    Task<SavePushChunkResult> ReceiveIncomingSnapshotChunkAsync(Guid requestingDeviceId, Guid snapshotId, string relativePath, long offset, byte[] bytes, CancellationToken cancellationToken);
    Task<TransportResult> CompleteIncomingSnapshotAsync(Guid requestingDeviceId, Guid snapshotId, CancellationToken cancellationToken);
    Task<SavePullBeginResult> BeginOutgoingSnapshotAsync(Guid requestingDeviceId, GameId gameId, Guid? knownHead, CancellationToken cancellationToken);
    Task<SavePullChunkResult> ReadOutgoingSnapshotChunkAsync(Guid requestingDeviceId, Guid snapshotId, string relativePath, long offset, int maximumBytes, CancellationToken cancellationToken);
    Task<string?> PrepareCredentialRotationAsync(Guid requestingDeviceId, byte[] newSecret, CancellationToken cancellationToken);
    Task<string?> CommitCredentialRotationAsync(Guid requestingDeviceId, CancellationToken cancellationToken);
    Task<string?> CancelIncomingPartialTransfersAsync(Guid requestingDeviceId, CancellationToken cancellationToken);
}

public interface IPairingSecretStore
{
    bool HasSecret { get; }
    byte[]? GetSecret();
    void SetSecret(ReadOnlySpan<byte> secret);
    void Clear();
}

public interface IPeerCredentialStore
{
    bool ContainsSecret(Guid peerDeviceId);
    byte[]? GetSecret(Guid peerDeviceId);
    void SetSecret(Guid peerDeviceId, ReadOnlySpan<byte> secret);
    void Clear(Guid peerDeviceId);
    string GetCredentialReference(Guid peerDeviceId);
    bool ContainsPendingSecret(Guid peerDeviceId);
    byte[]? GetPendingSecret(Guid peerDeviceId);
    void SetPendingSecret(Guid peerDeviceId, ReadOnlySpan<byte> secret);
    void PromotePendingSecret(Guid peerDeviceId);
    void ClearPendingSecret(Guid peerDeviceId);
}

public interface ISaveSyncService
{
    event Action<SaveTransferProgress>? TransferProgressChanged;
    SaveSyncSettings Settings { get; }
    bool IsPaired { get; }
    bool IsListening { get; }
    string ListenerStatus { get; }
    Task<string?> InitializeAsync(CancellationToken cancellationToken);
    Task<string> GeneratePairingCodeAsync(CancellationToken cancellationToken);
    Task<string?> ApplyPairingCodeAsync(string code, CancellationToken cancellationToken);
    Task<string?> RevokePeerAsync(CancellationToken cancellationToken);
    Task<string?> RenamePeerAsync(Guid peerDeviceId, string displayName, CancellationToken cancellationToken);
    Task<string?> SetPeerPausedAsync(Guid peerDeviceId, bool paused, CancellationToken cancellationToken);
    Task<string?> RevokePeerAsync(Guid peerDeviceId, CancellationToken cancellationToken);
    Task<string?> RotatePeerCredentialAsync(Guid peerDeviceId, CancellationToken cancellationToken);
    Task<string?> ConfigurePeerAsync(string address, CancellationToken cancellationToken);
    Task<string?> RetryListenerAsync(CancellationToken cancellationToken);
    Task<string?> CancelPartialTransfersAsync(CancellationToken cancellationToken);
    (Guid? Identity, string? Error) DeriveSharedSaveIdentity(string label, string platform);
    Task<int> RetryPendingUploadsAsync(CancellationToken cancellationToken);
    Task<SaveSyncResult> PullBeforeLaunchAsync(LibraryItem game, CancellationToken cancellationToken);
    Task<SaveSyncResult> SnapshotAndPushAfterExitAsync(LibraryItem game, CancellationToken cancellationToken);
    Task<SaveSyncResult> ResolveConflictAsync(LibraryItem game, SaveConflictChoice choice, CancellationToken cancellationToken);
    Task<IReadOnlyList<SaveConflictComparisonItem>> GetConflictComparisonAsync(LibraryItem game, CancellationToken cancellationToken);
    Task<IReadOnlyList<SaveSnapshotHistoryItem>> GetSnapshotHistoryAsync(GameId gameId, CancellationToken cancellationToken);
    Task<SaveSyncResult> VerifySnapshotsAsync(GameId gameId, CancellationToken cancellationToken);
    Task<SaveSyncResult> RestoreSnapshotAsync(LibraryItem game, Guid snapshotId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SaveRestoreHistoryItem>> GetRestoreHistoryAsync(GameId gameId, CancellationToken cancellationToken);
}
