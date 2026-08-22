using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;

namespace NovaLauncher.Application.GameTransfer;

public sealed class GameTransferCoordinator(IPeerGameTransferTransport transport, ISaveSyncService saveSync, TimeProvider timeProvider, string dataRoot, IReceivedContentScanner? contentScanner = null) : IGameTransferService, IDisposable
{
    public const int MaximumFiles = 50_000;
    public const long MaximumFileBytes = 700L * 1024 * 1024 * 1024;
    public const long MaximumPackageBytes = 700L * 1024 * 1024 * 1024;
    public const int ChunkBytes = 1024 * 1024;
    public const long MaximumBytesPerSecond = 64L * 1024 * 1024;
    public const int MaximumOffers = 16;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, AuthorizedGameTransfer> _offers = [];
    private readonly string _auditPath = Path.Combine(dataRoot, "GameTransfers", "audit.json");

    public void AttachEndpoint() => transport.AttachGameTransferEndpoint(this);

    public async Task<GameTransferPreview> PreviewAsync(LibraryItem game, string sourceFolder, CancellationToken cancellationToken)
    {
        if (!string.Equals(game.Source, "Manual", StringComparison.OrdinalIgnoreCase) || game.LaunchTarget.Kind != LaunchTargetKind.Executable)
            return Reject(game.Name, sourceFolder, "Only manually added direct-executable games can be transferred.");
        string root;
        try { root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceFolder)); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) { return Reject(game.Name, sourceFolder, exception.Message); }
        if (!Directory.Exists(root)) return Reject(game.Name, root, "The selected source folder does not exist.");
        if (IsManagedStoreRoot(root)) return Reject(game.Name, root, "Steam and other store-managed installation roots are excluded.");
        var executable = Path.GetFullPath(game.LaunchTarget.Target);
        if (!executable.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return Reject(game.Name, root, "The selected folder must contain the manual game executable.");
        var files = new List<GameTransferFile>();
        long total = 0;
        try
        {
            if ((new DirectoryInfo(root).Attributes & FileAttributes.ReparsePoint) != 0) return Reject(game.Name, root, "Reparse-point source folders are not allowed.");
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(root);
            while (pendingDirectories.Count > 0)
            {
                foreach (var directory in Directory.EnumerateDirectories(pendingDirectories.Pop()))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((new DirectoryInfo(directory).Attributes & FileAttributes.ReparsePoint) != 0)
                        return Reject(game.Name, root, "Reparse-point directories inside a transfer source are not allowed.");
                    pendingDirectories.Push(directory);
                }
            }
            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (files.Count >= MaximumFiles) return Reject(game.Name, root, $"The package exceeds the {MaximumFiles:N0}-file limit.");
                var info = new FileInfo(path);
                if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.SparseFile | FileAttributes.Device)) != 0) return Reject(game.Name, root, $"Unsafe file attributes were detected for {info.Name}.");
                var relative = NormalizeRelativePath(Path.GetRelativePath(root, path));
                if (!IsSafeRelativePath(relative) || !IsFileSizeWithinLimit(info.Length)) return Reject(game.Name, root, $"Unsafe or oversized package file: {relative}.");
                total = checked(total + info.Length);
                if (!IsPackageSizeWithinLimit(total)) return Reject(game.Name, root, "The package exceeds the 700 GiB aggregate size limit.");
                var beforeLength = info.Length;
                var beforeWrite = info.LastWriteTimeUtc;
                var hash = await HashFileAsync(path, cancellationToken).ConfigureAwait(false);
                info.Refresh();
                if (info.Length != beforeLength || info.LastWriteTimeUtc != beforeWrite) return Reject(game.Name, root, $"{relative} changed during scanning. Close the game and retry.");
                files.Add(new(relative, beforeLength, hash, beforeWrite));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OverflowException) { return Reject(game.Name, root, exception.Message); }
        if (files.Count == 0) return Reject(game.Name, root, "The selected folder is empty.");
        if (!files.Any(file => string.Equals(Path.GetFullPath(Path.Combine(root, file.RelativePath)), executable, StringComparison.OrdinalIgnoreCase))) return Reject(game.Name, root, "The game executable was not included in the manifest.");
        return new(true, game.Name, root, files.OrderBy(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(), [], total, null);
    }

    public async Task<GameTransferResult> AuthorizeAsync(LibraryItem game, GameTransferPreview preview, IReadOnlyCollection<Guid> recipientDeviceIds, bool userAttestedCopyRights, CancellationToken cancellationToken)
    {
        if (!userAttestedCopyRights) return new(false, false, "Confirm that you own or are authorized to copy this game folder.");
        if (!preview.Accepted || preview.Error is not null) return new(false, false, preview.Error ?? "Create a valid preview first.");
        var rescanned = await PreviewAsync(game, preview.SourceFolder, cancellationToken).ConfigureAwait(false);
        if (!rescanned.Accepted || !ManifestFilesEqual(preview.Files, rescanned.Files)) return new(false, false, rescanned.Error ?? "The source changed after preview; review it again.");
        var active = saveSync.Settings.EffectiveTrustedPeers.Where(static peer => peer.State == TrustedPeerState.Active).Select(static peer => peer.DeviceId).ToHashSet();
        var recipients = recipientDeviceIds.Distinct().ToHashSet();
        if (recipients.Count == 0 || recipients.Any(id => !active.Contains(id))) return new(false, false, "Choose at least one active trusted device.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var expired in _offers.Where(item => item.Value.Manifest.ExpiresAtUtc <= timeProvider.GetUtcNow()).Select(static item => item.Key).ToArray()) _offers.Remove(expired);
            if (_offers.Count >= MaximumOffers) return new(false, false, $"At most {MaximumOffers} active offers may be retained.");
            var id = Guid.NewGuid();
            var now = timeProvider.GetUtcNow();
            var manifest = new GameTransferManifest(id, game.Id, preview.PackageName, saveSync.Settings.DeviceId, now, now.AddHours(24), preview.Files, preview.TotalBytes);
            _offers[id] = new(manifest, preview.SourceFolder, recipients);
            await AppendAuditAsync(new(Guid.NewGuid(), id, Guid.Empty, preview.PackageName, preview.TotalBytes, preview.Files.Count, now, "Authorized"), cancellationToken).ConfigureAwait(false);
            return new(true, false, $"Offer {id:N} is authorized for {recipients.Count} trusted device(s) for 24 hours.", 0, preview.TotalBytes);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<PeerGameTransferOffer>> RefreshOffersAsync(CancellationToken cancellationToken)
    {
        var offers = new List<PeerGameTransferOffer>();
        foreach (var peer in saveSync.Settings.EffectiveTrustedPeers.Where(static peer => peer.State == TrustedPeerState.Active).OrderBy(static peer => peer.DeviceId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var manifest in await transport.ListGameTransferOffersAsync(peer, cancellationToken).ConfigureAwait(false))
                if (ValidateManifest(manifest, peer.DeviceId, out _)) offers.Add(new(peer, manifest));
        }
        return offers.OrderBy(static item => item.Manifest.PackageName, StringComparer.OrdinalIgnoreCase).Take(MaximumOffers * SaveSyncSettings.MaximumTrustedPeers).ToArray();
    }

    public async Task<GameTransferResult> DownloadAsync(PeerGameTransferOffer offer, string destination, IProgress<GameTransferProgress>? progress, CancellationToken cancellationToken)
    {
        if (!ValidateManifest(offer.Manifest, offer.Peer.DeviceId, out var error)) return new(false, false, error!);
        var destinationPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        if (Directory.Exists(destinationPath) && Directory.EnumerateFileSystemEntries(destinationPath).Any()) return new(false, false, "Choose an empty destination folder.");
        var parent = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent)) return new(false, false, "The destination parent folder does not exist.");
        if (new DriveInfo(Path.GetPathRoot(destinationPath)!).AvailableFreeSpace < offer.Manifest.TotalBytes + 64L * 1024 * 1024) return new(false, true, "There is not enough free space for staging and verification.");
        var stage = Path.Combine(parent, $".novalauncher-transfer-{offer.Manifest.OfferId:N}");
        Directory.CreateDirectory(stage);
        if (!ValidateStagingTree(stage, offer.Manifest, out var stagingError)) return new(false, false, stagingError!);
        var timer = Stopwatch.StartNew();
        long completed = 0;
        try
        {
            foreach (var file in offer.Manifest.Files)
            {
                var target = SafeCombine(stage, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (!ValidateStagingTree(stage, offer.Manifest, out stagingError)) return new(false, false, stagingError!);
                var offset = File.Exists(target) ? new FileInfo(target).Length : 0;
                if (offset > file.Length) { File.Delete(target); offset = 0; }
                completed += offset;
                await using (var output = new FileStream(target, FileMode.Append, FileAccess.Write, FileShare.None, ChunkBytes, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    while (offset < file.Length)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var requested = (int)Math.Min(ChunkBytes, file.Length - offset);
                        var chunk = await transport.PullGameTransferChunkAsync(offer.Peer, offer.Manifest.OfferId, file.RelativePath, offset, requested, cancellationToken).ConfigureAwait(false);
                        if (!chunk.Success || chunk.Bytes is null || chunk.Bytes.Length <= 0 || chunk.Bytes.Length > requested) return new(false, true, chunk.Error ?? "The peer returned an invalid chunk.", completed, offer.Manifest.TotalBytes);
                        await output.WriteAsync(chunk.Bytes, cancellationToken).ConfigureAwait(false);
                        offset += chunk.Bytes.Length;
                        completed += chunk.Bytes.Length;
                        progress?.Report(new(offer.Manifest.PackageName, file.RelativePath, completed, offer.Manifest.TotalBytes, completed / Math.Max(timer.Elapsed.TotalSeconds, 0.001)));
                        var minimumElapsed = TimeSpan.FromSeconds(completed / (double)MaximumBytesPerSecond);
                        if (timer.Elapsed < minimumElapsed) await Task.Delay(minimumElapsed - timer.Elapsed, timeProvider, cancellationToken).ConfigureAwait(false);
                    }
                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                if (!string.Equals(await HashFileAsync(target, cancellationToken).ConfigureAwait(false), file.Sha256, StringComparison.Ordinal)) { File.Delete(target); return new(false, true, $"Hash verification failed for {file.RelativePath}; staged bytes were discarded.", completed, offer.Manifest.TotalBytes); }
            }
            if (contentScanner is not null)
            {
                var scan = await contentScanner.ScanAsync(stage, cancellationToken).ConfigureAwait(false);
                if (scan.ScannerAvailable && !scan.Clean) return new(false, false, $"Windows Security did not clear the staged package: {scan.Message}", completed, offer.Manifest.TotalBytes);
            }
            if (!ValidateStagingTree(stage, offer.Manifest, out stagingError)) return new(false, false, stagingError!, completed, offer.Manifest.TotalBytes);
            if (Directory.Exists(destinationPath))
            {
                if (Directory.EnumerateFileSystemEntries(destinationPath).Any()) return new(false, true, "The destination changed during transfer; staged data was retained.", completed, offer.Manifest.TotalBytes);
                Directory.Delete(destinationPath);
            }
            Directory.Move(stage, destinationPath);
            await AppendAuditAsync(new(Guid.NewGuid(), offer.Manifest.OfferId, offer.Peer.DeviceId, offer.Manifest.PackageName, offer.Manifest.TotalBytes, offer.Manifest.Files.Count, timeProvider.GetUtcNow(), "Received and verified; not launched"), CancellationToken.None).ConfigureAwait(false);
            return new(true, false, "The verified folder was received. NovaLauncher did not install or launch any executable.", completed, offer.Manifest.TotalBytes);
        }
        catch (OperationCanceledException)
        {
            await AppendAuditAsync(new(Guid.NewGuid(), offer.Manifest.OfferId, offer.Peer.DeviceId, offer.Manifest.PackageName, offer.Manifest.TotalBytes, offer.Manifest.Files.Count, timeProvider.GetUtcNow(), "Paused or cancelled; resumable staging retained"), CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException) { return new(false, true, exception.Message, completed, offer.Manifest.TotalBytes); }
    }

    public Task<IReadOnlyList<GameTransferAuditItem>> GetHistoryAsync(CancellationToken cancellationToken) => ReadAuditAsync(cancellationToken);

    public Task<IReadOnlyList<GameTransferManifest>> ListAuthorizedGameTransfersAsync(Guid requestingDeviceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsActivePeer(requestingDeviceId)) return Task.FromResult<IReadOnlyList<GameTransferManifest>>([]);
        var now = timeProvider.GetUtcNow();
        return Task.FromResult<IReadOnlyList<GameTransferManifest>>(_offers.Values.Where(offer => offer.Manifest.ExpiresAtUtc > now && offer.RecipientDeviceIds.Contains(requestingDeviceId)).Select(static offer => offer.Manifest).Take(MaximumOffers).ToArray());
    }

    public async Task<GameTransferChunkResult> ServeGameTransferChunkAsync(Guid requestingDeviceId, Guid offerId, string relativePath, long offset, int maximumBytes, CancellationToken cancellationToken)
    {
        if (!IsActivePeer(requestingDeviceId)) return new(false, null, false, "The requesting device is not active and trusted.");
        if (maximumBytes is <= 0 or > ChunkBytes || offset < 0 || !IsSafeRelativePath(relativePath)) return new(false, null, false, "The chunk request is invalid.");
        if (!_offers.TryGetValue(offerId, out var offer) || offer.Manifest.ExpiresAtUtc <= timeProvider.GetUtcNow() || !offer.RecipientDeviceIds.Contains(requestingDeviceId)) return new(false, null, false, "The offer is unavailable or unauthorized.");
        var file = offer.Manifest.Files.FirstOrDefault(candidate => string.Equals(candidate.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
        if (file is null || offset > file.Length) return new(false, null, false, "The package file or offset is invalid.");
        var path = SafeCombine(offer.SourceFolder, file.RelativePath);
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != file.Length || info.LastWriteTimeUtc != file.LastWriteTimeUtc || (info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.SparseFile | FileAttributes.Device)) != 0) return new(false, null, false, "The authorized source changed; create a new preview and offer.");
        var length = (int)Math.Min(maximumBytes, file.Length - offset);
        var bytes = new byte[length];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkBytes, FileOptions.Asynchronous | FileOptions.SequentialScan) { Position = offset };
        if (length > 0) await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return new(true, bytes, offset + length == file.Length, null);
    }

    private bool IsActivePeer(Guid id) => saveSync.Settings.EffectiveTrustedPeers.Any(peer => peer.DeviceId == id && peer.State == TrustedPeerState.Active);
    public static bool IsFileSizeWithinLimit(long fileBytes) => fileBytes is >= 0 and <= MaximumFileBytes;
    public static bool IsPackageSizeWithinLimit(long totalBytes) => totalBytes is >= 0 and <= MaximumPackageBytes;
    private static bool ManifestFilesEqual(IReadOnlyList<GameTransferFile> left, IReadOnlyList<GameTransferFile> right) => left.Count == right.Count && left.Zip(right).All(pair => pair.First == pair.Second);
    private static GameTransferPreview Reject(string name, string source, string error) => new(false, name, source, [], [], 0, error);
    private static bool IsManagedStoreRoot(string path) { var value = path.Replace('/', '\\'); return value.Contains("\\steamapps\\", StringComparison.OrdinalIgnoreCase) || value.EndsWith("\\steamapps", StringComparison.OrdinalIgnoreCase) || value.Contains("\\Epic Games\\", StringComparison.OrdinalIgnoreCase) || value.Contains("\\XboxGames\\", StringComparison.OrdinalIgnoreCase) || value.Contains("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase); }
    private static string NormalizeRelativePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    private static bool IsSafeRelativePath(string path) => !string.IsNullOrWhiteSpace(path) && path.Length <= 1024 && !Path.IsPathRooted(path) && !path.Contains(':') && path.Split('/', StringSplitOptions.RemoveEmptyEntries).All(segment => segment is not ("." or "..") && !segment.Any(char.IsControl));
    private static string SafeCombine(string root, string relative) { if (!IsSafeRelativePath(relative)) throw new InvalidDataException("Unsafe transfer path."); var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)); var full = Path.GetFullPath(Path.Combine(normalized, relative.Replace('/', Path.DirectorySeparatorChar))); if (!full.StartsWith(normalized + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Transfer path escaped its root."); return full; }
    private static bool ValidateStagingTree(string stage, GameTransferManifest manifest, out string? error)
    {
        error = null;
        var root = new DirectoryInfo(stage);
        if ((root.Attributes & FileAttributes.ReparsePoint) != 0) { error = "The resumable staging folder is a reparse point and cannot be trusted."; return false; }
        var allowedFiles = manifest.Files.ToDictionary(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
        var allowedDirectories = manifest.Files.SelectMany(static file => ParentPaths(file.RelativePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(stage);
        while (pendingDirectories.Count > 0)
        {
            foreach (var directory in Directory.EnumerateDirectories(pendingDirectories.Pop()))
            {
                var info = new DirectoryInfo(directory);
                var relative = NormalizeRelativePath(Path.GetRelativePath(stage, directory));
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || !allowedDirectories.Contains(relative)) { error = "The resumable staging folder contains an unexpected or unsafe directory."; return false; }
                pendingDirectories.Push(directory);
            }
        }
        foreach (var path in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(path);
            var relative = NormalizeRelativePath(Path.GetRelativePath(stage, path));
            if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.SparseFile | FileAttributes.Device)) != 0 || !allowedFiles.TryGetValue(relative, out var expected) || info.Length > expected.Length)
            { error = "The resumable staging folder contains an unexpected or unsafe file."; return false; }
        }
        return true;
    }
    private static IEnumerable<string> ParentPaths(string relativePath)
    {
        var current = Path.GetDirectoryName(relativePath.Replace('/', Path.DirectorySeparatorChar));
        while (!string.IsNullOrWhiteSpace(current)) { yield return NormalizeRelativePath(current); current = Path.GetDirectoryName(current); }
    }
    private static async Task<string> HashFileAsync(string path, CancellationToken token) { await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkBytes, FileOptions.Asynchronous | FileOptions.SequentialScan); return Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false)).ToLowerInvariant(); }
    private static bool ValidateManifest(GameTransferManifest manifest, Guid sender, out string? error)
    {
        error = null;
        if (manifest.OfferId == Guid.Empty || manifest.GameId.Value == Guid.Empty || manifest.SenderDeviceId != sender || manifest.PackageName.Length is < 1 or > 200 || manifest.Files.Count is <= 0 or > MaximumFiles || !IsPackageSizeWithinLimit(manifest.TotalBytes)) { error = "The peer transfer manifest is invalid."; return false; }
        long total = 0; var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files) { if (!IsSafeRelativePath(file.RelativePath) || !paths.Add(file.RelativePath) || !IsFileSizeWithinLimit(file.Length) || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit)) { error = "The peer transfer manifest contains an unsafe file."; return false; } total = checked(total + file.Length); }
        if (total != manifest.TotalBytes) { error = "The peer transfer manifest size is inconsistent."; return false; }
        return true;
    }
    private async Task AppendAuditAsync(GameTransferAuditItem item, CancellationToken token) { var history = (await ReadAuditAsync(token).ConfigureAwait(false)).Append(item).TakeLast(100).ToArray(); Directory.CreateDirectory(Path.GetDirectoryName(_auditPath)!); var stage = _auditPath + $".{Guid.NewGuid():N}.tmp"; await File.WriteAllTextAsync(stage, JsonSerializer.Serialize(history), token).ConfigureAwait(false); File.Move(stage, _auditPath, true); }
    private async Task<IReadOnlyList<GameTransferAuditItem>> ReadAuditAsync(CancellationToken token) { if (!File.Exists(_auditPath)) return []; try { return JsonSerializer.Deserialize<GameTransferAuditItem[]>(await File.ReadAllTextAsync(_auditPath, token).ConfigureAwait(false)) ?? []; } catch (Exception exception) when (exception is IOException or JsonException) { return []; } }
    public void Dispose() => _gate.Dispose();
}
