using NovaLauncher.Domain.Library;

namespace NovaLauncher.Domain.SaveSync;

public sealed record SaveFileEntry(string RelativePath, long Length, string Sha256);

public sealed record SaveSnapshotManifest(
    Guid SnapshotId,
    Guid? ParentSnapshotId,
    GameId GameId,
    Guid DeviceId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<SaveFileEntry> Files,
    IReadOnlyList<string> DeletedPaths);

public sealed record SaveSyncGameState(
    GameId GameId,
    Guid? HeadSnapshotId,
    IReadOnlyList<SaveFileEntry> LastObservedFiles,
    string Status,
    string? ConflictSnapshotId = null,
    IReadOnlyList<Guid>? DestinationPeerIds = null,
    DateTimeOffset? LastSuccessAtUtc = null,
    string? LastError = null);

public enum TrustedPeerState
{
    Active,
    Paused,
    Revoked,
}

public sealed record TrustedSaveSyncPeer(
    Guid DeviceId,
    string DisplayName,
    string Address,
    string CredentialReference,
    TrustedPeerState State,
    DateTimeOffset PairedAtUtc,
    DateTimeOffset? LastSeenAtUtc = null);

public sealed record SaveSyncSettings(
    Guid DeviceId,
    string DeviceName,
    string? PeerAddress,
    Guid? PeerDeviceId,
    int Port,
    IReadOnlyList<SaveSyncGameState> Games,
    Guid? PendingInvitationId = null,
    DateTimeOffset? PendingInvitationExpiresAtUtc = null,
    Guid? LastConsumedInvitationId = null,
    string? PendingCodeSalt = null,
    string? PendingCodeHash = null,
    int PendingCodeFailedAttempts = 0,
    IReadOnlyList<TrustedSaveSyncPeer>? TrustedPeers = null)
{
    public const int DefaultPort = 47471;
    public const int MaximumTrustedPeers = 8;

    public IReadOnlyList<TrustedSaveSyncPeer> EffectiveTrustedPeers => TrustedPeers ?? [];

    public static SaveSyncSettings CreateDefault() =>
        new(Guid.NewGuid(), Environment.MachineName, null, null, DefaultPort, []);
}
