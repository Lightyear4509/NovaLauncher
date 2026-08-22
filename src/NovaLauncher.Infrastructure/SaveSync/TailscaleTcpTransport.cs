using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Collections.Concurrent;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Application.GameTransfer;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;

namespace NovaLauncher.Infrastructure.SaveSync;

public sealed class TailscaleTcpTransport(
    Func<SaveSyncSettings> settings,
    IPairingSecretStore secrets,
    IPAddress? localAddressOverride = null,
    int? localPortOverride = null,
    IPeerCredentialStore? peerCredentials = null,
    string? resumeRoot = null) : ISaveSyncTransport, IPeerGameTransferTransport, IDisposable
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
    private IPeerGameTransferEndpoint? _gameTransferEndpoint;
    private int _disposed;
    private readonly ConcurrentDictionary<Guid, long> _seenRequests = new();
    private readonly string _resumeRoot = resumeRoot ?? Path.Combine(Path.GetTempPath(), "NovaLauncher", "SaveSyncResume", settings().DeviceId.ToString("N"));
    public event Action<SaveTransferProgress>? ProgressChanged;

    public string? LastServerError { get; private set; }

    public bool IsConfigured => settings().EffectiveTrustedPeers.Any(peer =>
        peer.State == TrustedPeerState.Active && (peerCredentials?.ContainsSecret(peer.DeviceId) == true ||
        (settings().PeerDeviceId == peer.DeviceId && secrets.HasSecret)));
    public bool IsListening => _listener is not null;
    public string ListenerStatus { get; private set; } = "Listener has not been started.";

    public void AttachGameTransferEndpoint(IPeerGameTransferEndpoint endpoint) => _gameTransferEndpoint = endpoint;

    public async Task<IReadOnlyList<GameTransferManifest>> ListGameTransferOffersAsync(TrustedSaveSyncPeer peer, CancellationToken cancellationToken)
    {
        var response = await SendWireAsync(peer, new WireRequest(Guid.NewGuid(), "ListGameTransfers", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), default, null, null, null, null, 0, 0), cancellationToken).ConfigureAwait(false);
        if (response.Result is { Success: false } failure)
            throw new InvalidOperationException(failure.Error ?? "The peer rejected the offer refresh request.");
        if (response.GameTransferOffers is null)
            throw new InvalidOperationException("The peer returned no game-transfer offer response.");
        return response.GameTransferOffers ?? [];
    }

    public async Task<GameTransferChunkResult> PullGameTransferChunkAsync(TrustedSaveSyncPeer peer, Guid offerId, string relativePath, long offset, int maximumBytes, CancellationToken cancellationToken)
    {
        var response = await SendWireAsync(peer, new WireRequest(Guid.NewGuid(), "PullGameTransferChunk", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), default, null, null, offerId, relativePath, offset, maximumBytes), cancellationToken).ConfigureAwait(false);
        return response.GameTransferChunk ?? new(false, null, false, response.Result?.Error ?? "The peer returned no transfer chunk.");
    }

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
        PullChunkedAsync(null, gameId, knownHead, cancellationToken);

    public Task<TransportResult> PushAsync(SaveSnapshotPayload snapshot, CancellationToken cancellationToken) =>
        PushChunkedAsync(null, snapshot, cancellationToken);

    public Task<TransportResult> PullAsync(TrustedSaveSyncPeer peer, GameId gameId, Guid? knownHead, CancellationToken cancellationToken) =>
        PullChunkedAsync(peer, gameId, knownHead, cancellationToken);

    public Task<TransportResult> PushAsync(TrustedSaveSyncPeer peer, SaveSnapshotPayload snapshot, CancellationToken cancellationToken) =>
        PushChunkedAsync(peer, snapshot, cancellationToken);

    private async Task<TransportResult> PushChunkedAsync(TrustedSaveSyncPeer? selectedPeer, SaveSnapshotPayload snapshot, CancellationToken cancellationToken)
    {
        var peer = selectedPeer ?? settings().EffectiveTrustedPeers.FirstOrDefault(candidate => candidate.DeviceId == settings().PeerDeviceId)
            ?? settings().EffectiveTrustedPeers.FirstOrDefault(candidate => candidate.State == TrustedPeerState.Active);
        if (peer is null) return new(false, false, null, "The Tailscale peer is not configured or paired.");
        var changedPaths = snapshot.ChangedFiles.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var totalBytes = snapshot.ChangedFiles.Values.Sum(static content => content.LongLength);
        long transferred = 0;
        var started = System.Diagnostics.Stopwatch.StartNew();
        ProgressChanged?.Invoke(new(peer.DisplayName, "Upload", changedPaths.FirstOrDefault() ?? "Manifest", 0, totalBytes, 0, null, "Preparing authenticated upload"));
        var begin = await SendWireAsync(peer, new WireRequest(
            Guid.NewGuid(), "BeginSavePush", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), snapshot.Manifest.GameId,
            null, null, null, null, 0, 0, snapshot.Manifest, changedPaths), cancellationToken).ConfigureAwait(false);
        if (begin.SavePushBegin is not { Success: true } accepted)
            return new(false, false, null, begin.SavePushBegin?.Error ?? begin.Result?.Error ?? "The peer refused the save snapshot manifest.");
        foreach (var path in changedPaths)
        {
            var content = snapshot.ChangedFiles[path];
            var offset = accepted.ResumeOffsets.GetValueOrDefault(path);
            if (offset < 0 || offset > content.LongLength) return new(false, false, null, $"The peer returned an invalid resume offset for {path}.");
            transferred += offset;
            while (offset < content.LongLength)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = (int)Math.Min(SaveSyncCoordinator.SaveTransferChunkBytes, content.LongLength - offset);
                var bytes = content.AsSpan((int)offset, count).ToArray();
                var chunk = await SendWireAsync(peer, new WireRequest(
                    Guid.NewGuid(), "PushSaveChunk", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), snapshot.Manifest.GameId,
                    null, null, null, path, offset, count, null, null, snapshot.Manifest.SnapshotId, bytes), cancellationToken).ConfigureAwait(false);
                if (chunk.SavePushChunk is not { Success: true } acknowledged)
                {
                    var error = chunk.SavePushChunk?.Error ?? chunk.Result?.Error ?? $"The peer did not acknowledge save data for {path}.";
                    ProgressChanged?.Invoke(CreateProgress(peer.DisplayName, path, transferred, totalBytes, started, $"Paused for retry: {error}"));
                    return new(false, false, null, error);
                }
                if (acknowledged.NextOffset <= offset || acknowledged.NextOffset > content.LongLength)
                    return new(false, false, null, $"The peer returned an invalid next offset for {path}.");
                transferred += acknowledged.NextOffset - offset;
                offset = acknowledged.NextOffset;
                ProgressChanged?.Invoke(CreateProgress(peer.DisplayName, path, transferred, totalBytes, started, "Uploading"));
            }
        }
        var complete = await SendWireAsync(peer, new WireRequest(
            Guid.NewGuid(), "CompleteSavePush", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), snapshot.Manifest.GameId,
            null, null, null, null, 0, 0, null, null, snapshot.Manifest.SnapshotId), cancellationToken).ConfigureAwait(false);
        var result = complete.Result ?? new(false, false, null, "The peer returned no save-sync completion result.");
        ProgressChanged?.Invoke(CreateProgress(peer.DisplayName, changedPaths.LastOrDefault() ?? "Manifest", transferred, totalBytes, started, result.Success ? "Completed and verified" : $"Paused for retry: {result.Error}"));
        return result;
    }

    private static SaveTransferProgress CreateProgress(string deviceName, string path, long transferred, long total, System.Diagnostics.Stopwatch started, string status)
    {
        var speed = started.Elapsed.TotalSeconds <= 0 ? 0 : transferred / started.Elapsed.TotalSeconds;
        TimeSpan? remaining = speed <= 0 || transferred >= total ? null : TimeSpan.FromSeconds((total - transferred) / speed);
        return new(deviceName, "Upload", path, transferred, total, speed, remaining, status);
    }

    private async Task<TransportResult> PullChunkedAsync(TrustedSaveSyncPeer? selectedPeer, GameId gameId, Guid? knownHead, CancellationToken cancellationToken)
    {
        var peer = selectedPeer ?? settings().EffectiveTrustedPeers.FirstOrDefault(candidate => candidate.DeviceId == settings().PeerDeviceId)
            ?? settings().EffectiveTrustedPeers.FirstOrDefault(candidate => candidate.State == TrustedPeerState.Active);
        if (peer is null) return new(false, false, null, "The Tailscale peer is not configured or paired.");
        var begin = await SendWireAsync(peer, new WireRequest(
            Guid.NewGuid(), "BeginSavePull", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), gameId,
            knownHead, null, null, null, 0, 0), cancellationToken).ConfigureAwait(false);
        if (begin.SavePullBegin is not { Success: true } accepted)
            return new(false, false, null, begin.SavePullBegin?.Error ?? begin.Result?.Error ?? "The peer refused the save download request.");
        if (accepted.Manifest is null) return new(true, false, null, null);
        var manifest = accepted.Manifest;
        var totalBytes = manifest.Files.Sum(static file => file.Length);
        long transferred = 0;
        var started = System.Diagnostics.Stopwatch.StartNew();
        var stage = Path.Combine(_resumeRoot, peer.DeviceId.ToString("N"), manifest.SnapshotId.ToString("N"));
        Directory.CreateDirectory(stage);
        ProgressChanged?.Invoke(new(peer.DisplayName, "Download", manifest.Files.Count == 0 ? "Manifest" : manifest.Files[0].RelativePath, 0, totalBytes, 0, null, "Preparing authenticated download"));
        foreach (var entry in manifest.Files)
        {
            var target = SafeResumeCombine(stage, entry.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var offset = File.Exists(target) ? new FileInfo(target).Length : 0;
            if (offset > entry.Length) return new(false, false, null, $"The durable resume file for {entry.RelativePath} exceeds its manifest length.");
            transferred += offset;
            while (offset < entry.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var maximum = (int)Math.Min(SaveSyncCoordinator.SaveTransferChunkBytes, entry.Length - offset);
                var response = await SendWireAsync(peer, new WireRequest(
                    Guid.NewGuid(), "PullSaveChunk", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), gameId,
                    null, null, null, entry.RelativePath, offset, maximum, null, null, manifest.SnapshotId), cancellationToken).ConfigureAwait(false);
                if (response.SavePullChunk is not { Success: true, Bytes: not null } chunk)
                {
                    var error = response.SavePullChunk?.Error ?? response.Result?.Error ?? $"The peer did not return save data for {entry.RelativePath}.";
                    ProgressChanged?.Invoke(CreateDownloadProgress(peer.DisplayName, entry.RelativePath, transferred, totalBytes, started, $"Paused for retry: {error}"));
                    return new(false, false, null, error);
                }
                if (chunk.Bytes.Length == 0 || offset + chunk.Bytes.LongLength > entry.Length)
                    return new(false, false, null, $"The peer returned an invalid save chunk for {entry.RelativePath}.");
                await using (var stream = new FileStream(target, FileMode.Append, FileAccess.Write, FileShare.None, SaveSyncCoordinator.SaveTransferChunkBytes, true))
                {
                    await stream.WriteAsync(chunk.Bytes, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                offset += chunk.Bytes.LongLength;
                transferred += chunk.Bytes.LongLength;
                if (chunk.EndOfFile != (offset == entry.Length)) return new(false, false, null, $"The peer returned an inconsistent end marker for {entry.RelativePath}.");
                ProgressChanged?.Invoke(CreateDownloadProgress(peer.DisplayName, entry.RelativePath, transferred, totalBytes, started, "Downloading"));
            }
            await using var verify = File.OpenRead(target);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(verify, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
            if (!string.Equals(hash, entry.Sha256, StringComparison.Ordinal)) return new(false, false, null, $"The downloaded save file {entry.RelativePath} failed SHA-256 verification.");
        }
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Files) files[entry.RelativePath] = await File.ReadAllBytesAsync(SafeResumeCombine(stage, entry.RelativePath), cancellationToken).ConfigureAwait(false);
        if (Directory.Exists(stage)) Directory.Delete(stage, true);
        ProgressChanged?.Invoke(CreateDownloadProgress(peer.DisplayName, manifest.Files.Count == 0 ? "Manifest" : manifest.Files[^1].RelativePath, transferred, totalBytes, started, "Completed and verified"));
        return new(true, false, new(manifest, files), null);
    }

    private static SaveTransferProgress CreateDownloadProgress(string deviceName, string path, long transferred, long total, System.Diagnostics.Stopwatch started, string status)
    {
        var speed = started.Elapsed.TotalSeconds <= 0 ? 0 : transferred / started.Elapsed.TotalSeconds;
        TimeSpan? remaining = speed <= 0 || transferred >= total ? null : TimeSpan.FromSeconds((total - transferred) / speed);
        return new(deviceName, "Download", path, transferred, total, speed, remaining, status);
    }

    private static string SafeResumeCombine(string root, string relativePath)
    {
        if (!SaveSyncCoordinator.IsSafeRelativePath(relativePath)) throw new InvalidDataException("The save resume path is unsafe.");
        var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var combined = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!combined.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The save resume path escaped its staging root.");
        return combined;
    }

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

    public async Task<string?> RotatePeerCredentialAsync(TrustedSaveSyncPeer peer, byte[] newSecret, CancellationToken cancellationToken)
    {
        if (newSecret.Length != 32) return "The replacement credential is invalid.";
        var prepare = await SendWireAsync(peer, new WireRequest(
            Guid.NewGuid(), "PrepareCredentialRotation", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), default,
            null, null, null, null, 0, 0, RotationSecret: newSecret), cancellationToken).ConfigureAwait(false);
        if (prepare.Result is not { Success: true }) return prepare.Result?.Error ?? "The trusted device did not stage the replacement credential.";
        var commit = await SendWireAsync(peer, new WireRequest(
            Guid.NewGuid(), "CommitCredentialRotation", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), default,
            null, null, null, null, 0, 0), cancellationToken).ConfigureAwait(false);
        return commit.Result is { Success: true } ? null : commit.Result?.Error ?? "The trusted device did not commit the replacement credential.";
    }

    public async Task<string?> CancelPartialTransfersAsync(CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        foreach (var peer in settings().EffectiveTrustedPeers.Where(static peer => peer.State == TrustedPeerState.Active))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await SendWireAsync(peer, new WireRequest(
                Guid.NewGuid(), "CancelPartialSaveTransfers", settings().DeviceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), default,
                null, null, null, null, 0, 0), cancellationToken).ConfigureAwait(false);
            if (response.Result is not { Success: true }) failures.Add($"{peer.DisplayName}: {response.Result?.Error ?? "unavailable"}");
        }
        try { if (Directory.Exists(_resumeRoot)) Directory.Delete(_resumeRoot, true); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { failures.Add($"Local partial downloads: {exception.Message}"); }
        return failures.Count == 0 ? null : $"Some partial transfer data could not be cancelled ({string.Join("; ", failures)}).";
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
        var response = await SendWireAsync(peer, request, cancellationToken).ConfigureAwait(false);
        return response.Result ?? new(false, false, null, "The peer returned no save-sync result.");
    }

    private async Task<WireResponse> SendWireAsync(TrustedSaveSyncPeer peer, WireRequest request, CancellationToken cancellationToken)
    {
        if (peer.State != TrustedPeerState.Active) return new(settings().DeviceId, new(false, false, null, "The selected peer is not active."), null, null);
        var candidates = new List<(byte[] Key, bool Pending)>();
        var active = GetPeerSecret(peer.DeviceId);
        candidates.Add((active, false));
        var pending = peerCredentials?.GetPendingSecret(peer.DeviceId);
        if (pending is { Length: 32 } && !CryptographicOperations.FixedTimeEquals(active, pending)) candidates.Add((pending, true));
        else if (pending is not null) CryptographicOperations.ZeroMemory(pending);
        Exception? lastError = null;
        foreach (var candidate in candidates)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                var peerAddress = IPAddress.Parse(peer.Address);
                using var client = new TcpClient(peerAddress.AddressFamily);
                await client.ConnectAsync(peerAddress, settings().Port, timeout.Token).ConfigureAwait(false);
                await using var stream = client.GetStream();
                await WriteEncryptedAsync(stream, request, settings().DeviceId, candidate.Key, timeout.Token).ConfigureAwait(false);
                var response = await ReadEncryptedAsync<WireResponse>(stream, peer.DeviceId, candidate.Key, timeout.Token).ConfigureAwait(false);
                if (_endpoint is null || !await _endpoint.AuthorizePeerAsync(response.DeviceId, timeout.Token).ConfigureAwait(false))
                {
                    ZeroOtherCandidateKeys(candidates, candidate.Key);
                    return new(response.DeviceId, new(false, false, null, "The responding device identity is not paired."), null, null);
                }
                if (candidate.Pending) peerCredentials!.PromotePendingSecret(peer.DeviceId);
                ZeroOtherCandidateKeys(candidates, candidate.Key);
                return response;
            }
            catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException or CryptographicException or JsonException)
            {
                if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested) throw;
                lastError = exception;
            }
            finally { CryptographicOperations.ZeroMemory(candidate.Key); }
        }
        return new(peer.DeviceId, new(false, false, null, $"Peer transfer failed: {lastError?.Message ?? "no credential succeeded"}"), null, null);
    }

    private static void ZeroOtherCandidateKeys(IEnumerable<(byte[] Key, bool Pending)> candidates, byte[] current)
    {
        foreach (var candidate in candidates)
            if (!ReferenceEquals(candidate.Key, current)) CryptographicOperations.ZeroMemory(candidate.Key);
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
                    TransportResult? result = null;
                    IReadOnlyList<GameTransferManifest>? transferOffers = null;
                    GameTransferChunkResult? transferChunk = null;
                    SavePushBeginResult? savePushBegin = null;
                    SavePushChunkResult? savePushChunk = null;
                    SavePullBeginResult? savePullBegin = null;
                    SavePullChunkResult? savePullChunk = null;
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
                    else if (request.Operation == "BeginSavePush" && request.SaveManifest is not null && request.SaveChangedPaths is not null)
                        savePushBegin = await _endpoint.BeginIncomingSnapshotAsync(request.DeviceId, request.SaveManifest, request.SaveChangedPaths, cancellationToken).ConfigureAwait(false);
                    else if (request.Operation == "PushSaveChunk" && request.SaveSnapshotId is { } saveSnapshotId && request.TransferRelativePath is not null && request.SaveChunk is not null)
                        savePushChunk = await _endpoint.ReceiveIncomingSnapshotChunkAsync(request.DeviceId, saveSnapshotId, request.TransferRelativePath, request.TransferOffset, request.SaveChunk, cancellationToken).ConfigureAwait(false);
                    else if (request.Operation == "CompleteSavePush" && request.SaveSnapshotId is { } completedSnapshotId)
                        result = await _endpoint.CompleteIncomingSnapshotAsync(request.DeviceId, completedSnapshotId, cancellationToken).ConfigureAwait(false);
                    else if (request.Operation == "BeginSavePull")
                        savePullBegin = await _endpoint.BeginOutgoingSnapshotAsync(request.DeviceId, request.GameId, request.KnownHead, cancellationToken).ConfigureAwait(false);
                    else if (request.Operation == "PullSaveChunk" && request.SaveSnapshotId is { } outgoingSnapshotId && request.TransferRelativePath is not null)
                        savePullChunk = await _endpoint.ReadOutgoingSnapshotChunkAsync(request.DeviceId, outgoingSnapshotId, request.TransferRelativePath, request.TransferOffset, request.TransferMaximumBytes, cancellationToken).ConfigureAwait(false);
                    else if (request.Operation == "PrepareCredentialRotation" && request.RotationSecret is not null)
                    {
                        var error = await _endpoint.PrepareCredentialRotationAsync(request.DeviceId, request.RotationSecret, cancellationToken).ConfigureAwait(false);
                        result = new(error is null, false, null, error);
                    }
                    else if (request.Operation == "CommitCredentialRotation")
                    {
                        var error = await _endpoint.CommitCredentialRotationAsync(request.DeviceId, cancellationToken).ConfigureAwait(false);
                        result = new(error is null, false, null, error);
                    }
                    else if (request.Operation == "CancelPartialSaveTransfers")
                    {
                        var error = await _endpoint.CancelIncomingPartialTransfersAsync(request.DeviceId, cancellationToken).ConfigureAwait(false);
                        result = new(error is null, false, null, error);
                    }
                    else if (request.Operation == "ListGameTransfers" && _gameTransferEndpoint is not null)
                        transferOffers = await _gameTransferEndpoint.ListAuthorizedGameTransfersAsync(request.DeviceId, cancellationToken).ConfigureAwait(false);
                    else if (request.Operation == "PullGameTransferChunk" && _gameTransferEndpoint is not null && request.TransferOfferId is { } offerId && request.TransferRelativePath is not null)
                        transferChunk = await _gameTransferEndpoint.ServeGameTransferChunkAsync(request.DeviceId, offerId, request.TransferRelativePath, request.TransferOffset, request.TransferMaximumBytes, cancellationToken).ConfigureAwait(false);
                    else result = new(false, false, null, "Unknown peer operation.");
                    await WriteEncryptedAsync(stream, new WireResponse(settings().DeviceId, result, transferOffers, transferChunk, savePushBegin, savePushChunk, savePullBegin, savePullChunk), settings().DeviceId, requestKey, cancellationToken).ConfigureAwait(false);
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

    private sealed record WireRequest(Guid RequestId, string Operation, Guid DeviceId, long Timestamp, GameId GameId, Guid? KnownHead, SaveSnapshotPayload? Snapshot, Guid? TransferOfferId, string? TransferRelativePath, long TransferOffset, int TransferMaximumBytes, SaveSnapshotManifest? SaveManifest = null, IReadOnlyList<string>? SaveChangedPaths = null, Guid? SaveSnapshotId = null, byte[]? SaveChunk = null, byte[]? RotationSecret = null);
    private sealed record WireResponse(Guid DeviceId, TransportResult? Result, IReadOnlyList<GameTransferManifest>? GameTransferOffers, GameTransferChunkResult? GameTransferChunk, SavePushBeginResult? SavePushBegin = null, SavePushChunkResult? SavePushChunk = null, SavePullBeginResult? SavePullBegin = null, SavePullChunkResult? SavePullChunk = null);
    private sealed record BootstrapRequest(string Code, Guid DeviceId);
}
