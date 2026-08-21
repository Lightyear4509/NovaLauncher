using System.Security.Cryptography;
using NovaLauncher.Application.GameTransfer;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;

namespace NovaLauncher.Application.Tests;

public sealed class GameTransferCoordinatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"NovaLauncher-Transfer-{Guid.NewGuid():N}");

    [Fact]
    public async Task PreviewRejectsSteamAndManagedStoreRoots()
    {
        var source = Path.Combine(_root, "steamapps", "common", "Game");
        Directory.CreateDirectory(source);
        var executable = Path.Combine(source, "game.exe");
        await File.WriteAllBytesAsync(executable, [1]);
        using var service = CreateService(out _, out _);

        Assert.False((await service.PreviewAsync(Game("Steam", executable), source, CancellationToken.None)).Accepted);
        var result = await service.PreviewAsync(Game("Manual", executable), source, CancellationToken.None);
        Assert.False(result.Accepted);
        Assert.Contains("store-managed", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthorizationRequiresRightsAndActiveNamedRecipient()
    {
        var source = await CreateSourceAsync();
        var game = Game("Manual", Path.Combine(source, "game.exe"));
        using var service = CreateService(out _, out var saveSync);
        var preview = await service.PreviewAsync(game, source, CancellationToken.None);

        Assert.True(preview.Accepted, preview.Error);
        Assert.False((await service.AuthorizeAsync(game, preview, [saveSync.Peer.DeviceId], false, CancellationToken.None)).Success);
        Assert.False((await service.AuthorizeAsync(game, preview, [Guid.NewGuid()], true, CancellationToken.None)).Success);
        Assert.True((await service.AuthorizeAsync(game, preview, [saveSync.Peer.DeviceId], true, CancellationToken.None)).Success);
        Assert.Single(await service.ListAuthorizedGameTransfersAsync(saveSync.Peer.DeviceId, CancellationToken.None));
        Assert.Empty(await service.ListAuthorizedGameTransfersAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ChunkServingRejectsMutationAndTraversal()
    {
        var source = await CreateSourceAsync();
        var game = Game("Manual", Path.Combine(source, "game.exe"));
        using var service = CreateService(out _, out var saveSync);
        var preview = await service.PreviewAsync(game, source, CancellationToken.None);
        await service.AuthorizeAsync(game, preview, [saveSync.Peer.DeviceId], true, CancellationToken.None);
        var offer = Assert.Single(await service.ListAuthorizedGameTransfersAsync(saveSync.Peer.DeviceId, CancellationToken.None));

        Assert.False((await service.ServeGameTransferChunkAsync(saveSync.Peer.DeviceId, offer.OfferId, "../escape", 0, 10, CancellationToken.None)).Success);
        await File.AppendAllTextAsync(Path.Combine(source, "data.bin"), "changed");
        Assert.False((await service.ServeGameTransferChunkAsync(saveSync.Peer.DeviceId, offer.OfferId, "data.bin", 0, 10, CancellationToken.None)).Success);
    }

    [Fact]
    public async Task DownloadResumesVerifiesAndNeverLaunches()
    {
        var peerId = Guid.NewGuid();
        var bytes = Enumerable.Range(0, GameTransferCoordinator.ChunkBytes + 37).Select(static value => (byte)(value % 251)).ToArray();
        var file = new GameTransferFile("game.exe", bytes.Length, Hash(bytes), DateTimeOffset.UtcNow);
        var manifest = new GameTransferManifest(Guid.NewGuid(), new GameId(Guid.NewGuid()), "Package", peerId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), [file], bytes.Length);
        var transport = new MemoryTransport(manifest, new Dictionary<string, byte[]> { [file.RelativePath] = bytes });
        var saveSync = new SaveSyncStub(peerId);
        using var service = new GameTransferCoordinator(transport, saveSync, TimeProvider.System, Path.Combine(_root, "data"));
        var offer = new PeerGameTransferOffer(saveSync.Peer, manifest);
        var destination = Path.Combine(_root, "received");
        var stage = Path.Combine(_root, $".novalauncher-transfer-{manifest.OfferId:N}");
        Directory.CreateDirectory(stage);
        await File.WriteAllBytesAsync(Path.Combine(stage, "game.exe"), bytes[..100]);

        var result = await service.DownloadAsync(offer, destination, null, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(destination, "game.exe")));
        Assert.True(transport.Offsets[0] >= 100);
        Assert.Contains("did not install or launch", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(await service.GetHistoryAsync(CancellationToken.None), item => item.Outcome.Contains("not launched", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HashMismatchDiscardsBadFileAndRetainsResumableStage()
    {
        Directory.CreateDirectory(_root);
        var peerId = Guid.NewGuid();
        var good = new byte[] { 1, 2, 3 };
        var manifest = new GameTransferManifest(Guid.NewGuid(), new GameId(Guid.NewGuid()), "Package", peerId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            [new("data.bin", good.Length, Hash(good), DateTimeOffset.UtcNow)], good.Length);
        var transport = new MemoryTransport(manifest, new Dictionary<string, byte[]> { ["data.bin"] = [9, 9, 9] });
        var saveSync = new SaveSyncStub(peerId);
        using var service = new GameTransferCoordinator(transport, saveSync, TimeProvider.System, Path.Combine(_root, "data2"));

        var result = await service.DownloadAsync(new(saveSync.Peer, manifest), Path.Combine(_root, "bad"), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Resumable, result.Message);
        Assert.False(File.Exists(Path.Combine(_root, $".novalauncher-transfer-{manifest.OfferId:N}", "data.bin")));
    }

    [Fact]
    public async Task CancellationRetainsVerifiedOffsetAndRetryResumes()
    {
        Directory.CreateDirectory(_root);
        var peerId = Guid.NewGuid();
        var bytes = new byte[GameTransferCoordinator.ChunkBytes * 2 + 17]; RandomNumberGenerator.Fill(bytes);
        var file = new GameTransferFile("data.bin", bytes.Length, Hash(bytes), DateTimeOffset.UtcNow);
        var manifest = new GameTransferManifest(Guid.NewGuid(), new GameId(Guid.NewGuid()), "Package", peerId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), [file], bytes.Length);
        var transport = new MemoryTransport(manifest, new Dictionary<string, byte[]> { [file.RelativePath] = bytes }) { CancelAfterCalls = 1 };
        var saveSync = new SaveSyncStub(peerId);
        using var service = new GameTransferCoordinator(transport, saveSync, TimeProvider.System, Path.Combine(_root, "audit"));
        var offer = new PeerGameTransferOffer(saveSync.Peer, manifest); var destination = Path.Combine(_root, "resumed");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DownloadAsync(offer, destination, null, CancellationToken.None));
        var retained = new FileInfo(Path.Combine(_root, $".novalauncher-transfer-{manifest.OfferId:N}", file.RelativePath)).Length;
        Assert.Equal(GameTransferCoordinator.ChunkBytes, retained);
        transport.CancelAfterCalls = null; transport.Offsets.Clear();
        var resumed = await service.DownloadAsync(offer, destination, null, CancellationToken.None);

        Assert.True(resumed.Success, resumed.Message);
        Assert.Equal(retained, transport.Offsets[0]);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(destination, file.RelativePath)));
    }

    [Fact]
    public async Task SecurityScannerFailureBlocksPromotion()
    {
        Directory.CreateDirectory(_root);
        var peerId = Guid.NewGuid(); var bytes = new byte[] { 1, 2, 3 };
        var file = new GameTransferFile("game.exe", bytes.Length, Hash(bytes), DateTimeOffset.UtcNow);
        var manifest = new GameTransferManifest(Guid.NewGuid(), new GameId(Guid.NewGuid()), "Package", peerId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), [file], bytes.Length);
        var transport = new MemoryTransport(manifest, new Dictionary<string, byte[]> { [file.RelativePath] = bytes }); var saveSync = new SaveSyncStub(peerId);
        using var service = new GameTransferCoordinator(transport, saveSync, TimeProvider.System, Path.Combine(_root, "scan"), new RejectingScanner());
        var destination = Path.Combine(_root, "blocked");

        var result = await service.DownloadAsync(new(saveSync.Peer, manifest), destination, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(Directory.Exists(destination));
        Assert.Contains("Windows Security", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadRejectsUnmanifestedContentInResumableStage()
    {
        Directory.CreateDirectory(_root);
        var peerId = Guid.NewGuid(); var bytes = new byte[] { 1, 2, 3 };
        var file = new GameTransferFile("game.exe", bytes.Length, Hash(bytes), DateTimeOffset.UtcNow);
        var manifest = new GameTransferManifest(Guid.NewGuid(), new GameId(Guid.NewGuid()), "Package", peerId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), [file], bytes.Length);
        var transport = new MemoryTransport(manifest, new Dictionary<string, byte[]> { [file.RelativePath] = bytes }); var saveSync = new SaveSyncStub(peerId);
        using var service = new GameTransferCoordinator(transport, saveSync, TimeProvider.System, Path.Combine(_root, "tamper"));
        var stage = Path.Combine(_root, $".novalauncher-transfer-{manifest.OfferId:N}");
        Directory.CreateDirectory(stage);
        await File.WriteAllTextAsync(Path.Combine(stage, "unmanifested.exe"), "unexpected");

        var result = await service.DownloadAsync(new(saveSync.Peer, manifest), Path.Combine(_root, "received-tampered"), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Resumable);
        Assert.Contains("unexpected or unsafe file", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(_root, "received-tampered")));
        Assert.Empty(transport.Offsets);
    }

    private GameTransferCoordinator CreateService(out MemoryTransport transport, out SaveSyncStub saveSync)
    {
        saveSync = new(Guid.NewGuid());
        transport = new(null, new Dictionary<string, byte[]>());
        return new(transport, saveSync, TimeProvider.System, Path.Combine(_root, "data"));
    }
    private async Task<string> CreateSourceAsync()
    {
        var source = Path.Combine(_root, "DRM-Free-Game"); Directory.CreateDirectory(source);
        await File.WriteAllBytesAsync(Path.Combine(source, "game.exe"), [1, 2, 3]);
        await File.WriteAllTextAsync(Path.Combine(source, "data.bin"), "content");
        return source;
    }
    private static LibraryItem Game(string source, string executable) => new(new GameId(Guid.NewGuid()), "Game", "Windows", source, new(executable, [], Path.GetDirectoryName(executable), LaunchTargetKind.Executable), new(null, null, null, null, null, null), false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); GC.SuppressFinalize(this); }

    private sealed class MemoryTransport(GameTransferManifest? manifest, IReadOnlyDictionary<string, byte[]> files) : IPeerGameTransferTransport
    {
        public List<long> Offsets { get; } = [];
        public int? CancelAfterCalls { get; set; }
        public void AttachGameTransferEndpoint(IPeerGameTransferEndpoint endpoint) { }
        public Task<IReadOnlyList<GameTransferManifest>> ListGameTransferOffersAsync(TrustedSaveSyncPeer peer, CancellationToken token) => Task.FromResult<IReadOnlyList<GameTransferManifest>>(manifest is null ? [] : [manifest]);
        public Task<GameTransferChunkResult> PullGameTransferChunkAsync(TrustedSaveSyncPeer peer, Guid offerId, string path, long offset, int maximum, CancellationToken token)
        { if (CancelAfterCalls is { } limit && Offsets.Count >= limit) throw new OperationCanceledException(); Offsets.Add(offset); var source = files[path]; var count = Math.Min(maximum, source.Length - (int)offset); return Task.FromResult(new GameTransferChunkResult(true, source.AsSpan((int)offset, count).ToArray(), offset + count == source.Length, null)); }
    }

    private sealed class RejectingScanner : IReceivedContentScanner
    { public Task<ReceivedContentScanResult> ScanAsync(string directory, CancellationToken token) => Task.FromResult(new ReceivedContentScanResult(true, false, "Test threat")); }

    private sealed class SaveSyncStub(Guid peerId) : ISaveSyncService
    {
        public TrustedSaveSyncPeer Peer { get; } = new(peerId, "Peer", "100.64.0.2", $"test/{peerId:N}", TrustedPeerState.Active, DateTimeOffset.UtcNow);
        public SaveSyncSettings Settings => new(Guid.NewGuid(), "Local", "100.64.0.2", peerId, SaveSyncSettings.DefaultPort, [], TrustedPeers: [Peer]);
        public bool IsPaired => true; public bool IsListening => true; public string ListenerStatus => "Listening";
        public Task<string?> InitializeAsync(CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string> GeneratePairingCodeAsync(CancellationToken token) => Task.FromResult("123 456");
        public Task<string?> ApplyPairingCodeAsync(string code, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> RevokePeerAsync(CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> RenamePeerAsync(Guid id, string name, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> SetPeerPausedAsync(Guid id, bool paused, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> RevokePeerAsync(Guid id, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> RotatePeerCredentialAsync(Guid id, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> ConfigurePeerAsync(string address, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> RetryListenerAsync(CancellationToken token) => Task.FromResult<string?>(null);
        public (Guid? Identity, string? Error) DeriveSharedSaveIdentity(string label, string platform) => (null, null);
        public Task<int> RetryPendingUploadsAsync(CancellationToken token) => Task.FromResult(0);
        public Task<SaveSyncResult> PullBeforeLaunchAsync(LibraryItem game, CancellationToken token) => Task.FromResult(new SaveSyncResult(SaveSyncStatus.Unchanged, ""));
        public Task<SaveSyncResult> SnapshotAndPushAfterExitAsync(LibraryItem game, CancellationToken token) => Task.FromResult(new SaveSyncResult(SaveSyncStatus.Unchanged, ""));
        public Task<SaveSyncResult> ResolveConflictAsync(LibraryItem game, SaveConflictChoice choice, CancellationToken token) => Task.FromResult(new SaveSyncResult(SaveSyncStatus.Unchanged, ""));
        public Task<IReadOnlyList<SaveSnapshotHistoryItem>> GetSnapshotHistoryAsync(GameId id, CancellationToken token) => Task.FromResult<IReadOnlyList<SaveSnapshotHistoryItem>>([]);
        public Task<SaveSyncResult> RestoreSnapshotAsync(LibraryItem game, Guid id, CancellationToken token) => Task.FromResult(new SaveSyncResult(SaveSyncStatus.Unchanged, ""));
        public Task<IReadOnlyList<SaveRestoreHistoryItem>> GetRestoreHistoryAsync(GameId id, CancellationToken token) => Task.FromResult<IReadOnlyList<SaveRestoreHistoryItem>>([]);
    }
}
