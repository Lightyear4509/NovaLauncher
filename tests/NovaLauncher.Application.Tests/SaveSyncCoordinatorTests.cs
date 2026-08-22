using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;

namespace NovaLauncher.Application.Tests;

public sealed class SaveSyncCoordinatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"NovaLauncher-Sync-{Guid.NewGuid():N}");
    private string SaveRoot => Path.Combine(_root, "saves");

    [Theory]
    [InlineData("save.dat", true)]
    [InlineData("slot/one.sav", true)]
    [InlineData("../escape.sav", false)]
    [InlineData("C:\\escape.sav", false)]
    [InlineData("slot:ads", false)]
    public void RelativePathValidationRejectsTraversalAndAlternateStreams(string path, bool expected) =>
        Assert.Equal(expected, SaveSyncCoordinator.IsSafeRelativePath(path));

    [Fact]
    public async Task LegacySinglePeerIsMigratedToIndependentTrustedPeerRecord()
    {
        var peerId = Guid.NewGuid();
        var initial = SaveSyncDocument.CreateDefault();
        initial = initial with { Settings = initial.Settings with { PeerDeviceId = peerId, PeerAddress = "100.64.0.2" } };
        var service = new SaveSyncCoordinator(
            new Store(initial), new FakeTransport(), new Secret(),
            new Clock(new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero)),
            Path.Combine(_root, "migration"), TimeSpan.Zero);

        Assert.Null(await service.InitializeAsync(CancellationToken.None));

        var trusted = Assert.Single(service.Settings.EffectiveTrustedPeers);
        Assert.Equal(peerId, trusted.DeviceId);
        Assert.Equal("100.64.0.2", trusted.Address);
        Assert.Equal(TrustedPeerState.Active, trusted.State);
        Assert.Equal("NovaLauncher.SaveSync.LegacyPeer", trusted.CredentialReference);
        Assert.NotEqual(service.Settings.DeviceId, trusted.DeviceId);
    }

    [Fact]
    public async Task TrustedPeerLifecycleIsAtomicAndUsesIndependentCredential()
    {
        var peerId = Guid.NewGuid();
        var initial = SaveSyncDocument.CreateDefault();
        initial = initial with { Settings = initial.Settings with { PeerDeviceId = peerId, PeerAddress = "100.64.0.2" } };
        var credentials = new PeerCredentials();
        var service = new SaveSyncCoordinator(
            new Store(initial), new FakeTransport(), new Secret(), TimeProvider.System,
            Path.Combine(_root, "peer-lifecycle"), TimeSpan.Zero, credentials);
        await service.InitializeAsync(CancellationToken.None);

        Assert.True(credentials.ContainsSecret(peerId));
        Assert.Null(await service.RenamePeerAsync(peerId, "Living room", CancellationToken.None));
        Assert.Equal("Living room", Assert.Single(service.Settings.EffectiveTrustedPeers).DisplayName);
        Assert.Null(await service.SetPeerPausedAsync(peerId, true, CancellationToken.None));
        Assert.False(await service.AuthorizePeerAsync(peerId, CancellationToken.None));
        Assert.Equal(TrustedPeerState.Paused, Assert.Single(service.Settings.EffectiveTrustedPeers).State);
        Assert.Null(await service.SetPeerPausedAsync(peerId, false, CancellationToken.None));
        Assert.True(await service.AuthorizePeerAsync(peerId, CancellationToken.None));

        var beforeRotation = credentials.GetSecret(peerId)!;
        Assert.Null(await service.RotatePeerCredentialAsync(peerId, CancellationToken.None));
        Assert.NotEqual(beforeRotation, credentials.GetSecret(peerId));
        Assert.False(credentials.ContainsPendingSecret(peerId));

        Assert.Null(await service.RevokePeerAsync(peerId, CancellationToken.None));
        Assert.Equal(TrustedPeerState.Revoked, Assert.Single(service.Settings.EffectiveTrustedPeers).State);
        Assert.False(credentials.ContainsSecret(peerId));
        Assert.False(service.IsPaired);
    }

    [Fact]
    public async Task MultiPeerFanoutRequiresEveryActivePeerAcknowledgement()
    {
        Directory.CreateDirectory(SaveRoot);
        await File.WriteAllTextAsync(Path.Combine(SaveRoot, "slot.sav"), "one");
        var peers = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var credentials = new PeerCredentials();
        var initial = SaveSyncDocument.CreateDefault();
        initial = initial with
        {
            Settings = initial.Settings with
            {
                TrustedPeers = peers.Select((id, index) =>
            new TrustedSaveSyncPeer(id, $"Device {index + 1}", $"100.64.0.{index + 2}", credentials.GetCredentialReference(id), TrustedPeerState.Active, DateTimeOffset.UtcNow)).ToArray()
            }
        };
        foreach (var peer in peers) credentials.SetSecret(peer, new byte[32]);
        var transport = new FakeTransport { FailedPushPeer = peers[1] };
        var service = new SaveSyncCoordinator(new Store(initial), transport, new Secret(false), TimeProvider.System,
            Path.Combine(_root, "multi-fanout"), TimeSpan.Zero, credentials);
        await service.InitializeAsync(CancellationToken.None);

        var result = await service.SnapshotAndPushAfterExitAsync(Game("Manual") with { SaveDirectory = SaveRoot }, CancellationToken.None);

        Assert.Equal(SaveSyncStatus.QueuedOffline, result.Status);
        Assert.Equal(peers.Order(), transport.PushedPeers.Order());
        Assert.Contains("Partial fan-out", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PerGameDestinationPolicyLimitsFanoutAndSurvivesQueuedState()
    {
        Directory.CreateDirectory(SaveRoot);
        await File.WriteAllTextAsync(Path.Combine(SaveRoot, "slot.sav"), "one");
        var peers = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var credentials = new PeerCredentials();
        var initial = SaveSyncDocument.CreateDefault() with
        {
            Settings = SaveSyncDocument.CreateDefault().Settings with
            {
                TrustedPeers = peers.Select((id, index) => new TrustedSaveSyncPeer(id, $"Device {index + 1}", $"100.64.0.{index + 2}", credentials.GetCredentialReference(id), TrustedPeerState.Active, DateTimeOffset.UtcNow)).ToArray(),
            },
        };
        foreach (var peer in peers) credentials.SetSecret(peer, new byte[32]);
        var transport = new FakeTransport();
        var service = new SaveSyncCoordinator(new Store(initial), transport, new Secret(false), TimeProvider.System, Path.Combine(_root, "destination-policy"), TimeSpan.Zero, credentials);
        await service.InitializeAsync(CancellationToken.None);
        var selected = new[] { peers[0], peers[2] };

        var result = await service.SnapshotAndPushAfterExitAsync(Game("Manual") with { SaveDirectory = SaveRoot, SaveSyncPeerIds = selected }, CancellationToken.None);

        Assert.Equal(SaveSyncStatus.SnapshotCreated, result.Status);
        Assert.Equal(selected.Order(), transport.PushedPeers.Order());
        Assert.Equal(selected.Order(), Assert.Single(service.Settings.Games).DestinationPeerIds!.Order());
    }

    [Fact]
    public async Task DivergentMultiPeerHeadsBlockPullWithoutOverwritingLocalFolder()
    {
        Directory.CreateDirectory(SaveRoot);
        var game = Game("Manual") with { SaveDirectory = SaveRoot };
        var peers = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var credentials = new PeerCredentials();
        var initial = SaveSyncDocument.CreateDefault();
        initial = initial with
        {
            Settings = initial.Settings with
            {
                TrustedPeers = peers.Select((id, index) =>
            new TrustedSaveSyncPeer(id, $"Device {index + 1}", $"100.64.0.{index + 2}", credentials.GetCredentialReference(id), TrustedPeerState.Active, DateTimeOffset.UtcNow)).ToArray()
            }
        };
        foreach (var peer in peers) credentials.SetSecret(peer, new byte[32]);
        var transport = new FakeTransport();
        transport.Pulls[peers[0]] = Payload(game.Id, "peer-a");
        transport.Pulls[peers[1]] = Payload(game.Id, "peer-b");
        var service = new SaveSyncCoordinator(new Store(initial), transport, new Secret(false), TimeProvider.System,
            Path.Combine(_root, "multi-divergence"), TimeSpan.Zero, credentials);
        await service.InitializeAsync(CancellationToken.None);

        var result = await service.PullBeforeLaunchAsync(game, CancellationToken.None);

        Assert.Equal(SaveSyncStatus.Conflict, result.Status);
        Assert.Empty(Directory.EnumerateFileSystemEntries(SaveRoot));
    }

    [Fact]
    public async Task RetriedAcknowledgedGenerationIsIdempotent()
    {
        var (service, _) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var payload = Payload(new GameId(Guid.NewGuid()), "generation");

        Assert.True((await service.ReceivePushAsync(payload, CancellationToken.None)).Success);
        var retry = await service.ReceivePushAsync(payload, CancellationToken.None);

        Assert.True(retry.Success);
        Assert.False(retry.Conflict);
    }

    [Fact]
    public async Task IncomingSaveUploadResumesAtDurableByteOffsetBeforeCommit()
    {
        var (service, _) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var peerId = service.Settings.PeerDeviceId!.Value;
        var gameId = new GameId(Guid.NewGuid());
        var bytes = Enumerable.Range(0, SaveSyncCoordinator.SaveTransferChunkBytes + 37).Select(static index => (byte)(index % 251)).ToArray();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var manifest = new SaveSnapshotManifest(Guid.NewGuid(), null, gameId, peerId, DateTimeOffset.UtcNow, [new("slot.sav", bytes.Length, hash)], []);

        var begin = await service.BeginIncomingSnapshotAsync(peerId, manifest, ["slot.sav"], CancellationToken.None);
        Assert.True(begin.Success, begin.Error);
        var first = await service.ReceiveIncomingSnapshotChunkAsync(peerId, manifest.SnapshotId, "slot.sav", 0, bytes[..1000], CancellationToken.None);
        Assert.True(first.Success, first.Error);

        var resumed = await service.BeginIncomingSnapshotAsync(peerId, manifest, ["slot.sav"], CancellationToken.None);
        Assert.Equal(1000, resumed.ResumeOffsets["slot.sav"]);
        var second = await service.ReceiveIncomingSnapshotChunkAsync(peerId, manifest.SnapshotId, "slot.sav", 1000, bytes[1000..], CancellationToken.None);
        Assert.True(second.Success, second.Error);
        var completed = await service.CompleteIncomingSnapshotAsync(peerId, manifest.SnapshotId, CancellationToken.None);

        Assert.True(completed.Success, completed.Error);
        var retained = Assert.Single(await service.GetSnapshotHistoryAsync(gameId, CancellationToken.None));
        Assert.True(retained.IntegrityValid);
        Assert.Equal(bytes.Length, retained.TotalBytes);
    }

    [Fact]
    public async Task ExplicitCancelRemovesOnlyIncomingUnverifiedPartialData()
    {
        var (service, _) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var peerId = service.Settings.PeerDeviceId!.Value;
        var bytes = System.Text.Encoding.UTF8.GetBytes("partial-save");
        var manifest = new SaveSnapshotManifest(Guid.NewGuid(), null, new GameId(Guid.NewGuid()), peerId, DateTimeOffset.UtcNow,
            [new("slot.sav", bytes.Length, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant())], []);
        Assert.True((await service.BeginIncomingSnapshotAsync(peerId, manifest, ["slot.sav"], CancellationToken.None)).Success);
        Assert.True((await service.ReceiveIncomingSnapshotChunkAsync(peerId, manifest.SnapshotId, "slot.sav", 0, bytes[..3], CancellationToken.None)).Success);

        Assert.Null(await service.CancelIncomingPartialTransfersAsync(peerId, CancellationToken.None));
        var restarted = await service.BeginIncomingSnapshotAsync(peerId, manifest, ["slot.sav"], CancellationToken.None);

        Assert.True(restarted.Success);
        Assert.Equal(0, restarted.ResumeOffsets["slot.sav"]);
        Assert.Empty(await service.GetSnapshotHistoryAsync(manifest.GameId, CancellationToken.None));
    }

    [Fact]
    public async Task SteamGameIsHardExcluded()
    {
        var (service, _) = Create();
        var game = Game("Steam") with { SaveDirectory = _root };
        var result = await service.SnapshotAndPushAfterExitAsync(game, CancellationToken.None);
        Assert.Equal(SaveSyncStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task PushSendsOnlyChangedFilesAfterInitialSnapshot()
    {
        Directory.CreateDirectory(SaveRoot);
        await File.WriteAllTextAsync(Path.Combine(SaveRoot, "one.sav"), "one");
        await File.WriteAllTextAsync(Path.Combine(SaveRoot, "two.sav"), "two");
        var (service, transport) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var game = Game("Manual") with { SaveDirectory = SaveRoot };

        Assert.Equal(SaveSyncStatus.SnapshotCreated, (await service.SnapshotAndPushAfterExitAsync(game, CancellationToken.None)).Status);
        Assert.Equal(2, transport.LastPush!.ChangedFiles.Count);
        await File.WriteAllTextAsync(Path.Combine(SaveRoot, "one.sav"), "changed");
        Assert.Equal(SaveSyncStatus.SnapshotCreated, (await service.SnapshotAndPushAfterExitAsync(game, CancellationToken.None)).Status);
        Assert.Equal(["one.sav"], transport.LastPush!.ChangedFiles.Keys);
    }

    [Fact]
    public async Task RenameAndDeletionManifestRestoresWithBackupRecoveryPoint()
    {
        Directory.CreateDirectory(SaveRoot);
        var oldPath = Path.Combine(SaveRoot, "old.sav");
        await File.WriteAllTextAsync(oldPath, "baseline");
        var (service, transport) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var game = Game("Manual") with { SaveDirectory = SaveRoot };
        var baseline = await service.SnapshotAndPushAfterExitAsync(game, CancellationToken.None);
        var bytes = System.Text.Encoding.UTF8.GetBytes("renamed");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var snapshotId = Guid.NewGuid();
        transport.PullResult = new(new SaveSnapshotManifest(snapshotId, baseline.SnapshotId, game.Id, Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new("new.sav", bytes.Length, hash)], ["old.sav"]), new Dictionary<string, byte[]> { ["new.sav"] = bytes });

        var result = await service.PullBeforeLaunchAsync(game, CancellationToken.None);

        Assert.Equal(SaveSyncStatus.Applied, result.Status);
        Assert.False(File.Exists(oldPath));
        Assert.Equal("renamed", await File.ReadAllTextAsync(Path.Combine(SaveRoot, "new.sav")));
        Assert.Single(await service.GetRestoreHistoryAsync(game.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CorruptPulledContentCannotMutateExistingSave()
    {
        Directory.CreateDirectory(SaveRoot);
        var path = Path.Combine(SaveRoot, "slot.sav");
        await File.WriteAllTextAsync(path, "known-good");
        var (service, transport) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var game = Game("Manual") with { SaveDirectory = SaveRoot };
        var baseline = await service.SnapshotAndPushAfterExitAsync(game, CancellationToken.None);
        var corrupt = System.Text.Encoding.UTF8.GetBytes("corrupt");
        transport.PullResult = new(new SaveSnapshotManifest(Guid.NewGuid(), baseline.SnapshotId, game.Id, Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new("slot.sav", corrupt.Length, new string('0', 64))], []), new Dictionary<string, byte[]> { ["slot.sav"] = corrupt });

        var result = await service.PullBeforeLaunchAsync(game, CancellationToken.None);

        Assert.Equal(SaveSyncStatus.Failed, result.Status);
        Assert.Equal("known-good", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ThousandsOfSmallFilesRemainBoundedAndCancelable()
    {
        Directory.CreateDirectory(SaveRoot);
        for (var index = 0; index < 2_000; index++)
            await File.WriteAllTextAsync(Path.Combine(SaveRoot, $"slot-{index:D4}.sav"), index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var (service, transport) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var game = Game("Manual") with { SaveDirectory = SaveRoot };

        var result = await service.SnapshotAndPushAfterExitAsync(game, CancellationToken.None);

        Assert.Equal(SaveSyncStatus.SnapshotCreated, result.Status);
        Assert.Equal(2_000, transport.LastPush!.Manifest.Files.Count);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SnapshotAndPushAfterExitAsync(game, canceled.Token));
    }

    [Fact]
    public async Task SnapshotHistoryIsIntegrityCheckedBoundedAndRestorable()
    {
        Directory.CreateDirectory(SaveRoot);
        var (service, _) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var game = Game("Manual") with { SaveDirectory = SaveRoot };
        for (var index = 0; index < SaveSyncCoordinator.MaximumRetainedSnapshotsPerGame + 3; index++)
        {
            await File.WriteAllTextAsync(Path.Combine(SaveRoot, "slot.sav"), $"generation-{index}");
            Assert.Equal(SaveSyncStatus.SnapshotCreated, (await service.SnapshotAndPushAfterExitAsync(game, CancellationToken.None)).Status);
        }

        var history = await service.GetSnapshotHistoryAsync(game.Id, CancellationToken.None);
        Assert.Equal(SaveSyncCoordinator.MaximumRetainedSnapshotsPerGame, history.Count);
        Assert.All(history, static item => Assert.True(item.IntegrityValid));
        Assert.True(Assert.Single(history, static item => item.IsHead).IntegrityValid);
        var restorePoint = history[^1];
        var storedFile = Path.Combine(_root, "managed", "SaveSync", "Snapshots", game.Id.Value.ToString("N"), restorePoint.SnapshotId.ToString("N"), "slot.sav");
        var expected = await File.ReadAllTextAsync(storedFile);
        await File.WriteAllTextAsync(Path.Combine(SaveRoot, "slot.sav"), "uncommitted-local-change");

        var restored = await service.RestoreSnapshotAsync(game, restorePoint.SnapshotId, CancellationToken.None);

        Assert.Equal(SaveSyncStatus.Applied, restored.Status);
        Assert.Equal(expected, await File.ReadAllTextAsync(Path.Combine(SaveRoot, "slot.sav")));
        for (var index = 0; index < SaveSyncCoordinator.MaximumRetainedRestoreBackupsPerGame; index++)
            Assert.Equal(SaveSyncStatus.Applied, (await service.RestoreSnapshotAsync(game, restorePoint.SnapshotId, CancellationToken.None)).Status);
        var restoreHistory = await service.GetRestoreHistoryAsync(game.Id, CancellationToken.None);
        Assert.Equal(SaveSyncCoordinator.MaximumRetainedRestoreBackupsPerGame, restoreHistory.Count);
        Assert.All(restoreHistory, static item => Assert.Equal("Completed", item.Outcome));
    }

    [Fact]
    public async Task IntegrityAuditReportsCorruptHistoryWithoutChangingSnapshots()
    {
        Directory.CreateDirectory(SaveRoot);
        var (service, _) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var game = Game("Manual") with { SaveDirectory = SaveRoot };
        await File.WriteAllTextAsync(Path.Combine(SaveRoot, "slot.sav"), "first");
        var first = await service.SnapshotAndPushAfterExitAsync(game, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(SaveRoot, "slot.sav"), "second");
        await service.SnapshotAndPushAfterExitAsync(game, CancellationToken.None);
        var storedFile = Path.Combine(_root, "managed", "SaveSync", "Snapshots", game.Id.Value.ToString("N"), first.SnapshotId!.Value.ToString("N"), "slot.sav");
        await File.WriteAllTextAsync(storedFile, "corrupt");

        var result = await service.VerifySnapshotsAsync(game.Id, CancellationToken.None);

        Assert.Equal(SaveSyncStatus.Failed, result.Status);
        Assert.Contains("historical", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(storedFile));
        Assert.False(Assert.Single(await service.GetSnapshotHistoryAsync(game.Id, CancellationToken.None), item => item.SnapshotId == first.SnapshotId).IntegrityValid);
    }

    [Fact]
    public async Task FirstPullNeverOverwritesExistingUntrackedSave()
    {
        Directory.CreateDirectory(SaveRoot);
        await File.WriteAllTextAsync(Path.Combine(SaveRoot, "slot.sav"), "local");
        var (service, transport) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var game = Game("Manual") with { SaveDirectory = SaveRoot };
        transport.PullResult = Payload(game.Id, "remote");

        var result = await service.PullBeforeLaunchAsync(game, CancellationToken.None);

        Assert.Equal(SaveSyncStatus.Conflict, result.Status);
        Assert.Equal("local", await File.ReadAllTextAsync(Path.Combine(SaveRoot, "slot.sav")));
        var comparison = Assert.Single(await service.GetConflictComparisonAsync(game, CancellationToken.None));
        Assert.Equal("slot.sav", comparison.RelativePath);
        Assert.Equal("Changed", comparison.Difference);
        Assert.Equal(5, comparison.LocalBytes);
        Assert.Equal(6, comparison.RemoteBytes);

        var resolved = await service.ResolveConflictAsync(game, SaveConflictChoice.KeepRemote, CancellationToken.None);
        Assert.Equal(SaveSyncStatus.Applied, resolved.Status);
        Assert.Equal("remote", await File.ReadAllTextAsync(Path.Combine(SaveRoot, "slot.sav")));
    }

    [Fact]
    public async Task EmptySaveFolderAcceptsVerifiedPull()
    {
        Directory.CreateDirectory(SaveRoot);
        var (service, transport) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var game = Game("Manual") with { SaveDirectory = SaveRoot };
        transport.PullResult = Payload(game.Id, "remote");

        var result = await service.PullBeforeLaunchAsync(game, CancellationToken.None);

        Assert.Equal(SaveSyncStatus.Applied, result.Status);
        Assert.Equal("remote", await File.ReadAllTextAsync(Path.Combine(SaveRoot, "slot.sav")));
    }

    [Fact]
    public async Task EmptyFolderCannotPublishTheFirstSnapshot()
    {
        Directory.CreateDirectory(SaveRoot);
        var (service, transport) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var game = Game("Manual") with { SaveDirectory = SaveRoot, SaveSyncId = Guid.NewGuid() };

        var result = await service.SnapshotAndPushAfterExitAsync(game, CancellationToken.None);

        Assert.Equal(SaveSyncStatus.Unchanged, result.Status);
        Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(transport.LastPush);
    }

    [Fact]
    public async Task SharedSaveIdentityLinksSeparatelyAddedManualGames()
    {
        Directory.CreateDirectory(SaveRoot);
        var shared = Guid.NewGuid();
        var (service, transport) = Create();
        await service.InitializeAsync(CancellationToken.None);
        var localGame = Game("Manual") with { SaveDirectory = SaveRoot, SaveSyncId = shared };
        var separatelyAddedPeerGame = Game("Manual") with { SaveSyncId = shared };
        transport.PullResult = Payload(new GameId(shared), "linked-remote-save");

        var result = await service.PullBeforeLaunchAsync(localGame, CancellationToken.None);

        Assert.NotEqual(localGame.Id, separatelyAddedPeerGame.Id);
        Assert.Equal(SaveSyncStatus.Applied, result.Status);
        Assert.Equal(new GameId(shared), transport.LastPullGameId);
        Assert.Equal("linked-remote-save", await File.ReadAllTextAsync(Path.Combine(SaveRoot, "slot.sav")));
    }

    [Fact]
    public async Task PairedCredentialDerivesSameIdentityWithoutCopyingARecoveryLink()
    {
        var (deviceA, _) = Create();
        var (deviceB, _) = Create();
        await deviceA.InitializeAsync(CancellationToken.None);
        await deviceB.InitializeAsync(CancellationToken.None);

        var first = deviceA.DeriveSharedSaveIdentity("  My   Game ", "Windows");
        var second = deviceB.DeriveSharedSaveIdentity("my game", " windows ");
        var differentEdition = deviceB.DeriveSharedSaveIdentity("My Game Remastered", "Windows");

        Assert.Null(first.Error);
        Assert.Equal(first.Identity, second.Identity);
        Assert.NotEqual(first.Identity, differentEdition.Identity);
    }

    [Fact]
    public async Task InvitationExpiresAfterTwentyFourHoursAndPinsOnlyFirstDevice()
    {
        var now = new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
        var inviterStore = new Store();
        var inviterSecret = new Secret(configured: false);
        var inviterClock = new Clock(now);
        var inviter = new SaveSyncCoordinator(inviterStore, new FakeTransport(), inviterSecret, inviterClock, Path.Combine(_root, "inviter"), TimeSpan.Zero);
        await inviter.InitializeAsync(CancellationToken.None);
        Assert.Null(await inviter.ConfigurePeerAsync("100.64.0.3", CancellationToken.None));
        var invitation = await inviter.GeneratePairingCodeAsync(CancellationToken.None);
        Assert.Matches("^[0-9]{3} [0-9]{3}$", invitation);

        var receiverTransport = new FakeTransport { Redeemer = inviter.RedeemInvitationAsync };
        var receiver = new SaveSyncCoordinator(new Store(), receiverTransport, new Secret(configured: false), new Clock(now.AddHours(23)), Path.Combine(_root, "receiver"), TimeSpan.Zero);
        await receiver.InitializeAsync(CancellationToken.None);
        Assert.Null(await receiver.ConfigurePeerAsync("100.64.0.2", CancellationToken.None));
        Assert.Null(await receiver.ApplyPairingCodeAsync(invitation, CancellationToken.None));
        Assert.True(receiver.IsPaired);
        Assert.Equal(inviter.Settings.DeviceId, receiver.Settings.PeerDeviceId);
        Assert.True(await inviter.AuthorizePeerAsync(receiver.Settings.DeviceId, CancellationToken.None));
        Assert.Null(await receiver.RevokePeerAsync(CancellationToken.None));
        Assert.False(receiver.IsPaired);
        Assert.NotNull(await receiver.ApplyPairingCodeAsync(invitation, CancellationToken.None));

        Assert.False(await inviter.AuthorizePeerAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(receiver.Settings.DeviceId, inviter.Settings.PeerDeviceId);

        var expiringClock = new Clock(now);
        var expiringInviter = new SaveSyncCoordinator(new Store(), new FakeTransport(), new Secret(configured: false), expiringClock, Path.Combine(_root, "expiring-inviter"), TimeSpan.Zero);
        await expiringInviter.InitializeAsync(CancellationToken.None);
        Assert.Null(await expiringInviter.ConfigurePeerAsync("100.64.0.3", CancellationToken.None));
        var expiredCode = await expiringInviter.GeneratePairingCodeAsync(CancellationToken.None);
        expiringClock.Now = now.AddHours(25);
        var expiredTransport = new FakeTransport { Redeemer = expiringInviter.RedeemInvitationAsync };
        var expired = new SaveSyncCoordinator(new Store(), expiredTransport, new Secret(configured: false), new Clock(now.AddHours(25)), Path.Combine(_root, "expired"), TimeSpan.Zero);
        await expired.InitializeAsync(CancellationToken.None);
        Assert.Null(await expired.ConfigurePeerAsync("100.64.0.2", CancellationToken.None));
        var error = await expired.ApplyPairingCodeAsync(expiredCode, CancellationToken.None);
        Assert.Contains("expired", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(expired.IsPaired);

        var locked = new SaveSyncCoordinator(new Store(), new FakeTransport(), new Secret(configured: false), new Clock(now), Path.Combine(_root, "locked"), TimeSpan.Zero);
        await locked.InitializeAsync(CancellationToken.None);
        Assert.Null(await locked.ConfigurePeerAsync("100.64.0.3", CancellationToken.None));
        var validCode = await locked.GeneratePairingCodeAsync(CancellationToken.None);
        var attacker = Guid.NewGuid();
        Assert.Equal(2, (await locked.RedeemInvitationAsync("000 000" == validCode ? "000 001" : "000 000", attacker, CancellationToken.None)).RemainingAttempts);
        Assert.Equal(1, (await locked.RedeemInvitationAsync("111 111" == validCode ? "111 112" : "111 111", attacker, CancellationToken.None)).RemainingAttempts);
        Assert.Equal(0, (await locked.RedeemInvitationAsync("222 222" == validCode ? "222 223" : "222 222", attacker, CancellationToken.None)).RemainingAttempts);
        Assert.Contains("locked", (await locked.RedeemInvitationAsync(validCode, attacker, CancellationToken.None)).Error, StringComparison.OrdinalIgnoreCase);
    }

    private (SaveSyncCoordinator Service, FakeTransport Transport) Create()
    {
        var transport = new FakeTransport();
        var initial = SaveSyncDocument.CreateDefault();
        initial = initial with { Settings = initial.Settings with { PeerDeviceId = Guid.NewGuid(), PeerAddress = "100.64.0.2" } };
        var service = new SaveSyncCoordinator(new Store(initial), transport, new Secret(), TimeProvider.System, Path.Combine(_root, "managed"), TimeSpan.Zero);
        return (service, transport);
    }

    private static LibraryItem Game(string source) => new(
        new GameId(Guid.NewGuid()), "Game", "Windows", source,
        new LaunchTarget("C:\\Game.exe", [], "C:\\", LaunchTargetKind.Executable),
        new GameMetadata(null, null, null, null, null, null), false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static SaveSnapshotPayload Payload(GameId gameId, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var manifest = new SaveSnapshotManifest(Guid.NewGuid(), null, gameId, Guid.NewGuid(), DateTimeOffset.UtcNow, [new("slot.sav", bytes.Length, hash)], []);
        return new(manifest, new Dictionary<string, byte[]> { ["slot.sav"] = bytes });
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); GC.SuppressFinalize(this); }

    private sealed class Store(SaveSyncDocument? initial = null) : IDocumentStore<SaveSyncDocument>
    {
        private SaveSyncDocument? _value = initial;
        public Task<DocumentLoadResult<SaveSyncDocument>> LoadAsync(CancellationToken token) => Task.FromResult(_value is null ? new(DocumentLoadStatus.NotFound, null, null) : new DocumentLoadResult<SaveSyncDocument>(DocumentLoadStatus.Loaded, _value, null));
        public Task<DocumentSaveResult> SaveAsync(SaveSyncDocument document, CancellationToken token) { _value = document; return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null)); }
    }
    private sealed class Secret(bool configured = true) : IPairingSecretStore { private byte[]? _secret = configured ? new byte[32] : null; public bool HasSecret => _secret is not null; public byte[]? GetSecret() => _secret?.ToArray(); public void SetSecret(ReadOnlySpan<byte> secret) => _secret = secret.ToArray(); public void Clear() => _secret = null; }
    private sealed class PeerCredentials : IPeerCredentialStore
    {
        private readonly Dictionary<Guid, byte[]> _secrets = [];
        private readonly Dictionary<Guid, byte[]> _pending = [];
        public bool ContainsSecret(Guid peerDeviceId) => _secrets.ContainsKey(peerDeviceId);
        public byte[]? GetSecret(Guid peerDeviceId) => _secrets.GetValueOrDefault(peerDeviceId)?.ToArray();
        public void SetSecret(Guid peerDeviceId, ReadOnlySpan<byte> secret) => _secrets[peerDeviceId] = secret.ToArray();
        public void Clear(Guid peerDeviceId) => _secrets.Remove(peerDeviceId);
        public string GetCredentialReference(Guid peerDeviceId) => $"test/{peerDeviceId:N}";
        public bool ContainsPendingSecret(Guid peerDeviceId) => _pending.ContainsKey(peerDeviceId);
        public byte[]? GetPendingSecret(Guid peerDeviceId) => _pending.GetValueOrDefault(peerDeviceId)?.ToArray();
        public void SetPendingSecret(Guid peerDeviceId, ReadOnlySpan<byte> secret) => _pending[peerDeviceId] = secret.ToArray();
        public void PromotePendingSecret(Guid peerDeviceId) { _secrets[peerDeviceId] = _pending[peerDeviceId]; _pending.Remove(peerDeviceId); }
        public void ClearPendingSecret(Guid peerDeviceId) => _pending.Remove(peerDeviceId);
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class FakeTransport : ISaveSyncTransport
    {
        public event Action<SaveTransferProgress>? ProgressChanged { add { } remove { } }
        public bool IsConfigured => true;
        public bool IsListening { get; private set; }
        public string ListenerStatus => IsListening ? "Listening." : "Not listening.";
        public SaveSnapshotPayload? LastPush { get; private set; }
        public SaveSnapshotPayload? PullResult { get; set; }
        public GameId? LastPullGameId { get; private set; }
        public Guid? FailedPushPeer { get; init; }
        public List<Guid> PushedPeers { get; } = [];
        public Dictionary<Guid, SaveSnapshotPayload> Pulls { get; } = [];
        public Func<string, Guid, CancellationToken, Task<PairingRedemptionResult>>? Redeemer { get; init; }
        public Task StartAsync(ISaveSyncPeerEndpoint endpoint, CancellationToken token) { IsListening = true; return Task.CompletedTask; }
        public Task<TransportResult> PullAsync(GameId gameId, Guid? knownHead, CancellationToken token) { LastPullGameId = gameId; return Task.FromResult(new TransportResult(true, false, PullResult, null)); }
        public Task<TransportResult> PushAsync(SaveSnapshotPayload snapshot, CancellationToken token) { LastPush = snapshot; return Task.FromResult(new TransportResult(true, false, null, null)); }
        public Task<TransportResult> PullAsync(TrustedSaveSyncPeer peer, GameId gameId, Guid? knownHead, CancellationToken token)
        { LastPullGameId = gameId; return Task.FromResult(new TransportResult(true, false, Pulls.GetValueOrDefault(peer.DeviceId) ?? PullResult, null)); }
        public Task<TransportResult> PushAsync(TrustedSaveSyncPeer peer, SaveSnapshotPayload snapshot, CancellationToken token)
        { PushedPeers.Add(peer.DeviceId); LastPush = snapshot; return Task.FromResult(peer.DeviceId == FailedPushPeer ? new TransportResult(false, false, null, "offline") : new TransportResult(true, false, null, null)); }
        public Task<PairingRedemptionResult> RedeemInvitationAsync(string code, Guid deviceId, CancellationToken token) => Redeemer is null ? Task.FromResult(new PairingRedemptionResult(false, null, null, "No inviter.")) : Redeemer(code, deviceId, token);
        public Task<string?> RotatePeerCredentialAsync(TrustedSaveSyncPeer peer, byte[] newSecret, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> CancelPartialTransfersAsync(CancellationToken token) => Task.FromResult<string?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
