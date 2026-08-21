using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using NovaLauncher.Application.SaveSync;
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

        var result = await a.PushAsync(payload, CancellationToken.None);

        Assert.True(result.Success, $"{result.Error}; server={b.LastServerError}");
        Assert.Equal(payload.Manifest.SnapshotId, endpointB.Received!.Manifest.SnapshotId);
        Assert.Equal("secret-save", System.Text.Encoding.UTF8.GetString(endpointB.Received.ChangedFiles["slot.sav"]));
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
        public bool HasSecret => value.Length == 32;
        public byte[]? GetSecret() => value.ToArray();
        public void SetSecret(ReadOnlySpan<byte> secret) => value = secret.ToArray();
        public void Clear() => value = [];
        public bool ContainsSecret(Guid peerDeviceId) => value.Length == 32;
        public byte[]? GetSecret(Guid peerDeviceId) => GetSecret();
        public void SetSecret(Guid peerDeviceId, ReadOnlySpan<byte> secret) => SetSecret(secret);
        public void Clear(Guid peerDeviceId) => Clear();
        public string GetCredentialReference(Guid peerDeviceId) => $"test/{peerDeviceId:N}";
    }
    private sealed class Endpoint(Guid expected) : ISaveSyncPeerEndpoint
    {
        public SaveSnapshotPayload? Received { get; private set; }
        public Func<string, Guid, PairingRedemptionResult>? Redeemer { get; init; }
        public Task<bool> AuthorizePeerAsync(Guid deviceId, CancellationToken token) => Task.FromResult(deviceId == expected);
        public Task<PairingRedemptionResult> RedeemInvitationAsync(string code, Guid deviceId, CancellationToken token) => Task.FromResult(Redeemer?.Invoke(code, deviceId) ?? new PairingRedemptionResult(false, null, null, "Not configured."));
        public Task<TransportResult> ReceivePushAsync(SaveSnapshotPayload snapshot, CancellationToken token) { Received = snapshot; return Task.FromResult(new TransportResult(true, false, null, null)); }
        public Task<TransportResult> ServePullAsync(GameId gameId, Guid? knownHead, CancellationToken token) => Task.FromResult(new TransportResult(true, false, null, null));
    }
}
