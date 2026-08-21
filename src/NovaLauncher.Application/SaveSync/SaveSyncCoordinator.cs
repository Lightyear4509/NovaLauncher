using System.Security.Cryptography;
using System.Text;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;

namespace NovaLauncher.Application.SaveSync;

public sealed class SaveSyncCoordinator(
    IDocumentStore<SaveSyncDocument> store,
    ISaveSyncTransport transport,
    IPairingSecretStore secrets,
    TimeProvider timeProvider,
    string dataRoot,
    TimeSpan? quietPeriod = null,
    IPeerCredentialStore? peerCredentials = null) : ISaveSyncService, ISaveSyncPeerEndpoint, IDisposable
{
    public const int MaximumFiles = 20_000;
    public const long MaximumFileBytes = 64L * 1024 * 1024;
    public const long MaximumSnapshotBytes = 512L * 1024 * 1024;
    public const int MaximumRetainedSnapshotsPerGame = 20;
    public const int MaximumRetainedRestoreBackupsPerGame = 10;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _identityGate = new(1, 1);
    private SaveSyncDocument _document = SaveSyncDocument.CreateDefault();
    private readonly string _snapshotRoot = Path.Combine(dataRoot, "SaveSync", "Snapshots");
    private readonly TimeSpan _quietPeriod = quietPeriod ?? TimeSpan.FromSeconds(2);

    public SaveSyncSettings Settings => _document.Settings;
    public bool IsPaired => ActivePeers().Length > 0;
    public bool IsListening => transport.IsListening;
    public string ListenerStatus => transport.ListenerStatus;
    private bool HasActiveCredentials => IsPaired || (secrets.HasSecret &&
        Settings.PendingInvitationId is not null && Settings.PendingInvitationExpiresAtUtc > timeProvider.GetUtcNow());

    public async Task<string?> InitializeAsync(CancellationToken cancellationToken)
    {
        var load = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        _document = load.Document ?? SaveSyncDocument.CreateDefault();
        if (load.Status == DocumentLoadStatus.NotFound)
        {
            var save = await store.SaveAsync(_document, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved) return save.Error ?? "Save-sync identity could not be created.";
        }
        if (Settings.PeerDeviceId is { } legacyPeer &&
            Settings.EffectiveTrustedPeers.Count == 0 &&
            TailscalePeerValidator.TryNormalize(Settings.PeerAddress ?? string.Empty, out var legacyAddress, out _))
        {
            var credentialReference = "NovaLauncher.SaveSync.LegacyPeer";
            var copiedCredential = false;
            var legacySecret = secrets.GetSecret();
            if (peerCredentials is not null && legacySecret is { Length: 32 })
            {
                peerCredentials.SetSecret(legacyPeer, legacySecret);
                credentialReference = peerCredentials.GetCredentialReference(legacyPeer);
                copiedCredential = true;
            }
            if (legacySecret is not null) CryptographicOperations.ZeroMemory(legacySecret);
            var migrated = _document with
            {
                Settings = Settings with
                {
                    TrustedPeers =
                    [
                        new TrustedSaveSyncPeer(
                            legacyPeer,
                            $"Paired device {legacyPeer.ToString("N", System.Globalization.CultureInfo.InvariantCulture)[..8]}",
                            legacyAddress,
                            credentialReference,
                            TrustedPeerState.Active,
                            timeProvider.GetUtcNow()),
                    ],
                },
            };
            var migrationSave = await store.SaveAsync(migrated, cancellationToken).ConfigureAwait(false);
            if (migrationSave.Status != DocumentSaveStatus.Saved)
            {
                if (copiedCredential) peerCredentials!.Clear(legacyPeer);
                return migrationSave.Error ?? "The existing paired device could not be migrated safely.";
            }
            _document = migrated;
        }
        if (!IsPaired && Settings.PendingInvitationExpiresAtUtc is { } expired && expired <= timeProvider.GetUtcNow())
        {
            secrets.Clear();
            var cleared = _document with
            {
                Settings = Settings with
                {
                    PendingInvitationId = null,
                    PendingInvitationExpiresAtUtc = null,
                    PendingCodeSalt = null,
                    PendingCodeHash = null,
                    PendingCodeFailedAttempts = 0,
                },
            };
            var clearSave = await store.SaveAsync(cleared, cancellationToken).ConfigureAwait(false);
            if (clearSave.Status != DocumentSaveStatus.Saved) return clearSave.Error ?? "An expired pairing invitation could not be cleared safely.";
            _document = cleared;
        }
        if (HasActiveCredentials)
        {
            try
            {
                await transport.StartAsync(this, cancellationToken).ConfigureAwait(false);
                foreach (var pending in Settings.Games.Where(static game => game.Status == "Pending upload" && game.HeadSnapshotId is not null).ToArray())
                {
                    var payload = await LoadSnapshotAsync(pending.GameId, pending.HeadSnapshotId!.Value, fullContent: false, cancellationToken).ConfigureAwait(false);
                    if (payload is null) continue;
                    var pushed = await PushToActivePeersAsync(payload, cancellationToken).ConfigureAwait(false);
                    if (pushed.Success) await SetStateAsync(pending with { Status = "Synchronized" }, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException exception) { return $"Save sync is configured but inactive: {exception.Message}"; }
        }
        return load.Warning;
    }

    public async Task<string> GeneratePairingCodeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Settings.EffectiveTrustedPeers.Count(peer => peer.State != TrustedPeerState.Revoked) >= SaveSyncSettings.MaximumTrustedPeers)
            throw new InvalidOperationException($"At most {SaveSyncSettings.MaximumTrustedPeers} trusted devices may be retained.");
        var secret = RandomNumberGenerator.GetBytes(32);
        var invitationId = Guid.NewGuid();
        var expires = timeProvider.GetUtcNow().AddHours(24);
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var salt = RandomNumberGenerator.GetBytes(16);
        var codeHash = HashPairingCode(code, salt);
        var staged = _document with
        {
            Settings = Settings with
            {
                PendingInvitationId = invitationId,
                PendingInvitationExpiresAtUtc = expires,
                PendingCodeSalt = Convert.ToBase64String(salt),
                PendingCodeHash = Convert.ToBase64String(codeHash),
                PendingCodeFailedAttempts = 0,
            },
        };
        var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
        if (save.Status != DocumentSaveStatus.Saved) throw new IOException(save.Error ?? "The invitation could not be persisted.");
        _document = staged;
        try { secrets.SetSecret(secret); }
        catch { secrets.Clear(); throw; }
        await EnsureTransportStartedAsync(cancellationToken).ConfigureAwait(false);
        CryptographicOperations.ZeroMemory(secret);
        CryptographicOperations.ZeroMemory(salt);
        CryptographicOperations.ZeroMemory(codeHash);
        return $"{code[..3]} {code[3..]}";
    }

    public async Task<string?> ApplyPairingCodeAsync(string code, CancellationToken cancellationToken)
    {
        if (Settings.EffectiveTrustedPeers.Count(peer => peer.State != TrustedPeerState.Revoked) >= SaveSyncSettings.MaximumTrustedPeers)
            return $"At most {SaveSyncSettings.MaximumTrustedPeers} trusted devices may be retained.";
        if (!TryNormalizePairingCode(code, out var normalized)) return "Enter the six-digit invitation code.";
        if (string.IsNullOrWhiteSpace(Settings.PeerAddress)) return "Enter and save the inviter's Tailscale IP first.";
        var redemption = await transport.RedeemInvitationAsync(normalized, Settings.DeviceId, cancellationToken).ConfigureAwait(false);
        if (!redemption.Success || redemption.PeerDeviceId is null || redemption.Secret is not { Length: 32 } secret)
            return redemption.Error ?? "The invitation was rejected.";
        var credentialReference = "NovaLauncher.SaveSync.LegacyPeer";
        try
        {
            secrets.SetSecret(secret);
            if (peerCredentials is not null)
            {
                peerCredentials.SetSecret(redemption.PeerDeviceId.Value, secret);
                credentialReference = peerCredentials.GetCredentialReference(redemption.PeerDeviceId.Value);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            secrets.Clear();
            peerCredentials?.Clear(redemption.PeerDeviceId.Value);
            return $"The peer credential could not be stored safely: {exception.Message}";
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
        var now = timeProvider.GetUtcNow();
        var staged = _document with
        {
            Settings = Settings with
            {
                PeerDeviceId = Settings.PeerDeviceId ?? redemption.PeerDeviceId,
                TrustedPeers = UpsertTrustedPeer(
                    Settings.EffectiveTrustedPeers,
                    new TrustedSaveSyncPeer(redemption.PeerDeviceId.Value, "Paired device", Settings.PeerAddress!, credentialReference, TrustedPeerState.Active, now, now)),
                PendingInvitationId = null,
                PendingInvitationExpiresAtUtc = null,
                PendingCodeSalt = null,
                PendingCodeHash = null,
                PendingCodeFailedAttempts = 0,
            },
        };
        var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
        if (save.Status != DocumentSaveStatus.Saved)
        {
            secrets.Clear();
            peerCredentials?.Clear(redemption.PeerDeviceId.Value);
            return save.Error ?? "The invitation could not be accepted atomically.";
        }
        _document = staged;
        await EnsureTransportStartedAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    public async Task<string?> RevokePeerAsync(CancellationToken cancellationToken)
    {
        if (Settings.PeerDeviceId is not { } peerDeviceId) return null;
        return await RevokePeerAsync(peerDeviceId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> RenamePeerAsync(Guid peerDeviceId, string displayName, CancellationToken cancellationToken)
    {
        var normalized = displayName.Trim();
        if (peerDeviceId == Guid.Empty || normalized.Length is < 1 or > 100 || normalized.Any(char.IsControl))
            return "The trusted device name is invalid.";
        return await MutateTrustedPeerAsync(peerDeviceId, peer => peer with { DisplayName = normalized }, "The trusted device could not be renamed.", cancellationToken).ConfigureAwait(false);
    }

    public Task<string?> SetPeerPausedAsync(Guid peerDeviceId, bool paused, CancellationToken cancellationToken) =>
        MutateTrustedPeerAsync(peerDeviceId, peer => peer with { State = paused ? TrustedPeerState.Paused : TrustedPeerState.Active },
            paused ? "The trusted device could not be paused." : "The trusted device could not be resumed.", cancellationToken);

    public Task<string?> RotatePeerCredentialAsync(Guid peerDeviceId, CancellationToken cancellationToken) =>
        Task.FromResult<string?>("Credential rotation requires the peer to be online and is not available until the authenticated rotation handshake is completed.");

    public async Task<string?> RevokePeerAsync(Guid peerDeviceId, CancellationToken cancellationToken)
    {
        await _identityGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = Settings.EffectiveTrustedPeers.FirstOrDefault(peer => peer.DeviceId == peerDeviceId);
            if (existing is null && Settings.PeerDeviceId != peerDeviceId) return "The trusted device no longer exists.";
            var trustedPeers = Settings.EffectiveTrustedPeers
                .Select(peer => peer.DeviceId == peerDeviceId ? peer with { State = TrustedPeerState.Revoked } : peer)
                .ToArray();
            var isLegacyPeer = Settings.PeerDeviceId == peerDeviceId;
            var staged = _document with
            {
                Settings = Settings with
                {
                    PeerDeviceId = isLegacyPeer ? null : Settings.PeerDeviceId,
                    PeerAddress = isLegacyPeer ? null : Settings.PeerAddress,
                    TrustedPeers = trustedPeers,
                    PendingInvitationId = null,
                    PendingInvitationExpiresAtUtc = null,
                    PendingCodeSalt = null,
                    PendingCodeHash = null,
                    PendingCodeFailedAttempts = 0,
                },
            };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved) return save.Error ?? "The paired device could not be revoked.";
            _document = staged;
            peerCredentials?.Clear(peerDeviceId);
            if (isLegacyPeer) secrets.Clear();
            return null;
        }
        finally { _identityGate.Release(); }
    }

    public async Task<string?> ConfigurePeerAsync(string address, CancellationToken cancellationToken)
    {
        if (!TailscalePeerValidator.TryNormalize(address, out var normalized, out var error)) return error;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var staged = _document with { Settings = Settings with { PeerAddress = normalized } };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved) return save.Error ?? "The peer could not be saved.";
            _document = staged;
        }
        finally { _gate.Release(); }
        await EnsureTransportStartedAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    public async Task<string?> RetryListenerAsync(CancellationToken cancellationToken)
    {
        if (!HasActiveCredentials) return "Generate an invitation or complete pairing before starting the listener.";
        try
        {
            await transport.StartAsync(this, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (InvalidOperationException exception) { return exception.Message; }
    }

    public (Guid? Identity, string? Error) DeriveSharedSaveIdentity(string label, string platform)
    {
        if (!IsPaired) return (null, "Pair the devices before linking a game automatically.");
        if (ActivePeers().Length != 1) return (null, "Automatic pair-derived links are available only with one active peer. Use one shared save identity across every selected device.");
        var normalizedLabel = NormalizeIdentityText(label);
        var normalizedPlatform = NormalizeIdentityText(platform);
        if (normalizedLabel.Length is < 1 or > 200 || normalizedPlatform.Length is < 1 or > 100)
            return (null, "The sync label or platform is invalid.");
        var secret = secrets.GetSecret();
        if (secret is not { Length: 32 }) return (null, "The paired-device credential is unavailable.");
        try
        {
            var input = Encoding.UTF8.GetBytes($"novalauncher:paired-save:v1:{normalizedPlatform}:{normalizedLabel}");
            var hash = HMACSHA256.HashData(secret, input);
            hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
            return (new Guid(hash.AsSpan(0, 16)), null);
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    public async Task<int> RetryPendingUploadsAsync(CancellationToken cancellationToken)
    {
        if (!IsPaired || !transport.IsConfigured) return 0;
        var completed = 0;
        foreach (var pending in Settings.Games.Where(static game => game.Status == "Pending upload" && game.HeadSnapshotId is not null).ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = await LoadSnapshotAsync(pending.GameId, pending.HeadSnapshotId!.Value, fullContent: false, cancellationToken).ConfigureAwait(false);
            if (payload is null) continue;
            var pushed = await PushToActivePeersAsync(payload, cancellationToken).ConfigureAwait(false);
            if (!pushed.Success) continue;
            await SetStateAsync(pending with { Status = "Peer acknowledged" }, cancellationToken).ConfigureAwait(false);
            completed++;
        }
        return completed;
    }

    public async Task<SaveSyncResult> PullBeforeLaunchAsync(LibraryItem game, CancellationToken cancellationToken)
    {
        var validation = ValidateGame(game);
        if (validation is not null) return validation;
        if (!transport.IsConfigured || !IsPaired) return new(SaveSyncStatus.Unavailable, "Save sync is not paired.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var syncGameId = GetSyncGameId(game);
            var state = FindState(syncGameId);
            var local = await ScanAsync(game.SaveDirectory!, cancellationToken).ConfigureAwait(false);
            var pull = await PullFromActivePeersAsync(syncGameId, state?.HeadSnapshotId, cancellationToken).ConfigureAwait(false);
            if (!pull.Success) return new(pull.Conflict ? SaveSyncStatus.Conflict : SaveSyncStatus.QueuedOffline, pull.Error ?? "The peer is unavailable.");
            if (pull.Snapshot is null) return new(SaveSyncStatus.Unchanged, "The peer has no newer save snapshot.");
            if ((state is null && local.Count > 0 && !FileSetsEqual(local, pull.Snapshot.Manifest.Files)) ||
                (state is not null && !FileSetsEqual(local, state.LastObservedFiles)))
            {
                await StoreConflictAsync(pull.Snapshot, cancellationToken).ConfigureAwait(false);
                await SetStateAsync((state ?? new(syncGameId, null, local, "Conflict")) with { Status = "Conflict", ConflictSnapshotId = pull.Snapshot.Manifest.SnapshotId.ToString("N") }, cancellationToken).ConfigureAwait(false);
                return new(SaveSyncStatus.Conflict, "Both devices changed this save. Nothing was overwritten; resolve the conflict before launch.", pull.Snapshot.Manifest.SnapshotId);
            }
            await RestoreAtomicallyAsync(game.SaveDirectory!, pull.Snapshot, cancellationToken).ConfigureAwait(false);
            await SetStateAsync(new(syncGameId, pull.Snapshot.Manifest.SnapshotId, pull.Snapshot.Manifest.Files, "Downloaded"), cancellationToken).ConfigureAwait(false);
            return new(SaveSyncStatus.Applied, "A verified peer snapshot was restored after creating a local backup.", pull.Snapshot.Manifest.SnapshotId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or InvalidDataException)
        { return new(SaveSyncStatus.Failed, exception.Message); }
        finally { _gate.Release(); }
    }

    public async Task<SaveSyncResult> SnapshotAndPushAfterExitAsync(LibraryItem game, CancellationToken cancellationToken)
    {
        var validation = ValidateGame(game);
        if (validation is not null) return validation;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var first = await ScanAsync(game.SaveDirectory!, cancellationToken).ConfigureAwait(false);
            await Task.Delay(_quietPeriod, timeProvider, cancellationToken).ConfigureAwait(false);
            var files = await ScanAsync(game.SaveDirectory!, cancellationToken).ConfigureAwait(false);
            if (!FileSetsEqual(first, files)) return new(SaveSyncStatus.QueuedOffline, "Save files were still changing and were not uploaded.");
            var state = FindState(GetSyncGameId(game));
            if (state is null && files.Count == 0)
                return new(SaveSyncStatus.Unchanged, "The mapped folder is empty. NovaLauncher did not publish an empty first snapshot.");
            if (state is not null && FileSetsEqual(files, state.LastObservedFiles)) return new(SaveSyncStatus.Unchanged, "No save changes were detected.");
            var payload = await CreatePayloadAsync(game, state, files, cancellationToken).ConfigureAwait(false);
            await StoreSnapshotAsync(payload, cancellationToken).ConfigureAwait(false);
            await SetStateAsync(new(payload.Manifest.GameId, payload.Manifest.SnapshotId, files, "Pending upload"), cancellationToken).ConfigureAwait(false);
            if (!transport.IsConfigured || !IsPaired) return new(SaveSyncStatus.QueuedOffline, "A local snapshot was created and queued until the peer is available.", payload.Manifest.SnapshotId);
            var push = await PushToActivePeersAsync(payload, cancellationToken).ConfigureAwait(false);
            var status = push.Success ? "Peer acknowledged" : push.Conflict ? "Conflict" : "Pending upload";
            await SetStateAsync(new(payload.Manifest.GameId, payload.Manifest.SnapshotId, files, status, push.Conflict ? payload.Manifest.SnapshotId.ToString("N") : null), cancellationToken).ConfigureAwait(false);
            return push.Success
                ? new(SaveSyncStatus.SnapshotCreated, "Only changed save files were encrypted and sent to the paired peer.", payload.Manifest.SnapshotId)
                : new(push.Conflict ? SaveSyncStatus.Conflict : SaveSyncStatus.QueuedOffline, push.Error ?? "The snapshot remains queued.", payload.Manifest.SnapshotId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        { return new(SaveSyncStatus.Failed, exception.Message); }
        finally { _gate.Release(); }
    }

    public async Task<SaveSyncResult> ResolveConflictAsync(LibraryItem game, SaveConflictChoice choice, CancellationToken cancellationToken)
    {
        var validation = ValidateGame(game);
        if (validation is not null) return validation;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var syncGameId = GetSyncGameId(game);
            var state = FindState(syncGameId);
            if (state?.ConflictSnapshotId is null || !Guid.TryParse(state.ConflictSnapshotId, out var conflictId))
                return new(SaveSyncStatus.Unavailable, "There is no retained conflict for this game.");
            var remote = await LoadConflictAsync(syncGameId, conflictId, cancellationToken).ConfigureAwait(false);
            if (remote is null) return new(SaveSyncStatus.Failed, "The retained remote conflict snapshot is unavailable.");
            if (choice == SaveConflictChoice.KeepRemote)
            {
                await RestoreAtomicallyAsync(game.SaveDirectory!, remote, cancellationToken).ConfigureAwait(false);
                await StoreSnapshotAsync(remote, cancellationToken).ConfigureAwait(false);
                await SetStateAsync(new(syncGameId, remote.Manifest.SnapshotId, remote.Manifest.Files, "Synchronized"), cancellationToken).ConfigureAwait(false);
                return new(SaveSyncStatus.Applied, "The verified remote save was restored; the prior local files remain in NovaLauncher's backup history.", remote.Manifest.SnapshotId);
            }

            var local = await ScanAsync(game.SaveDirectory!, cancellationToken).ConfigureAwait(false);
            if (choice == SaveConflictChoice.KeepBoth)
            {
                await SetStateAsync(new(syncGameId, remote.Manifest.SnapshotId, local, "Remote conflict retained"), cancellationToken).ConfigureAwait(false);
                return new(SaveSyncStatus.Applied, "The local save remains active and the remote version is retained in conflict storage for recovery.", remote.Manifest.SnapshotId);
            }

            await StoreSnapshotAsync(remote, cancellationToken).ConfigureAwait(false);
            var baseState = new SaveSyncGameState(syncGameId, remote.Manifest.SnapshotId, remote.Manifest.Files, "Resolving local");
            var localPayload = await CreatePayloadAsync(game, baseState, local, cancellationToken).ConfigureAwait(false);
            await StoreSnapshotAsync(localPayload, cancellationToken).ConfigureAwait(false);
            var pushed = await PushToActivePeersAsync(localPayload, cancellationToken).ConfigureAwait(false);
            await SetStateAsync(new(syncGameId, localPayload.Manifest.SnapshotId, local, pushed.Success ? "Peer acknowledged" : "Pending upload"), cancellationToken).ConfigureAwait(false);
            return new(pushed.Success ? SaveSyncStatus.SnapshotCreated : SaveSyncStatus.QueuedOffline,
                pushed.Success ? "The local save was chosen and sent as a new generation; the remote conflict remains retained." : "The local choice was queued until the peer is available.", localPayload.Manifest.SnapshotId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or InvalidDataException)
        { return new(SaveSyncStatus.Failed, exception.Message); }
        finally { _gate.Release(); }
    }

    public async Task<TransportResult> ReceivePushAsync(SaveSnapshotPayload snapshot, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ValidatePayload(snapshot);
            var hosted = await LoadHeadAsync(snapshot.Manifest.GameId, cancellationToken).ConfigureAwait(false);
            if (hosted?.Manifest.SnapshotId == snapshot.Manifest.SnapshotId)
                return new(true, false, null, null);
            if (hosted?.Manifest.SnapshotId != snapshot.Manifest.ParentSnapshotId)
                return new(false, true, null, "The peer head diverged; the incoming snapshot was retained only by its sender.");
            await StoreSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
            return new(true, false, null, null);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or CryptographicException)
        { return new(false, false, null, exception.Message); }
        finally { _gate.Release(); }
    }

    public async Task<bool> AuthorizePeerAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        if (deviceId == Guid.Empty || deviceId == Settings.DeviceId) return false;
        var trusted = Settings.EffectiveTrustedPeers.FirstOrDefault(peer => peer.DeviceId == deviceId);
        if (trusted is not null && trusted.State != TrustedPeerState.Active) return false;
        if (Settings.PeerDeviceId is { } pinned)
        {
            if (pinned != deviceId) return false;
            if (Settings.PendingInvitationId is not null) await ConsumePendingInvitationAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        // An unpinned peer must redeem the rate-limited six-digit invitation first.
        // Encrypted application traffic never acts as an alternate pairing path.
        return false;
    }

    public async Task<PairingRedemptionResult> RedeemInvitationAsync(string code, Guid requestingDeviceId, CancellationToken cancellationToken)
    {
        if (requestingDeviceId == Guid.Empty || requestingDeviceId == Settings.DeviceId)
            return new(false, null, null, "The requesting device identity is invalid.");
        if (!TryNormalizePairingCode(code, out var normalized)) return new(false, null, null, "Enter the six-digit invitation code.");
        await _identityGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Settings.EffectiveTrustedPeers.Any(peer => peer.DeviceId == requestingDeviceId && peer.State == TrustedPeerState.Active))
                return new(false, null, null, "This device is already trusted; create a fresh invitation only when re-pairing is required.");
            if (Settings.PendingInvitationId is null || Settings.PendingInvitationExpiresAtUtc <= timeProvider.GetUtcNow() ||
                Settings.PendingCodeSalt is null || Settings.PendingCodeHash is null)
                return new(false, null, null, "The invitation is missing or expired.");
            if (Settings.PendingCodeFailedAttempts >= 3) return new(false, null, null, "The invitation is locked after three failed attempts.", 0);
            byte[] salt;
            byte[] expected;
            try { salt = Convert.FromBase64String(Settings.PendingCodeSalt); expected = Convert.FromBase64String(Settings.PendingCodeHash); }
            catch (FormatException) { return new(false, null, null, "The stored invitation is invalid."); }
            var actual = HashPairingCode(normalized, salt);
            var matches = expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
            CryptographicOperations.ZeroMemory(actual);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(salt);
            if (!matches)
            {
                var failures = Settings.PendingCodeFailedAttempts + 1;
                var failed = _document with { Settings = Settings with { PendingCodeFailedAttempts = failures } };
                var failedSave = await store.SaveAsync(failed, cancellationToken).ConfigureAwait(false);
                if (failedSave.Status != DocumentSaveStatus.Saved) return new(false, null, null, "The failed-attempt counter could not be persisted safely.");
                _document = failed;
                return new(false, null, null, failures >= 3 ? "The invitation is now locked." : "The code is incorrect.", Math.Max(0, 3 - failures));
            }
            var secret = secrets.GetSecret();
            if (secret is not { Length: 32 })
            {
                if (secret is not null) CryptographicOperations.ZeroMemory(secret);
                return new(false, null, null, "The invitation credential is unavailable.");
            }
            if (!TailscalePeerValidator.TryNormalize(Settings.PeerAddress ?? string.Empty, out var peerAddress, out _))
                return new(false, null, null, "Save the requesting device's Tailscale address before accepting its invitation redemption.");
            var credentialReference = "NovaLauncher.SaveSync.LegacyPeer";
            var credentialCopied = false;
            if (peerCredentials is not null)
            {
                peerCredentials.SetSecret(requestingDeviceId, secret);
                credentialReference = peerCredentials.GetCredentialReference(requestingDeviceId);
                credentialCopied = true;
            }
            var now = timeProvider.GetUtcNow();
            var staged = _document with
            {
                Settings = Settings with
                {
                    PeerDeviceId = Settings.PeerDeviceId ?? requestingDeviceId,
                    LastConsumedInvitationId = Settings.PendingInvitationId,
                    PendingInvitationId = null,
                    PendingInvitationExpiresAtUtc = null,
                    PendingCodeSalt = null,
                    PendingCodeHash = null,
                    PendingCodeFailedAttempts = 0,
                    TrustedPeers = UpsertTrustedPeer(Settings.EffectiveTrustedPeers,
                    new TrustedSaveSyncPeer(requestingDeviceId, "Paired device", peerAddress, credentialReference, TrustedPeerState.Active, now, now)),
                }
            };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved)
            {
                if (credentialCopied) peerCredentials!.Clear(requestingDeviceId);
                return new(false, null, null, "The device identity could not be pinned atomically.");
            }
            _document = staged;
            return new(true, Settings.DeviceId, secret, null, 0);
        }
        finally { _identityGate.Release(); }
    }

    public async Task<TransportResult> ServePullAsync(GameId gameId, Guid? knownHead, CancellationToken cancellationToken)
    {
        var head = await LoadHeadAsync(gameId, cancellationToken).ConfigureAwait(false);
        return head is null || head.Manifest.SnapshotId == knownHead
            ? new(true, false, null, null)
            : new(true, false, head, null);
    }

    public async Task<IReadOnlyList<SaveSnapshotHistoryItem>> GetSnapshotHistoryAsync(GameId gameId, CancellationToken cancellationToken)
    {
        if (gameId.Value == Guid.Empty) return [];
        var gameRoot = Path.Combine(_snapshotRoot, gameId.Value.ToString("N"));
        if (!Directory.Exists(gameRoot)) return [];
        Guid? head = null;
        var headPath = Path.Combine(gameRoot, "head.txt");
        if (File.Exists(headPath) && Guid.TryParse(await File.ReadAllTextAsync(headPath, cancellationToken).ConfigureAwait(false), out var parsedHead)) head = parsedHead;
        var history = new List<SaveSnapshotHistoryItem>();
        foreach (var directory in Directory.EnumerateDirectories(gameRoot).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).Take(MaximumRetainedSnapshotsPerGame))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(directory);
            if (!Guid.TryParse(name, out var snapshotId)) continue;
            try
            {
                var manifestPath = Path.Combine(directory, "manifest.txt");
                var manifest = ParseManifest(await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false));
                if (manifest.SnapshotId != snapshotId || manifest.GameId != gameId) throw new InvalidDataException("Snapshot identity does not match its directory.");
                long total = 0;
                foreach (var file in manifest.Files)
                {
                    total += file.Length;
                    var path = SafeCombine(directory, file.RelativePath);
                    if (!File.Exists(path) || !string.Equals(await HashFileAsync(path, cancellationToken).ConfigureAwait(false), file.Sha256, StringComparison.Ordinal))
                        throw new InvalidDataException($"Integrity check failed for {file.RelativePath}.");
                }
                history.Add(new(snapshotId, manifest.ParentSnapshotId, manifest.DeviceId, manifest.CreatedAtUtc, manifest.Files.Count, total, head == snapshotId, true, null));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                history.Add(new(snapshotId, null, Guid.Empty, DateTimeOffset.MinValue, 0, 0, head == snapshotId, false, exception.Message));
            }
        }
        return history.OrderByDescending(static item => item.CreatedAtUtc).ThenByDescending(static item => item.SnapshotId).ToArray();
    }

    public async Task<SaveSyncResult> RestoreSnapshotAsync(LibraryItem game, Guid snapshotId, CancellationToken cancellationToken)
    {
        if (snapshotId == Guid.Empty) return new(SaveSyncStatus.Failed, "Choose a valid snapshot to restore.");
        var validation = ValidateGame(game);
        if (validation is not null) return validation;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var syncGameId = GetSyncGameId(game);
            var payload = await LoadSnapshotAsync(syncGameId, snapshotId, fullContent: true, cancellationToken).ConfigureAwait(false);
            if (payload is null) return new(SaveSyncStatus.Failed, "The selected snapshot is no longer available.");
            ValidatePayload(payload);
            await RestoreAtomicallyAsync(game.SaveDirectory!, payload, cancellationToken).ConfigureAwait(false);
            await SetStateAsync(new(syncGameId, snapshotId, payload.Manifest.Files, "Restored from history"), cancellationToken).ConfigureAwait(false);
            return new(SaveSyncStatus.Applied, "The verified snapshot was restored after creating a backup of the current save folder.", snapshotId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or InvalidDataException)
        { return new(SaveSyncStatus.Failed, exception.Message); }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<SaveRestoreHistoryItem>> GetRestoreHistoryAsync(GameId gameId, CancellationToken cancellationToken)
    {
        if (gameId.Value == Guid.Empty) return [];
        var root = Path.Combine(_snapshotRoot, "backups", gameId.Value.ToString("N"));
        if (!Directory.Exists(root)) return [];
        var history = new List<SaveRestoreHistoryItem>();
        foreach (var path in Directory.EnumerateFiles(root, "restore.json", SearchOption.AllDirectories)
                     .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                     .Take(MaximumRetainedRestoreBackupsPerGame))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var item = System.Text.Json.JsonSerializer.Deserialize<SaveRestoreHistoryItem>(
                    await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
                if (item is not null && item.GameId == gameId && item.OperationId != Guid.Empty) history.Add(item);
            }
            catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException) { /* Corrupt history is omitted, never restored. */ }
        }
        return history.OrderByDescending(static item => item.CreatedAtUtc).ThenByDescending(static item => item.OperationId).ToArray();
    }

    public static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathFullyQualified(path) || path.Length > 1024 || path.Contains(':')) return false;
        var normalized = path.Replace('\\', '/');
        return normalized.Split('/').All(static part => part.Length > 0 && part is not "." and not "..");
    }

    private async Task<string?> MutateTrustedPeerAsync(
        Guid peerDeviceId,
        Func<TrustedSaveSyncPeer, TrustedSaveSyncPeer> mutation,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (peerDeviceId == Guid.Empty) return "The trusted device identity is invalid.";
        await _identityGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = Settings.EffectiveTrustedPeers.ToList().FindIndex(peer => peer.DeviceId == peerDeviceId);
            if (index < 0) return "The trusted device no longer exists.";
            var peers = Settings.EffectiveTrustedPeers.ToArray();
            peers[index] = mutation(peers[index]);
            var staged = _document with { Settings = Settings with { TrustedPeers = peers } };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved) return save.Error ?? failureMessage;
            _document = staged;
            return null;
        }
        finally { _identityGate.Release(); }
    }

    private static TrustedSaveSyncPeer[] UpsertTrustedPeer(
        IReadOnlyList<TrustedSaveSyncPeer> peers,
        TrustedSaveSyncPeer replacement) =>
        peers.Where(peer => peer.DeviceId != replacement.DeviceId).Append(replacement).OrderBy(peer => peer.DeviceId).ToArray();

    private SaveSyncResult? ValidateGame(LibraryItem game)
    {
        if (!string.Equals(game.Source, "Manual", StringComparison.OrdinalIgnoreCase))
            return new(SaveSyncStatus.Unavailable, "Steam games are excluded and continue to use Steam Cloud.");
        if (string.IsNullOrWhiteSpace(game.SaveDirectory) || !Directory.Exists(game.SaveDirectory))
            return new(SaveSyncStatus.Unavailable, "Choose an existing save folder for this manual game.");
        var saveRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(game.SaveDirectory));
        var managedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_snapshotRoot));
        if (managedRoot.StartsWith(saveRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            saveRoot.StartsWith(managedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return new(SaveSyncStatus.Failed, "The save folder overlaps NovaLauncher's managed snapshot storage.");
        return null;
    }

    private static async Task<IReadOnlyList<SaveFileEntry>> ScanAsync(string root, CancellationToken cancellationToken)
    {
        var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var entries = new List<SaveFileEntry>();
        long total = 0;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false, AttributesToSkip = FileAttributes.ReparsePoint, ReturnSpecialDirectories = false };
        foreach (var path in Directory.EnumerateFiles(rootPath, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entries.Count >= MaximumFiles) throw new InvalidDataException("The save folder contains too many files.");
            var full = Path.GetFullPath(path);
            if (!full.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("A save path escaped its configured root.");
            var info = new FileInfo(full);
            if (info.Length > MaximumFileBytes) throw new InvalidDataException("A save file exceeds the 64 MiB limit.");
            total += info.Length;
            if (total > MaximumSnapshotBytes) throw new InvalidDataException("The save set exceeds the 512 MiB limit.");
            var relative = Path.GetRelativePath(rootPath, full).Replace('\\', '/');
            if (!IsSafeRelativePath(relative)) throw new InvalidDataException("A save path is unsafe.");
            await using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
            entries.Add(new(relative, info.Length, Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant()));
        }
        return entries.OrderBy(static entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task<SaveSnapshotPayload> CreatePayloadAsync(LibraryItem game, SaveSyncGameState? state, IReadOnlyList<SaveFileEntry> files, CancellationToken cancellationToken)
    {
        var previous = state?.LastObservedFiles.ToDictionary(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase) ?? [];
        var changed = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.Where(file => !previous.TryGetValue(file.RelativePath, out var old) || old.Sha256 != file.Sha256))
            changed[file.RelativePath] = await File.ReadAllBytesAsync(Path.Combine(game.SaveDirectory!, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)), cancellationToken).ConfigureAwait(false);
        var current = files.Select(static file => file.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var deleted = previous.Keys.Where(path => !current.Contains(path)).ToArray();
        var manifest = new SaveSnapshotManifest(Guid.NewGuid(), state?.HeadSnapshotId, GetSyncGameId(game), Settings.DeviceId, timeProvider.GetUtcNow(), files, deleted);
        return new(manifest, changed);
    }

    private async Task RestoreAtomicallyAsync(string destination, SaveSnapshotPayload payload, CancellationToken cancellationToken)
    {
        ValidatePayload(payload);
        var operationId = Guid.NewGuid();
        var operation = operationId.ToString("N");
        var stage = Path.Combine(_snapshotRoot, "staging", operation);
        var backup = Path.Combine(_snapshotRoot, "backups", payload.Manifest.GameId.Value.ToString("N"), operation);
        Directory.CreateDirectory(stage);
        Directory.CreateDirectory(backup);
        SaveRestoreHistoryItem? restoreHistory = null;
        try
        {
            foreach (var file in payload.Manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!payload.ChangedFiles.TryGetValue(file.RelativePath, out var bytes)) throw new InvalidDataException("The restore payload is incomplete.");
                var target = SafeCombine(stage, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllBytesAsync(target, bytes, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(Hash(bytes), file.Sha256, StringComparison.Ordinal)) throw new InvalidDataException("A restored save failed integrity verification.");
            }
            var backupCount = 0;
            long backupBytes = 0;
            foreach (var existing in Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(destination, existing);
                var target = SafeCombine(backup, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(existing, target, true);
                backupCount++;
                backupBytes += new FileInfo(existing).Length;
            }
            restoreHistory = new(operationId, payload.Manifest.GameId, payload.Manifest.SnapshotId, timeProvider.GetUtcNow(), backupCount, backupBytes, "Prepared");
            await WriteRestoreHistoryAsync(backup, restoreHistory, cancellationToken).ConfigureAwait(false);
            foreach (var file in payload.Manifest.Files)
            {
                var source = SafeCombine(stage, file.RelativePath);
                var target = SafeCombine(destination, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(source, target, true);
            }
            foreach (var deleted in payload.Manifest.DeletedPaths)
            {
                var target = SafeCombine(destination, deleted);
                if (File.Exists(target)) File.Delete(target);
            }
            restoreHistory = restoreHistory with { Outcome = "Completed" };
            await WriteRestoreHistoryAsync(backup, restoreHistory, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            foreach (var file in Directory.Exists(backup) ? Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories) : [])
            {
                if (string.Equals(Path.GetFileName(file), "restore.json", StringComparison.OrdinalIgnoreCase)) continue;
                var relative = Path.GetRelativePath(backup, file);
                var target = SafeCombine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
            if (restoreHistory is not null)
            {
                try { await WriteRestoreHistoryAsync(backup, restoreHistory with { Outcome = "Rolled back" }, CancellationToken.None).ConfigureAwait(false); }
                catch (IOException) { /* Preserve the backup even when its optional history marker cannot be updated. */ }
            }
            throw;
        }
        finally
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
            EnforceBackupRetention(payload.Manifest.GameId);
        }
    }

    private async Task StoreSnapshotAsync(SaveSnapshotPayload payload, CancellationToken cancellationToken)
    {
        ValidatePayload(payload);
        var directory = SnapshotDirectory(payload.Manifest.GameId, payload.Manifest.SnapshotId);
        var stage = directory + ".tmp-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stage);
        try
        {
            var parent = payload.Manifest.ParentSnapshotId is { } parentId ? SnapshotDirectory(payload.Manifest.GameId, parentId) : null;
            foreach (var file in payload.Manifest.Files)
            {
                var target = SafeCombine(stage, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (payload.ChangedFiles.TryGetValue(file.RelativePath, out var bytes)) await File.WriteAllBytesAsync(target, bytes, cancellationToken).ConfigureAwait(false);
                else if (parent is not null) File.Copy(SafeCombine(parent, file.RelativePath), target, true);
                else throw new InvalidDataException("A snapshot delta has no available parent content.");
                if (!string.Equals(await HashFileAsync(target, cancellationToken).ConfigureAwait(false), file.Sha256, StringComparison.Ordinal)) throw new InvalidDataException("Snapshot integrity verification failed.");
            }
            await File.WriteAllTextAsync(Path.Combine(stage, "manifest.txt"), SerializeManifest(payload.Manifest), cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(Path.GetDirectoryName(directory)!);
            Directory.Move(stage, directory);
            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(directory)!, "head.txt"), payload.Manifest.SnapshotId.ToString("N"), cancellationToken).ConfigureAwait(false);
            EnforceSnapshotRetention(payload.Manifest.GameId, payload.Manifest.SnapshotId);
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }

    private async Task StoreConflictAsync(SaveSnapshotPayload payload, CancellationToken cancellationToken)
    {
        ValidatePayload(payload);
        var directory = Path.Combine(_snapshotRoot, "conflicts", payload.Manifest.GameId.Value.ToString("N"), payload.Manifest.SnapshotId.ToString("N"));
        Directory.CreateDirectory(directory);
        foreach (var file in payload.Manifest.Files)
        {
            if (!payload.ChangedFiles.TryGetValue(file.RelativePath, out var bytes)) throw new InvalidDataException("A pulled conflict must contain complete file content.");
            var target = SafeCombine(directory, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllBytesAsync(target, bytes, cancellationToken).ConfigureAwait(false);
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest.txt"), SerializeManifest(payload.Manifest), cancellationToken).ConfigureAwait(false);
    }

    private async Task<SaveSnapshotPayload?> LoadConflictAsync(GameId gameId, Guid id, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_snapshotRoot, "conflicts", gameId.Value.ToString("N"), id.ToString("N"));
        var manifestPath = Path.Combine(directory, "manifest.txt");
        if (!File.Exists(manifestPath)) return null;
        var manifest = ParseManifest(await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false));
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files) files[file.RelativePath] = await File.ReadAllBytesAsync(SafeCombine(directory, file.RelativePath), cancellationToken).ConfigureAwait(false);
        return new(manifest, files);
    }

    private async Task<SaveSnapshotPayload?> LoadHeadAsync(GameId gameId, CancellationToken cancellationToken)
    {
        var gameRoot = Path.Combine(_snapshotRoot, gameId.Value.ToString("N"));
        var headPath = Path.Combine(gameRoot, "head.txt");
        if (!File.Exists(headPath) || !Guid.TryParse(await File.ReadAllTextAsync(headPath, cancellationToken).ConfigureAwait(false), out var head)) return null;
        return await LoadSnapshotAsync(gameId, head, fullContent: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SaveSnapshotPayload?> LoadSnapshotAsync(GameId gameId, Guid snapshotId, bool fullContent, CancellationToken cancellationToken)
    {
        var directory = SnapshotDirectory(gameId, snapshotId);
        if (!Directory.Exists(directory)) return null;
        var manifest = ParseManifest(await File.ReadAllTextAsync(Path.Combine(directory, "manifest.txt"), cancellationToken).ConfigureAwait(false));
        Dictionary<string, SaveFileEntry> parentFiles = [];
        if (!fullContent && manifest.ParentSnapshotId is { } parentId)
        {
            var parentManifestPath = Path.Combine(SnapshotDirectory(gameId, parentId), "manifest.txt");
            if (File.Exists(parentManifestPath))
            {
                var parentManifest = ParseManifest(await File.ReadAllTextAsync(parentManifestPath, cancellationToken).ConfigureAwait(false));
                parentFiles = parentManifest.Files.ToDictionary(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
            }
        }
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files.Where(file => fullContent || !parentFiles.TryGetValue(file.RelativePath, out var parent) || parent.Sha256 != file.Sha256))
            files[file.RelativePath] = await File.ReadAllBytesAsync(SafeCombine(directory, file.RelativePath), cancellationToken).ConfigureAwait(false);
        return new(manifest, files);
    }

    private static void ValidatePayload(SaveSnapshotPayload payload)
    {
        if (payload.Manifest.SnapshotId == Guid.Empty || payload.Manifest.DeviceId == Guid.Empty || payload.Manifest.Files.Count > MaximumFiles) throw new InvalidDataException("The snapshot manifest is invalid.");
        long total = 0;
        foreach (var file in payload.Manifest.Files)
        {
            if (!IsSafeRelativePath(file.RelativePath) || file.Length < 0 || file.Length > MaximumFileBytes || file.Sha256.Length != 64) throw new InvalidDataException("The snapshot contains an unsafe file.");
            total += file.Length;
        }
        if (total > MaximumSnapshotBytes || payload.Manifest.DeletedPaths.Any(path => !IsSafeRelativePath(path))) throw new InvalidDataException("The snapshot exceeds safety limits.");
        foreach (var (path, bytes) in payload.ChangedFiles)
            if (!IsSafeRelativePath(path) || bytes.LongLength > MaximumFileBytes || !payload.Manifest.Files.Any(file => string.Equals(file.RelativePath, path, StringComparison.OrdinalIgnoreCase) && file.Length == bytes.LongLength && file.Sha256 == Hash(bytes))) throw new InvalidDataException("Changed snapshot content failed validation.");
    }

    private TrustedSaveSyncPeer[] ActivePeers() => Settings.EffectiveTrustedPeers
        .Where(peer => peer.State == TrustedPeerState.Active &&
            (peerCredentials?.ContainsSecret(peer.DeviceId) == true ||
             (Settings.PeerDeviceId == peer.DeviceId && secrets.HasSecret)))
        .OrderBy(peer => peer.DeviceId)
        .ToArray();

    private async Task<TransportResult> PushToActivePeersAsync(SaveSnapshotPayload payload, CancellationToken cancellationToken)
    {
        var peers = ActivePeers();
        if (peers.Length == 0) return new(false, false, null, "No active trusted save-sync device is available.");
        var failures = new List<string>();
        foreach (var peer in peers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await transport.PushAsync(peer, payload, cancellationToken).ConfigureAwait(false);
            if (result.Conflict) return new(false, true, null, $"{peer.DisplayName}: {result.Error ?? "generation conflict"}");
            if (!result.Success) failures.Add($"{peer.DisplayName}: {result.Error ?? "unavailable"}");
        }
        return failures.Count == 0
            ? new(true, false, null, null)
            : new(false, false, null, $"Partial fan-out remains queued ({string.Join("; ", failures)}). No failed peer was marked acknowledged.");
    }

    private async Task<TransportResult> PullFromActivePeersAsync(GameId gameId, Guid? knownHead, CancellationToken cancellationToken)
    {
        var peers = ActivePeers();
        if (peers.Length == 0) return new(false, false, null, "No active trusted save-sync device is available.");
        var snapshots = new List<(TrustedSaveSyncPeer Peer, SaveSnapshotPayload Snapshot)>();
        var failures = new List<string>();
        foreach (var peer in peers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await transport.PullAsync(peer, gameId, knownHead, cancellationToken).ConfigureAwait(false);
            if (result.Conflict) return new(false, true, null, $"{peer.DisplayName}: {result.Error ?? "generation conflict"}");
            if (!result.Success) failures.Add($"{peer.DisplayName}: {result.Error ?? "unavailable"}");
            else if (result.Snapshot is not null) snapshots.Add((peer, result.Snapshot));
        }
        var distinctHeads = snapshots.Select(item => item.Snapshot.Manifest.SnapshotId).Distinct().ToArray();
        if (distinctHeads.Length > 1)
            return new(false, true, null, "Trusted devices reported divergent save generations. Fan-out is blocked until the conflict is resolved explicitly.");
        if (snapshots.Count > 0) return new(true, false, snapshots[0].Snapshot, null);
        return failures.Count == peers.Length
            ? new(false, false, null, $"All trusted devices are unavailable ({string.Join("; ", failures)}).")
            : new(true, false, null, null);
    }

    private SaveSyncGameState? FindState(GameId gameId) => Settings.Games.FirstOrDefault(game => game.GameId == gameId);
    private static GameId GetSyncGameId(LibraryItem game) => game.SaveSyncId is { } shared ? new GameId(shared) : game.Id;
    private async Task SetStateAsync(SaveSyncGameState state, CancellationToken cancellationToken)
    {
        var games = Settings.Games.Where(game => game.GameId != state.GameId).Append(state).OrderBy(game => game.GameId.Value).ToArray();
        var staged = _document with { Settings = Settings with { Games = games } };
        var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
        if (save.Status != DocumentSaveStatus.Saved) throw new IOException(save.Error ?? "Save-sync state persistence failed.");
        _document = staged;
    }
    private async Task EnsureTransportStartedAsync(CancellationToken token)
    {
        if (!HasActiveCredentials) return;
        try { await transport.StartAsync(this, token).ConfigureAwait(false); }
        catch (InvalidOperationException) { /* ListenerStatus exposes the failure; configuration remains available for retry. */ }
    }
    private async Task ConsumePendingInvitationAsync(CancellationToken token)
    {
        await _identityGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (Settings.PendingInvitationId is null) return;
            var consumed = _document with
            {
                Settings = Settings with
                {
                    PendingInvitationId = null,
                    PendingInvitationExpiresAtUtc = null,
                    PendingCodeSalt = null,
                    PendingCodeHash = null,
                    PendingCodeFailedAttempts = 0,
                },
            };
            var save = await store.SaveAsync(consumed, token).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved) throw new IOException(save.Error ?? "The invitation could not be consumed atomically.");
            _document = consumed;
        }
        finally { _identityGate.Release(); }
    }
    private string SnapshotDirectory(GameId gameId, Guid id) => Path.Combine(_snapshotRoot, gameId.Value.ToString("N"), id.ToString("N"));
    private void EnforceSnapshotRetention(GameId gameId, Guid headSnapshotId)
    {
        var gameRoot = Path.Combine(_snapshotRoot, gameId.Value.ToString("N"));
        if (!Directory.Exists(gameRoot)) return;
        var snapshots = Directory.EnumerateDirectories(gameRoot)
            .Select(path => (Path: path, Id: Guid.TryParse(Path.GetFileName(path), out var id) ? id : Guid.Empty, Created: ReadSnapshotCreatedAt(path)))
            .Where(static item => item.Id != Guid.Empty)
            .OrderByDescending(static item => item.Created)
            .ThenByDescending(static item => item.Id)
            .ToArray();
        var retained = new HashSet<Guid> { headSnapshotId };
        foreach (var snapshot in snapshots.Where(item => item.Id != headSnapshotId).Take(MaximumRetainedSnapshotsPerGame - 1)) retained.Add(snapshot.Id);
        foreach (var obsolete in snapshots.Where(item => !retained.Contains(item.Id)))
            Directory.Delete(obsolete.Path, recursive: true);
    }

    private static DateTimeOffset ReadSnapshotCreatedAt(string directory)
    {
        try { return ParseManifest(File.ReadAllText(Path.Combine(directory, "manifest.txt"))).CreatedAtUtc; }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException) { return DateTimeOffset.MinValue; }
    }
    private static Task WriteRestoreHistoryAsync(string backup, SaveRestoreHistoryItem item, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(Path.Combine(backup, "restore.json"), System.Text.Json.JsonSerializer.Serialize(item), cancellationToken);

    private void EnforceBackupRetention(GameId gameId)
    {
        var root = Path.Combine(_snapshotRoot, "backups", gameId.Value.ToString("N"));
        if (!Directory.Exists(root)) return;
        var backups = Directory.EnumerateDirectories(root)
            .OrderByDescending(static path => Directory.GetCreationTimeUtc(path))
            .ThenByDescending(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var obsolete in backups.Skip(MaximumRetainedRestoreBackupsPerGame)) Directory.Delete(obsolete, recursive: true);
    }
    private static string SafeCombine(string root, string relative) { if (!IsSafeRelativePath(relative)) throw new InvalidDataException("Unsafe relative path."); var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))); var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)); if (!full.StartsWith(normalized + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escaped its root."); return full; }
    private static bool FileSetsEqual(IReadOnlyList<SaveFileEntry> left, IReadOnlyList<SaveFileEntry> right) => left.Count == right.Count && left.Zip(right).All(pair => pair.First == pair.Second);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static async Task<string> HashFileAsync(string path, CancellationToken token) { await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false)).ToLowerInvariant(); }
    private static string SerializeManifest(SaveSnapshotManifest manifest) => System.Text.Json.JsonSerializer.Serialize(manifest);
    private static SaveSnapshotManifest ParseManifest(string json) => System.Text.Json.JsonSerializer.Deserialize<SaveSnapshotManifest>(json) ?? throw new InvalidDataException("Snapshot manifest is invalid.");
    public void Dispose() { _gate.Dispose(); _identityGate.Dispose(); }

    private static bool TryNormalizePairingCode(string value, out string normalized)
    {
        normalized = new string(value.Where(static character => character is not (' ' or '-')).ToArray());
        return normalized.Length == 6 && normalized.All(static character => character is >= '0' and <= '9');
    }

    private static byte[] HashPairingCode(string code, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(code, salt, 200_000, HashAlgorithmName.SHA256, 32);

    private static string NormalizeIdentityText(string value) => string.Join(' ', value
        .Normalize(NormalizationForm.FormKC)
        .Trim()
        .ToLowerInvariant()
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
