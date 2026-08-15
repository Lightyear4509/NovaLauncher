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
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class FakeTransport : ISaveSyncTransport
    {
        public bool IsConfigured => true;
        public bool IsListening { get; private set; }
        public string ListenerStatus => IsListening ? "Listening." : "Not listening.";
        public SaveSnapshotPayload? LastPush { get; private set; }
        public SaveSnapshotPayload? PullResult { get; set; }
        public GameId? LastPullGameId { get; private set; }
        public Func<string, Guid, CancellationToken, Task<PairingRedemptionResult>>? Redeemer { get; init; }
        public Task StartAsync(ISaveSyncPeerEndpoint endpoint, CancellationToken token) { IsListening = true; return Task.CompletedTask; }
        public Task<TransportResult> PullAsync(GameId gameId, Guid? knownHead, CancellationToken token) { LastPullGameId = gameId; return Task.FromResult(new TransportResult(true, false, PullResult, null)); }
        public Task<TransportResult> PushAsync(SaveSnapshotPayload snapshot, CancellationToken token) { LastPush = snapshot; return Task.FromResult(new TransportResult(true, false, null, null)); }
        public Task<PairingRedemptionResult> RedeemInvitationAsync(string code, Guid deviceId, CancellationToken token) => Redeemer is null ? Task.FromResult(new PairingRedemptionResult(false, null, null, "No inviter.")) : Redeemer(code, deviceId, token);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
