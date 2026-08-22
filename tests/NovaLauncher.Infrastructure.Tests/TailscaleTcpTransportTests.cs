using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Application.GameTransfer;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;
using NovaLauncher.Infrastructure.SaveSync;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class TailscaleTcpTransportTests
{
    [Fact]
    public async Task AuthenticatedEncryptedLoopbackTransfersSnapshotAndPinsIdentities()
    {
        var portA = FreePort();
        var portB = FreePort();
        while (portB == portA) portB = FreePort();
        var secret = RandomNumberGenerator.GetBytes(32);
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var credentialA = new Secret(secret);
        var credentialB = new Secret(secret);
        var settingsA = Settings(idA, "A", idB, portB, credentialA);
        var settingsB = Settings(idB, "B", idA, portA, credentialB);
        await using var a = new TailscaleTcpTransport(() => settingsA, credentialA, IPAddress.Loopback, portA, credentialA);
        await using var b = new TailscaleTcpTransport(() => settingsB, credentialB, IPAddress.Loopback, portB, credentialB);
        var endpointA = new Endpoint(idB);
        var endpointB = new Endpoint(idA);
        await a.StartAsync(endpointA, CancellationToken.None);
        await b.StartAsync(endpointB, CancellationToken.None);
        var payload = Payload();
        var progress = new List<SaveTransferProgress>();
        a.ProgressChanged += progress.Add;

        var result = await a.PushAsync(payload, CancellationToken.None);

        Assert.True(result.Success, $"{result.Error}; server={b.LastServerError}");
        Assert.Equal(payload.Manifest.SnapshotId, endpointB.Received!.Manifest.SnapshotId);
        Assert.Equal("secret-save", System.Text.Encoding.UTF8.GetString(endpointB.Received.ChangedFiles["slot.sav"]));
        Assert.Contains(progress, static item => item.Status == "Completed and verified" && item.BytesTransferred == item.TotalBytes);
    }

    [Fact]
    public async Task WrongPairingSecretCannotAuthenticateOrDeliverPayload()
    {
        var portA = FreePort();
        var portB = FreePort();
        while (portB == portA) portB = FreePort();
        var credentialA = new Secret(RandomNumberGenerator.GetBytes(32));
        var credentialB = new Secret(RandomNumberGenerator.GetBytes(32));
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var settingsA = Settings(idA, "A", idB, portB, credentialA);
        var settingsB = Settings(idB, "B", idA, portA, credentialB);
        await using var a = new TailscaleTcpTransport(() => settingsA, credentialA, IPAddress.Loopback, portA, credentialA);
        await using var b = new TailscaleTcpTransport(() => settingsB, credentialB, IPAddress.Loopback, portB, credentialB);
        var endpoint = new Endpoint(settingsA.DeviceId);
        await a.StartAsync(new Endpoint(settingsB.DeviceId), CancellationToken.None);
        await b.StartAsync(endpoint, CancellationToken.None);

        var result = await a.PushAsync(Payload(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(endpoint.Received);
    }

    [Fact]
    public async Task AuthenticatedDownloadResumesDurablePartialAndReportsCompletion()
    {
        var portA = FreePort(); var portB = FreePort(); while (portB == portA) portB = FreePort();
        var secret = RandomNumberGenerator.GetBytes(32); var idA = Guid.NewGuid(); var idB = Guid.NewGuid();
        var credentialA = new Secret(secret); var credentialB = new Secret(secret);
        var settingsA = Settings(idA, "A", idB, portB, credentialA); var settingsB = Settings(idB, "B", idA, portA, credentialB);
        var resumeRoot = Path.Combine(Path.GetTempPath(), $"NovaLauncher-Pull-{Guid.NewGuid():N}");
        try
        {
            await using var a = new TailscaleTcpTransport(() => settingsA, credentialA, IPAddress.Loopback, portA, credentialA, resumeRoot);
            await using var b = new TailscaleTcpTransport(() => settingsB, credentialB, IPAddress.Loopback, portB, credentialB);
            var payload = Payload();
            await a.StartAsync(new Endpoint(idB), CancellationToken.None);
            await b.StartAsync(new Endpoint(idA) { Outgoing = payload }, CancellationToken.None);
            var partial = Path.Combine(resumeRoot, idB.ToString("N"), payload.Manifest.SnapshotId.ToString("N"), "slot.sav");
            Directory.CreateDirectory(Path.GetDirectoryName(partial)!);
            await File.WriteAllBytesAsync(partial, payload.ChangedFiles["slot.sav"][..3]);
            var progress = new List<SaveTransferProgress>();
            a.ProgressChanged += progress.Add;

            var result = await a.PullAsync(settingsA.EffectiveTrustedPeers[0], payload.Manifest.GameId, null, CancellationToken.None);

            Assert.True(result.Success, result.Error);
            Assert.Equal(payload.ChangedFiles["slot.sav"], result.Snapshot!.ChangedFiles["slot.sav"]);
            Assert.Contains(progress, static item => item.Direction == "Download" && item.Status == "Completed and verified" && item.BytesTransferred == item.TotalBytes);
        }
        finally
        {
            if (Directory.Exists(resumeRoot)) Directory.Delete(resumeRoot, true);
        }
    }

    [Fact]
    public async Task TwoPhaseCredentialRotationKeepsEncryptedPeerUsable()
    {
        var portA = FreePort(); var portB = FreePort(); while (portB == portA) portB = FreePort();
        var oldSecret = RandomNumberGenerator.GetBytes(32); var replacement = RandomNumberGenerator.GetBytes(32);
        var idA = Guid.NewGuid(); var idB = Guid.NewGuid();
        var credentialA = new Secret(oldSecret); var credentialB = new Secret(oldSecret);
        var settingsA = Settings(idA, "A", idB, portB, credentialA); var settingsB = Settings(idB, "B", idA, portA, credentialB);
        await using var a = new TailscaleTcpTransport(() => settingsA, credentialA, IPAddress.Loopback, portA, credentialA);
        await using var b = new TailscaleTcpTransport(() => settingsB, credentialB, IPAddress.Loopback, portB, credentialB);
        var endpointB = new Endpoint(idA) { RotationCredentials = credentialB };
        await a.StartAsync(new Endpoint(idB), CancellationToken.None);
        await b.StartAsync(endpointB, CancellationToken.None);
        credentialA.SetPendingSecret(idB, replacement);

        var error = await a.RotatePeerCredentialAsync(settingsA.EffectiveTrustedPeers[0], replacement, CancellationToken.None);
        Assert.Null(error);
        credentialA.PromotePendingSecret(idB);
        var result = await a.PushAsync(Payload(), CancellationToken.None);

        Assert.True(result.Success, $"{result.Error}; server={b.LastServerError}");
        Assert.Equal(replacement, credentialA.GetSecret(idB));
        Assert.Equal(replacement, credentialB.GetSecret(idA));
    }

    [Fact]
    public async Task PendingCredentialRecoversAfterPeerCommittedBeforeClientAcknowledgement()
    {
        var portA = FreePort(); var portB = FreePort(); while (portB == portA) portB = FreePort();
        var oldSecret = RandomNumberGenerator.GetBytes(32); var replacement = RandomNumberGenerator.GetBytes(32);
        var idA = Guid.NewGuid(); var idB = Guid.NewGuid();
        var credentialA = new Secret(oldSecret); var credentialB = new Secret(replacement);
        credentialA.SetPendingSecret(idB, replacement);
        var settingsA = Settings(idA, "A", idB, portB, credentialA); var settingsB = Settings(idB, "B", idA, portA, credentialB);
        await using var a = new TailscaleTcpTransport(() => settingsA, credentialA, IPAddress.Loopback, portA, credentialA);
        await using var b = new TailscaleTcpTransport(() => settingsB, credentialB, IPAddress.Loopback, portB, credentialB);
        await a.StartAsync(new Endpoint(idB), CancellationToken.None);
        await b.StartAsync(new Endpoint(idA), CancellationToken.None);

        var result = await a.PushAsync(Payload(), CancellationToken.None);

        Assert.True(result.Success, $"{result.Error}; server={b.LastServerError}");
        Assert.Equal(replacement, credentialA.GetSecret(idB));
        Assert.False(credentialA.ContainsPendingSecret(idB));
    }

    [Fact]
    public async Task SixDigitBootstrapReturnsStrongCredentialOverTailscaleChannel()
    {
        var localPort = FreePort();
        var remotePort = FreePort();
        while (remotePort == localPort) remotePort = FreePort();
        var requesterId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var strongSecret = RandomNumberGenerator.GetBytes(32);
        var requesterSettings = new SaveSyncSettings(requesterId, "Requester", "127.0.0.1", null, remotePort, []);
        var inviterSettings = new SaveSyncSettings(inviterId, "Inviter", "127.0.0.1", null, localPort, []);
        await using var requester = new TailscaleTcpTransport(() => requesterSettings, new Secret([]), IPAddress.Loopback, localPort);
        await using var inviter = new TailscaleTcpTransport(() => inviterSettings, new Secret(strongSecret), IPAddress.Loopback, remotePort);
        var endpoint = new Endpoint(requesterId)
        {
            Redeemer = (code, deviceId) => code == "123456" && deviceId == requesterId
                ? new PairingRedemptionResult(true, inviterId, strongSecret.ToArray(), null)
                : new PairingRedemptionResult(false, null, null, "Rejected"),
        };
        await inviter.StartAsync(endpoint, CancellationToken.None);

        var result = await requester.RedeemInvitationAsync("123456", requesterId, CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(inviterId, result.PeerDeviceId);
        Assert.Equal(strongSecret, result.Secret);
    }

    [Fact]
    public async Task AuthenticatedPeerCanListAuthorizedOfferAndPullBoundedChunk()
    {
        var portA = FreePort(); var portB = FreePort(); while (portB == portA) portB = FreePort();
        var secret = RandomNumberGenerator.GetBytes(32); var idA = Guid.NewGuid(); var idB = Guid.NewGuid();
        var credentialA = new Secret(secret); var credentialB = new Secret(secret);
        var settingsA = Settings(idA, "A", idB, portB, credentialA); var settingsB = Settings(idB, "B", idA, portA, credentialB);
        await using var a = new TailscaleTcpTransport(() => settingsA, credentialA, IPAddress.Loopback, portA, credentialA);
        await using var b = new TailscaleTcpTransport(() => settingsB, credentialB, IPAddress.Loopback, portB, credentialB);
        var bytes = System.Text.Encoding.UTF8.GetBytes("authorized-package");
        var file = new GameTransferFile("game.exe", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), DateTimeOffset.UtcNow);
        var manifest = new GameTransferManifest(Guid.NewGuid(), new GameId(Guid.NewGuid()), "Package", idB, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), [file], bytes.Length);
        b.AttachGameTransferEndpoint(new GameEndpoint(idA, manifest, bytes));
        await a.StartAsync(new Endpoint(idB), CancellationToken.None); await b.StartAsync(new Endpoint(idA), CancellationToken.None);

        var offers = await a.ListGameTransferOffersAsync(settingsA.EffectiveTrustedPeers[0], CancellationToken.None);
        var chunk = await a.PullGameTransferChunkAsync(settingsA.EffectiveTrustedPeers[0], manifest.OfferId, file.RelativePath, 2, 5, CancellationToken.None);

        Assert.Equal(manifest.OfferId, Assert.Single(offers).OfferId);
        Assert.True(chunk.Success, chunk.Error);
        Assert.Equal(bytes.AsSpan(2, 5).ToArray(), chunk.Bytes);
    }

    private static SaveSnapshotPayload Payload()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("secret-save");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var manifest = new SaveSnapshotManifest(Guid.NewGuid(), null, new GameId(Guid.NewGuid()), Guid.NewGuid(), DateTimeOffset.UtcNow, [new("slot.sav", bytes.Length, hash)], []);
        return new(manifest, new Dictionary<string, byte[]> { ["slot.sav"] = bytes });
    }

    private static SaveSyncSettings Settings(Guid localId, string name, Guid peerId, int port, Secret credential) =>
        new(localId, name, "127.0.0.1", peerId, port, [], TrustedPeers:
            [new(peerId, $"Peer {name}", "127.0.0.1", credential.GetCredentialReference(peerId), TrustedPeerState.Active, DateTimeOffset.UtcNow)]);

    private static int FreePort() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
    private sealed class Secret(byte[] value) : IPairingSecretStore, IPeerCredentialStore
    {
        private readonly Dictionary<Guid, byte[]> _pending = [];
        public bool HasSecret => value.Length == 32;
        public byte[]? GetSecret() => value.ToArray();
        public void SetSecret(ReadOnlySpan<byte> secret) => value = secret.ToArray();
        public void Clear() => value = [];
        public bool ContainsSecret(Guid peerDeviceId) => value.Length == 32;
        public byte[]? GetSecret(Guid peerDeviceId) => GetSecret();
        public void SetSecret(Guid peerDeviceId, ReadOnlySpan<byte> secret) => SetSecret(secret);
        public void Clear(Guid peerDeviceId) => Clear();
        public string GetCredentialReference(Guid peerDeviceId) => $"test/{peerDeviceId:N}";
        public bool ContainsPendingSecret(Guid peerDeviceId) => _pending.ContainsKey(peerDeviceId);
        public byte[]? GetPendingSecret(Guid peerDeviceId) => _pending.GetValueOrDefault(peerDeviceId)?.ToArray();
        public void SetPendingSecret(Guid peerDeviceId, ReadOnlySpan<byte> secret) => _pending[peerDeviceId] = secret.ToArray();
        public void PromotePendingSecret(Guid peerDeviceId) { value = _pending[peerDeviceId]; _pending.Remove(peerDeviceId); }
        public void ClearPendingSecret(Guid peerDeviceId) => _pending.Remove(peerDeviceId);
    }
    private sealed class Endpoint(Guid expected) : ISaveSyncPeerEndpoint
    {
        private SaveSnapshotManifest? _manifest;
        private readonly Dictionary<string, List<byte>> _incoming = new(StringComparer.OrdinalIgnoreCase);
        public SaveSnapshotPayload? Received { get; private set; }
        public SaveSnapshotPayload? Outgoing { get; init; }
        public Secret? RotationCredentials { get; init; }
        public Func<string, Guid, PairingRedemptionResult>? Redeemer { get; init; }
        public Task<bool> AuthorizePeerAsync(Guid deviceId, CancellationToken token) => Task.FromResult(deviceId == expected);
        public Task<PairingRedemptionResult> RedeemInvitationAsync(string code, Guid deviceId, CancellationToken token) => Task.FromResult(Redeemer?.Invoke(code, deviceId) ?? new PairingRedemptionResult(false, null, null, "Not configured."));
        public Task<TransportResult> ReceivePushAsync(SaveSnapshotPayload snapshot, CancellationToken token) { Received = snapshot; return Task.FromResult(new TransportResult(true, false, null, null)); }
        public Task<TransportResult> ServePullAsync(GameId gameId, Guid? knownHead, CancellationToken token) => Task.FromResult(new TransportResult(true, false, null, null));
        public Task<SavePushBeginResult> BeginIncomingSnapshotAsync(Guid requestingDeviceId, SaveSnapshotManifest manifest, IReadOnlyList<string> changedPaths, CancellationToken token)
        {
            _manifest = manifest;
            foreach (var path in changedPaths) _incoming.TryAdd(path, []);
            return Task.FromResult(new SavePushBeginResult(true, changedPaths.ToDictionary(path => path, path => (long)_incoming[path].Count, StringComparer.OrdinalIgnoreCase), null));
        }
        public Task<SavePushChunkResult> ReceiveIncomingSnapshotChunkAsync(Guid requestingDeviceId, Guid snapshotId, string relativePath, long offset, byte[] bytes, CancellationToken token)
        {
            var target = _incoming[relativePath];
            if (target.Count != offset) return Task.FromResult(new SavePushChunkResult(false, target.Count, "Offset mismatch"));
            target.AddRange(bytes);
            return Task.FromResult(new SavePushChunkResult(true, target.Count, null));
        }
        public Task<TransportResult> CompleteIncomingSnapshotAsync(Guid requestingDeviceId, Guid snapshotId, CancellationToken token)
        {
            Received = new(_manifest!, _incoming.ToDictionary(static item => item.Key, static item => item.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
            return Task.FromResult(new TransportResult(true, false, null, null));
        }
        public Task<SavePullBeginResult> BeginOutgoingSnapshotAsync(Guid requestingDeviceId, GameId gameId, Guid? knownHead, CancellationToken token) =>
            Task.FromResult(Outgoing is null || Outgoing.Manifest.SnapshotId == knownHead
                ? new SavePullBeginResult(true, null, null)
                : new SavePullBeginResult(true, Outgoing.Manifest, null));
        public Task<SavePullChunkResult> ReadOutgoingSnapshotChunkAsync(Guid requestingDeviceId, Guid snapshotId, string relativePath, long offset, int maximumBytes, CancellationToken token)
        {
            if (Outgoing is null || Outgoing.Manifest.SnapshotId != snapshotId || !Outgoing.ChangedFiles.TryGetValue(relativePath, out var content))
                return Task.FromResult(new SavePullChunkResult(false, null, false, "Missing"));
            var count = Math.Min(maximumBytes, content.Length - (int)offset);
            return Task.FromResult(new SavePullChunkResult(true, content.AsSpan((int)offset, count).ToArray(), offset + count == content.Length, null));
        }
        public Task<string?> PrepareCredentialRotationAsync(Guid requestingDeviceId, byte[] newSecret, CancellationToken token)
        { RotationCredentials?.SetPendingSecret(requestingDeviceId, newSecret); return Task.FromResult<string?>(null); }
        public Task<string?> CommitCredentialRotationAsync(Guid requestingDeviceId, CancellationToken token)
        { RotationCredentials?.PromotePendingSecret(requestingDeviceId); return Task.FromResult<string?>(null); }
        public Task<string?> CancelIncomingPartialTransfersAsync(Guid requestingDeviceId, CancellationToken token) => Task.FromResult<string?>(null);
    }

    private sealed class GameEndpoint(Guid expected, GameTransferManifest manifest, byte[] bytes) : IPeerGameTransferEndpoint
    {
        public Task<IReadOnlyList<GameTransferManifest>> ListAuthorizedGameTransfersAsync(Guid requestingDeviceId, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<GameTransferManifest>>(requestingDeviceId == expected ? [manifest] : []);
        public Task<GameTransferChunkResult> ServeGameTransferChunkAsync(Guid requestingDeviceId, Guid offerId, string path, long offset, int maximum, CancellationToken token)
        {
            if (requestingDeviceId != expected || offerId != manifest.OfferId) return Task.FromResult(new GameTransferChunkResult(false, null, false, "Unauthorized"));
            var count = Math.Min(maximum, bytes.Length - (int)offset);
            return Task.FromResult(new GameTransferChunkResult(true, bytes.AsSpan((int)offset, count).ToArray(), offset + count == bytes.Length, null));
        }
    }
}
