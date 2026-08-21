using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Collections.Concurrent;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;

namespace NovaLauncher.Infrastructure.SaveSync;

public sealed class TailscaleTcpTransport(
    Func<SaveSyncSettings> settings,
    IPairingSecretStore secrets,
    IPAddress? localAddressOverride = null,
    int? localPortOverride = null,
    IPeerCredentialStore? peerCredentials = null) : ISaveSyncTransport, IDisposable
{
    private const int MaximumFrameBytes = 520 * 1024 * 1024;
    private const int BootstrapMarker = -2;
    private const int MaximumBootstrapBytes = 4096;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _connections = new(4, 4);
    private TcpListener? _listener;
    private Task? _acceptLoop;
    private ISaveSyncPeerEndpoint? _endpoint;
    private int _disposed;
    private readonly ConcurrentDictionary<Guid, long> _seenRequests = new();

    public string? LastServerError { get; private set; }

    public bool IsConfigured => settings().EffectiveTrustedPeers.Any(peer =>
        peer.State == TrustedPeerState.Active && (peerCredentials?.ContainsSecret(peer.DeviceId) == true ||
        (settings().PeerDeviceId == peer.DeviceId && secrets.HasSecret)));
    public bool IsListening => _listener is not null;
    public string ListenerStatus { get; private set; } = "Listener has not been started.";

    public async Task StartAsync(ISaveSyncPeerEndpoint endpoint, CancellationToken cancellationToken)
    {
        _endpoint = endpoint;
        if (_listener is not null) return;
        await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_listener is not null) return;
            var local = localAddressOverride ?? FindLocalTailscaleAddress();
            if (local is null)
            {
                ListenerStatus = "Inactive: no active local Tailscale IP was detected. Confirm the Tailscale client says Connected, then restart NovaLauncher.";
                throw new InvalidOperationException(ListenerStatus);
            }
            try
            {
                _listener = new TcpListener(local, localPortOverride ?? settings().Port);
                _listener.Start(8);
                var displayAddress = local.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{local}]" : local.ToString();
                ListenerStatus = $"Listening on {displayAddress}:{localPortOverride ?? settings().Port}.";
                _acceptLoop = AcceptLoopAsync(_lifetime.Token);
            }
            catch (SocketException exception)
            {
                _listener = null;
                ListenerStatus = $"Inactive: Windows could not open {local}:{localPortOverride ?? settings().Port} ({exception.SocketErrorCode}).";
                throw new InvalidOperationException(ListenerStatus, exception);
            }
        }
        finally { _startGate.Release(); }
    }

    public Task<TransportResult> PullAsync(GameId gameId, Guid? knownHead, CancellationToken cancellationToken) =>
        SendAsync(new WireRequest(Guid.NewGuid(), "Pull", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), gameId, knownHead, null), cancellationToken);

    public Task<TransportResult> PushAsync(SaveSnapshotPayload snapshot, CancellationToken cancellationToken) =>
        SendAsync(new WireRequest(Guid.NewGuid(), "Push", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), snapshot.Manifest.GameId, null, snapshot), cancellationToken);

    public Task<TransportResult> PullAsync(TrustedSaveSyncPeer peer, GameId gameId, Guid? knownHead, CancellationToken cancellationToken) =>
        SendAsync(peer, new WireRequest(Guid.NewGuid(), "Pull", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), gameId, knownHead, null), cancellationToken);

    public Task<TransportResult> PushAsync(TrustedSaveSyncPeer peer, SaveSnapshotPayload snapshot, CancellationToken cancellationToken) =>
        SendAsync(peer, new WireRequest(Guid.NewGuid(), "Push", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), snapshot.Manifest.GameId, null, snapshot), cancellationToken);

    public async Task<PairingRedemptionResult> RedeemInvitationAsync(string code, Guid requestingDeviceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings().PeerAddress)) return new(false, null, null, "Enter and save the inviter's Tailscale IP first.");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var peer = IPAddress.Parse(settings().PeerAddress!);
            using var client = new TcpClient(peer.AddressFamily);
            await client.ConnectAsync(peer, settings().Port, timeout.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();
            await WriteBootstrapAsync(stream, new BootstrapRequest(code, requestingDeviceId), timeout.Token).ConfigureAwait(false);
            return await ReadBootstrapAsync<PairingRedemptionResult>(stream, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException or JsonException)
        { return new(false, null, null, $"Invitation redemption failed: {exception.Message}"); }
    }

    private async Task<TransportResult> SendAsync(WireRequest request, CancellationToken cancellationToken)
    {
        var peer = settings().EffectiveTrustedPeers.FirstOrDefault(candidate => candidate.DeviceId == settings().PeerDeviceId)
            ?? settings().EffectiveTrustedPeers.FirstOrDefault(candidate => candidate.State == TrustedPeerState.Active);
        if (peer is null) return new(false, false, null, "The Tailscale peer is not configured or paired.");
        return await SendAsync(peer, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TransportResult> SendAsync(TrustedSaveSyncPeer peer, WireRequest request, CancellationToken cancellationToken)
    {
        if (peer.State != TrustedPeerState.Active) return new(false, false, null, "The selected peer is not active.");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var peerAddress = IPAddress.Parse(peer.Address);
            using var client = new TcpClient(peerAddress.AddressFamily);
            await client.ConnectAsync(peerAddress, settings().Port, timeout.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();
            var key = GetPeerSecret(peer.DeviceId);
            await WriteEncryptedAsync(stream, request, settings().DeviceId, key, timeout.Token).ConfigureAwait(false);
            var response = await ReadEncryptedAsync<WireResponse>(stream, peer.DeviceId, key, timeout.Token).ConfigureAwait(false);
            if (_endpoint is null || !await _endpoint.AuthorizePeerAsync(response.DeviceId, timeout.Token).ConfigureAwait(false))
                return new(false, false, null, "The responding device identity is not paired.");
            return response.Result;
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException or CryptographicException or JsonException)
        { return new(false, false, null, $"Peer transfer failed: {exception.Message}"); }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                if (!await _connections.WaitAsync(0, cancellationToken).ConfigureAwait(false)) client.Dispose();
                else _ = HandleClientAsync(client, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { break; }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 30_000;
                    client.SendTimeout = 30_000;
                    await using var stream = client.GetStream();
                    var header = new byte[4];
                    await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
                    if (BinaryPrimitives.ReadInt32BigEndian(header) == BootstrapMarker)
                    {
                        var bootstrap = await ReadBootstrapBodyAsync<BootstrapRequest>(stream, cancellationToken).ConfigureAwait(false);
                        var redemption = _endpoint is null
                            ? new PairingRedemptionResult(false, null, null, "The invitation endpoint is unavailable.")
                            : await _endpoint.RedeemInvitationAsync(bootstrap.Code, bootstrap.DeviceId, cancellationToken).ConfigureAwait(false);
                        await WriteBootstrapAsync(stream, redemption, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                    var (request, _, requestKey) = await ReadIncomingRequestAsync(stream, header, cancellationToken).ConfigureAwait(false);
                    TransportResult result;
                    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    if (_seenRequests.Count > 4096)
                        foreach (var old in _seenRequests.Where(item => now - item.Value > 120).Select(static item => item.Key)) _seenRequests.TryRemove(old, out _);
                    if (request.RequestId == Guid.Empty || !_seenRequests.TryAdd(request.RequestId, now))
                        result = new(false, false, null, "A replayed peer request was rejected.");
                    else if (_endpoint is null || Math.Abs(now - request.Timestamp) > 120 ||
                        !await _endpoint.AuthorizePeerAsync(request.DeviceId, cancellationToken).ConfigureAwait(false))
                        result = new(false, false, null, "The request identity or timestamp was rejected.");
                    else if (request.Operation == "Pull")
                        result = await _endpoint.ServePullAsync(request.GameId, request.KnownHead, cancellationToken).ConfigureAwait(false);
                    else if (request.Operation == "Push" && request.Snapshot is not null)
                        result = await _endpoint.ReceivePushAsync(request.Snapshot, cancellationToken).ConfigureAwait(false);
                    else result = new(false, false, null, "Unknown peer operation.");
                    await WriteEncryptedAsync(stream, new WireResponse(settings().DeviceId, result), settings().DeviceId, requestKey, cancellationToken).ConfigureAwait(false);
                    CryptographicOperations.ZeroMemory(requestKey);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LastServerError = exception.Message;
                }
            }
        }
        finally { _connections.Release(); }
    }

    private static async Task WriteEncryptedAsync<T>(Stream stream, T value, Guid senderDeviceId, byte[] key, CancellationToken cancellationToken)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(value);
        if (plain.Length > MaximumFrameBytes) throw new InvalidDataException("The peer frame exceeds its size limit.");
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, plain, cipher, tag);
        var length = 16 + nonce.Length + tag.Length + cipher.Length;
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(senderDeviceId.ToByteArray(), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(nonce, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(tag, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(cipher, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> ReadEncryptedAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var peer = settings().EffectiveTrustedPeers.FirstOrDefault(candidate => candidate.DeviceId == settings().PeerDeviceId)
            ?? throw new CryptographicException("The paired device identity is unavailable.");
        var key = GetPeerSecret(peer.DeviceId);
        try { return await ReadEncryptedAsync<T>(stream, peer.DeviceId, key, cancellationToken).ConfigureAwait(false); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static async Task<T> ReadEncryptedAsync<T>(Stream stream, Guid expectedSender, byte[] key, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        return await ReadEncryptedAsync<T>(stream, header, expectedSender, key, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ReadEncryptedAsync<T>(Stream stream, byte[] header, Guid expectedSender, byte[] key, CancellationToken cancellationToken)
    {
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is < 45 or > MaximumFrameBytes) throw new InvalidDataException("The peer frame length is invalid.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        var sender = new Guid(payload.AsSpan(0, 16));
        if (sender != expectedSender) throw new CryptographicException("The encrypted frame identity does not match the selected peer.");
        var plain = new byte[length - 44];
        using (var aes = new AesGcm(key, 16)) aes.Decrypt(payload.AsSpan(16, 12), payload.AsSpan(44), payload.AsSpan(28, 16), plain);
        return JsonSerializer.Deserialize<T>(plain) ?? throw new JsonException("The peer message is empty.");
    }

    private async Task<(WireRequest Request, Guid DeviceId, byte[] Key)> ReadIncomingRequestAsync(Stream stream, byte[] header, CancellationToken cancellationToken)
    {
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is < 45 or > MaximumFrameBytes) throw new InvalidDataException("The peer frame length is invalid.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        var deviceId = new Guid(payload.AsSpan(0, 16));
        var key = GetPeerSecret(deviceId);
        try
        {
            var plain = new byte[length - 44];
            using (var aes = new AesGcm(key, 16)) aes.Decrypt(payload.AsSpan(16, 12), payload.AsSpan(44), payload.AsSpan(28, 16), plain);
            var request = JsonSerializer.Deserialize<WireRequest>(plain) ?? throw new JsonException("The peer request is empty.");
            if (request.DeviceId != deviceId) throw new CryptographicException("The authenticated request identity is inconsistent.");
            return (request, deviceId, key);
        }
        catch { CryptographicOperations.ZeroMemory(key); throw; }
    }

    private byte[] GetPeerSecret(Guid peerDeviceId)
    {
        var key = peerCredentials?.GetSecret(peerDeviceId);
        if (key is { Length: 32 }) return key;
        if (settings().PeerDeviceId == peerDeviceId)
        {
            key = secrets.GetSecret();
            if (key is { Length: 32 }) return key;
        }
        if (key is not null) CryptographicOperations.ZeroMemory(key);
        throw new CryptographicException("The selected peer credential is unavailable.");
    }

    private static async Task WriteBootstrapAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value);
        if (payload.Length is <= 0 or > MaximumBootstrapBytes) throw new InvalidDataException("The invitation message is too large.");
        var header = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), BootstrapMarker);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ReadBootstrapAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var marker = new byte[4];
        await stream.ReadExactlyAsync(marker, cancellationToken).ConfigureAwait(false);
        if (BinaryPrimitives.ReadInt32BigEndian(marker) != BootstrapMarker) throw new InvalidDataException("The invitation response marker is invalid.");
        return await ReadBootstrapBodyAsync<T>(stream, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ReadBootstrapBodyAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBytes = new byte[4];
        await stream.ReadExactlyAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
        if (length is <= 0 or > MaximumBootstrapBytes) throw new InvalidDataException("The invitation message length is invalid.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(payload) ?? throw new JsonException("The invitation message is empty.");
    }

    private static IPAddress? FindLocalTailscaleAddress() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(static adapter => adapter.OperationalStatus == OperationalStatus.Up)
        .SelectMany(static adapter => adapter.GetIPProperties().UnicastAddresses)
        .Select(static address => address.Address)
        .Where(address => TailscalePeerValidator.TryNormalize(address.ToString(), out _, out _))
        .OrderBy(static address => address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
        .FirstOrDefault();

    public async ValueTask DisposeAsync()
    {
        var loop = _acceptLoop;
        Dispose();
        if (loop is not null) try { await loop.ConfigureAwait(false); } catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        _listener?.Stop();
        _lifetime.Dispose();
        _startGate.Dispose();
    }

    private sealed record WireRequest(Guid RequestId, string Operation, Guid DeviceId, long Timestamp, GameId GameId, Guid? KnownHead, SaveSnapshotPayload? Snapshot);
    private sealed record WireResponse(Guid DeviceId, TransportResult Result);
    private sealed record BootstrapRequest(string Code, Guid DeviceId);
}
