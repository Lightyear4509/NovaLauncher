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

public enum SaveConflictChoice { KeepLocal, KeepRemote, KeepBoth }

public sealed record SaveSnapshotPayload(
    SaveSnapshotManifest Manifest,
    IReadOnlyDictionary<string, byte[]> ChangedFiles);

public sealed record TransportResult(bool Success, bool Conflict, SaveSnapshotPayload? Snapshot, string? Error);
public sealed record PairingRedemptionResult(bool Success, Guid? PeerDeviceId, byte[]? Secret, string? Error, int RemainingAttempts = 0);

public interface ISaveSyncTransport : IAsyncDisposable
{
    bool IsConfigured { get; }
    bool IsListening { get; }
    string ListenerStatus { get; }
    Task StartAsync(ISaveSyncPeerEndpoint endpoint, CancellationToken cancellationToken);
    Task<TransportResult> PullAsync(GameId gameId, Guid? knownHead, CancellationToken cancellationToken);
    Task<TransportResult> PushAsync(SaveSnapshotPayload snapshot, CancellationToken cancellationToken);
    Task<PairingRedemptionResult> RedeemInvitationAsync(string code, Guid requestingDeviceId, CancellationToken cancellationToken);
}

public interface ISaveSyncPeerEndpoint
{
    Task<bool> AuthorizePeerAsync(Guid deviceId, CancellationToken cancellationToken);
    Task<PairingRedemptionResult> RedeemInvitationAsync(string code, Guid requestingDeviceId, CancellationToken cancellationToken);
    Task<TransportResult> ReceivePushAsync(SaveSnapshotPayload snapshot, CancellationToken cancellationToken);
    Task<TransportResult> ServePullAsync(GameId gameId, Guid? knownHead, CancellationToken cancellationToken);
}

public interface IPairingSecretStore
{
    bool HasSecret { get; }
    byte[]? GetSecret();
    void SetSecret(ReadOnlySpan<byte> secret);
    void Clear();
}

public interface ISaveSyncService
{
    SaveSyncSettings Settings { get; }
    bool IsPaired { get; }
    bool IsListening { get; }
    string ListenerStatus { get; }
    Task<string?> InitializeAsync(CancellationToken cancellationToken);
    Task<string> GeneratePairingCodeAsync(CancellationToken cancellationToken);
    Task<string?> ApplyPairingCodeAsync(string code, CancellationToken cancellationToken);
    Task<string?> RevokePeerAsync(CancellationToken cancellationToken);
    Task<string?> ConfigurePeerAsync(string address, CancellationToken cancellationToken);
    Task<string?> RetryListenerAsync(CancellationToken cancellationToken);
    (Guid? Identity, string? Error) DeriveSharedSaveIdentity(string label, string platform);
    Task<int> RetryPendingUploadsAsync(CancellationToken cancellationToken);
    Task<SaveSyncResult> PullBeforeLaunchAsync(LibraryItem game, CancellationToken cancellationToken);
    Task<SaveSyncResult> SnapshotAndPushAfterExitAsync(LibraryItem game, CancellationToken cancellationToken);
    Task<SaveSyncResult> ResolveConflictAsync(LibraryItem game, SaveConflictChoice choice, CancellationToken cancellationToken);
}
