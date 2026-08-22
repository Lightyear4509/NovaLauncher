using System.Security.Cryptography;
using System.Text.Json;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;

namespace NovaLauncher.Infrastructure.SaveSync;

public sealed class PrivateSnapshotDestinationService(
    string dataRoot,
    TimeProvider timeProvider,
    Func<string, CancellationToken, Task>? copyCheckpoint = null) : IPrivateSnapshotDestinationService, IDisposable
{
    public const long MinimumBudgetBytes = 64L * 1024 * 1024;
    public const long MaximumBudgetBytes = 1024L * 1024 * 1024 * 1024;
    public const int MaximumHealthEvents = 500;
    private readonly string _sourceRoot = Path.Combine(Path.GetFullPath(dataRoot), "SaveSync", "Snapshots");
    private readonly string _configurationPath = Path.Combine(Path.GetFullPath(dataRoot), "SaveSync", "private-destination.json");
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, DestinationPublishPreview> _publishPreviews = [];
    private readonly Dictionary<Guid, DestinationRepairPreview> _repairPreviews = [];
    private readonly Dictionary<Guid, PrivateMetadataPreview> _metadataPreviews = [];
    private PrivateDestinationConfiguration? _configuration;

    public PrivateDestinationConfiguration? Configuration => _configuration ??= LoadConfiguration();

    public async Task<PrivateDestinationResult> ConfigureAsync(PrivateDestinationConfiguration configuration, CancellationToken cancellationToken)
    {
        var validation = ValidateConfiguration(configuration);
        if (validation is not null) return new(false, validation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configurationPath)!);
            Directory.CreateDirectory(configuration.RootPath);
            var marker = Path.Combine(configuration.RootPath, ".novalauncher-destination");
            await AtomicWriteTextAsync(marker, "NovaLauncher private snapshot destination v1", cancellationToken).ConfigureAwait(false);
            await AtomicWriteTextAsync(_configurationPath, JsonSerializer.Serialize(configuration), cancellationToken).ConfigureAwait(false);
            _configuration = configuration with { RootPath = Path.GetFullPath(configuration.RootPath) };
            await AppendHealthAsync("Configure", DestinationHealthOutcome.Succeeded, null, null, 0, "Destination verified and configured.", cancellationToken).ConfigureAwait(false);
            return new(true, "Private snapshot destination configured. Windows supplies any network-share authentication.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(false, $"The destination failed closed during configuration: {exception.Message}");
        }
        finally { _gate.Release(); }
    }

    public async Task<(DestinationPublishPreview? Preview, string? Error)> PreviewPublishAsync(GameId gameId, CancellationToken cancellationToken)
    {
        var configuration = Configuration;
        if (configuration is null) return (null, "Choose a private snapshot destination first.");
        if (gameId.Value == Guid.Empty) return (null, "Choose a valid linked game.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sourceGame = Path.Combine(_sourceRoot, gameId.Value.ToString("N"));
            var headPath = Path.Combine(sourceGame, "head.txt");
            if (!File.Exists(headPath) || !Guid.TryParse(await File.ReadAllTextAsync(headPath, cancellationToken).ConfigureAwait(false), out var head))
                return (null, "No verified local snapshot head is available for this game.");
            var manifest = await ReadAndVerifySnapshotAsync(Path.Combine(sourceGame, head.ToString("N")), gameId, head, cancellationToken).ConfigureAwait(false);
            var snapshotBytes = manifest.Files.Sum(static file => file.Length);
            var destinationGame = DestinationGameRoot(configuration, gameId);
            var inventory = await ReadInventoryAsync(destinationGame, cancellationToken).ConfigureAwait(false);
            var existingBytes = inventory.Sum(static item => item.Bytes);
            var alreadyPublished = inventory.Any(item => item.Id == head);
            var additionalBytes = alreadyPublished ? 0 : snapshotBytes;
            var orderedRemovals = inventory.Where(item => item.Id != head).OrderBy(item => item.CreatedAtUtc).ToList();
            while (orderedRemovals.Count > 0 && inventory.Count - orderedRemovals.Count + (alreadyPublished ? 0 : 1) <= configuration.RetainedSnapshotsPerGame &&
                   existingBytes + additionalBytes - orderedRemovals.Sum(static item => item.Bytes) <= configuration.StorageBudgetBytes)
                orderedRemovals.RemoveAt(orderedRemovals.Count - 1);
            var remove = orderedRemovals.Select(static item => item.Id).ToArray();
            var removeBytes = orderedRemovals.Sum(static item => item.Bytes);
            var canPublish = snapshotBytes <= configuration.StorageBudgetBytes && existingBytes + additionalBytes - removeBytes <= configuration.StorageBudgetBytes;
            var preview = new DestinationPublishPreview(Guid.NewGuid(), gameId, head, Kind(configuration.RootPath), DisplayPath(configuration.RootPath),
                manifest.Files.Count, snapshotBytes, existingBytes, configuration.StorageBudgetBytes, remove, removeBytes, canPublish,
                canPublish ? $"Publish {manifest.Files.Count} verified file(s); remove {remove.Length} expired non-head snapshot(s) after commit."
                           : "The snapshot cannot fit within this destination's configured storage budget.");
            _publishPreviews.Clear();
            _publishPreviews[preview.PreviewId] = preview;
            return (preview, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            return (null, $"Destination preview failed closed: {exception.Message}");
        }
        finally { _gate.Release(); }
    }

    public async Task<PrivateDestinationResult> PublishAsync(DestinationPublishPreview preview, CancellationToken cancellationToken)
    {
        var configuration = Configuration;
        if (configuration is null) return new(false, "The private destination is no longer configured.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_publishPreviews.Remove(preview.PreviewId, out var approved) || approved != preview || !preview.CanPublish)
                return new(false, "The destination preview is missing, stale, or not publishable. Preview again.");
            await using var lease = AcquireWriterLease(configuration.RootPath);
            var source = Path.Combine(_sourceRoot, preview.GameId.Value.ToString("N"), preview.SnapshotId.ToString("N"));
            await ReadAndVerifySnapshotAsync(source, preview.GameId, preview.SnapshotId, cancellationToken).ConfigureAwait(false);
            var gameRoot = DestinationGameRoot(configuration, preview.GameId);
            Directory.CreateDirectory(gameRoot);
            var stage = Path.Combine(gameRoot, $".staging-{preview.PreviewId:N}");
            var committed = Path.Combine(gameRoot, preview.SnapshotId.ToString("N"));
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
            await CopyDirectoryVerifiedAsync(source, stage, cancellationToken).ConfigureAwait(false);
            await ReadAndVerifySnapshotAsync(stage, preview.GameId, preview.SnapshotId, cancellationToken).ConfigureAwait(false);
            if (!Directory.Exists(committed)) Directory.Move(stage, committed);
            else Directory.Delete(stage, true);
            await AtomicWriteTextAsync(Path.Combine(gameRoot, "head.txt"), preview.SnapshotId.ToString("D"), cancellationToken).ConfigureAwait(false);
            foreach (var id in preview.SnapshotsToRemove)
            {
                var obsolete = Path.Combine(gameRoot, id.ToString("N"));
                if (Directory.Exists(obsolete) && id != preview.SnapshotId) Directory.Delete(obsolete, true);
            }
            await AppendHealthAsync("Publish", DestinationHealthOutcome.Succeeded, preview.GameId, preview.SnapshotId, preview.SnapshotBytes,
                "Verified snapshot committed atomically; head published last.", cancellationToken).ConfigureAwait(false);
            return new(true, "Verified snapshot published. No live save files were overwritten.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            await TryAppendFailureAsync("Publish", preview.GameId, preview.SnapshotId, exception.Message).ConfigureAwait(false);
            return new(false, $"Destination publish failed closed; no partial snapshot was accepted: {exception.Message}");
        }
        finally { _gate.Release(); }
    }

    public async Task<(DestinationRepairPreview? Preview, string? Error)> PreviewQuarantineAsync(GameId gameId, Guid snapshotId, CancellationToken cancellationToken)
    {
        var configuration = Configuration;
        if (configuration is null) return (null, "Choose a private snapshot destination first.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var gameRoot = DestinationGameRoot(configuration, gameId);
            var target = Path.Combine(gameRoot, snapshotId.ToString("N"));
            if (!Directory.Exists(target)) return (null, "The selected destination snapshot no longer exists.");
            var head = await ReadHeadAsync(gameRoot, cancellationToken).ConfigureAwait(false);
            Guid? parent = null;
            try
            {
                var manifest = await ReadAndVerifySnapshotAsync(target, gameId, snapshotId, cancellationToken).ConfigureAwait(false);
                parent = manifest.ParentSnapshotId;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
                try
                {
                    var raw = JsonSerializer.Deserialize<SaveSnapshotManifest>(await File.ReadAllTextAsync(Path.Combine(target, "manifest.txt"), cancellationToken).ConfigureAwait(false));
                    parent = raw?.ParentSnapshotId;
                }
                catch (Exception parseException) when (parseException is IOException or UnauthorizedAccessException or JsonException) { parent = null; }
            }
            Guid? verifiedParent = parent is { } candidate && Directory.Exists(Path.Combine(gameRoot, candidate.ToString("N"))) &&
                await IsVerifiedAsync(Path.Combine(gameRoot, candidate.ToString("N")), gameId, candidate, cancellationToken).ConfigureAwait(false)
                    ? candidate
                    : null;
            var canRepair = head != snapshotId || verifiedParent is not null;
            var action = head == snapshotId ? "Quarantine snapshot and repoint head to its verified parent" : "Quarantine historical snapshot";
            var preview = new DestinationRepairPreview(Guid.NewGuid(), gameId, snapshotId, DisplayPath(configuration.RootPath), head == snapshotId,
                verifiedParent, action, canRepair, canRepair ? "Only destination-local data will move; live saves remain untouched." : "Current head has no verified parent; repair cannot proceed.");
            _repairPreviews.Clear();
            _repairPreviews[preview.PreviewId] = preview;
            return (preview, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return (null, $"Repair preview failed closed: {exception.Message}");
        }
        finally { _gate.Release(); }
    }

    public async Task<PrivateDestinationResult> QuarantineAsync(DestinationRepairPreview preview, CancellationToken cancellationToken)
    {
        var configuration = Configuration;
        if (configuration is null) return new(false, "The private destination is no longer configured.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_repairPreviews.Remove(preview.PreviewId, out var approved) || approved != preview || !preview.CanRepair)
                return new(false, "The repair preview is missing, stale, or unsafe. Preview again.");
            await using var lease = AcquireWriterLease(configuration.RootPath);
            var gameRoot = DestinationGameRoot(configuration, preview.GameId);
            var source = Path.Combine(gameRoot, preview.SnapshotId.ToString("N"));
            if (!Directory.Exists(source)) return new(false, "The selected snapshot changed after preview. Nothing was moved.");
            if (preview.IsCurrentHead)
            {
                if (preview.VerifiedParentSnapshotId is not { } parent ||
                    !await IsVerifiedAsync(Path.Combine(gameRoot, parent.ToString("N")), preview.GameId, parent, cancellationToken).ConfigureAwait(false))
                    return new(false, "The proposed replacement head is no longer verified. Nothing was moved.");
                await AtomicWriteTextAsync(Path.Combine(gameRoot, "head.txt"), parent.ToString("D"), cancellationToken).ConfigureAwait(false);
            }
            var quarantine = Path.Combine(configuration.RootPath, "quarantine", preview.GameId.Value.ToString("N"), $"{preview.SnapshotId:N}-{timeProvider.GetUtcNow():yyyyMMddHHmmss}");
            Directory.CreateDirectory(Path.GetDirectoryName(quarantine)!);
            Directory.Move(source, quarantine);
            await AppendHealthAsync("Repair", DestinationHealthOutcome.Quarantined, preview.GameId, preview.SnapshotId, 0,
                preview.IsCurrentHead ? "Snapshot quarantined after verified parent became head." : "Historical snapshot quarantined.", cancellationToken).ConfigureAwait(false);
            return new(true, "Snapshot quarantined with an auditable health event. Live save files were not changed.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await TryAppendFailureAsync("Repair", preview.GameId, preview.SnapshotId, exception.Message).ConfigureAwait(false);
            return new(false, $"Repair failed closed: {exception.Message}");
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<DestinationHealthEvent>> GetHealthHistoryAsync(CancellationToken cancellationToken)
    {
        var configuration = Configuration;
        if (configuration is null) return [];
        var path = Path.Combine(configuration.RootPath, "health.jsonl");
        if (!File.Exists(path)) return [];
        var lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
        return lines.TakeLast(MaximumHealthEvents).Select(line => { try { return JsonSerializer.Deserialize<DestinationHealthEvent>(line); } catch (JsonException) { return null; } })
            .Where(static item => item is not null).Cast<DestinationHealthEvent>().OrderByDescending(static item => item.TimestampUtc).ToArray();
    }

    public async Task<IReadOnlyList<SaveSnapshotHistoryItem>> GetDestinationSnapshotsAsync(GameId gameId, CancellationToken cancellationToken)
    {
        var configuration = Configuration;
        if (configuration is null || gameId.Value == Guid.Empty) return [];
        var gameRoot = DestinationGameRoot(configuration, gameId);
        if (!Directory.Exists(gameRoot)) return [];
        var head = await ReadHeadAsync(gameRoot, cancellationToken).ConfigureAwait(false);
        var result = new List<SaveSnapshotHistoryItem>();
        foreach (var directory in Directory.EnumerateDirectories(gameRoot).Where(path => Guid.TryParse(Path.GetFileName(path), out _)).Take(SaveSyncCoordinator.MaximumRetainedSnapshotsPerGame + 1))
        {
            var id = Guid.Parse(Path.GetFileName(directory));
            try
            {
                var manifest = await ReadAndVerifySnapshotAsync(directory, gameId, id, cancellationToken).ConfigureAwait(false);
                result.Add(new(id, manifest.ParentSnapshotId, manifest.DeviceId, manifest.CreatedAtUtc, manifest.Files.Count,
                    manifest.Files.Sum(static file => file.Length), head == id, true, null));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
                result.Add(new(id, null, Guid.Empty, DateTimeOffset.MinValue, 0, 0, head == id, false, exception.Message));
            }
        }
        return result.OrderByDescending(static item => item.CreatedAtUtc).ThenByDescending(static item => item.SnapshotId).ToArray();
    }

    public Task<(PrivateMetadataPreview? Preview, string? Error)> PreviewMetadataPushAsync(PrivateMetadataEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Configuration is not { SyncNotesAndTags: true }) return Task.FromResult<(PrivateMetadataPreview?, string?)>((null, "Enable notes-and-tags synchronization for this destination first."));
        var error = ValidateMetadata(entry);
        if (error is not null) return Task.FromResult<(PrivateMetadataPreview?, string?)>((null, error));
        var preview = new PrivateMetadataPreview(Guid.NewGuid(), "Push", entry,
            $"Publish notes ({entry.Notes?.Length ?? 0} characters) and {entry.Tags.Count} tag(s) for {entry.Name}. No target, artwork path, credential, or device identity is included.");
        _metadataPreviews.Clear();
        _metadataPreviews[preview.PreviewId] = preview;
        return Task.FromResult<(PrivateMetadataPreview?, string?)>((preview, null));
    }

    public async Task<(PrivateMetadataPreview? Preview, string? Error)> PreviewMetadataPullAsync(GameId gameId, CancellationToken cancellationToken)
    {
        var configuration = Configuration;
        if (configuration is not { SyncNotesAndTags: true }) return (null, "Enable notes-and-tags synchronization for this destination first.");
        if (gameId.Value == Guid.Empty) return (null, "Choose a valid game identity.");
        try
        {
            var path = Path.Combine(configuration.RootPath, "metadata", gameId.Value.ToString("N") + ".json");
            if (!File.Exists(path)) return (null, "No shared notes-and-tags package exists for this game.");
            if (new FileInfo(path).Length > 128 * 1024) return (null, "The metadata package exceeds its 128 KiB safety limit.");
            var entry = JsonSerializer.Deserialize<PrivateMetadataEntry>(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
            var error = entry is null || entry.GameId != gameId ? "The metadata package identity is invalid." : ValidateMetadata(entry);
            if (error is not null) return (null, error);
            var preview = new PrivateMetadataPreview(Guid.NewGuid(), "Pull", entry!,
                $"Replace local notes with {entry!.Notes?.Length ?? 0} reviewed characters and {entry.Tags.Count} reviewed tag(s). Executable and provider data are unaffected.");
            _metadataPreviews.Clear();
            _metadataPreviews[preview.PreviewId] = preview;
            return (preview, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return (null, $"Metadata preview failed closed: {exception.Message}");
        }
    }

    public async Task<PrivateDestinationResult> CommitMetadataPushAsync(PrivateMetadataPreview preview, CancellationToken cancellationToken)
    {
        var configuration = Configuration;
        if (configuration is not { SyncNotesAndTags: true }) return new(false, "Notes-and-tags synchronization is no longer enabled.");
        if (!_metadataPreviews.Remove(preview.PreviewId, out var approved) || approved != preview || preview.Direction != "Push")
            return new(false, "The metadata preview is stale. Preview again.");
        try
        {
            await using var lease = AcquireWriterLease(configuration.RootPath);
            var path = Path.Combine(configuration.RootPath, "metadata", preview.Entry.GameId.Value.ToString("N") + ".json");
            await AtomicWriteTextAsync(path, JsonSerializer.Serialize(preview.Entry), cancellationToken).ConfigureAwait(false);
            await AppendHealthAsync("Metadata push", DestinationHealthOutcome.Succeeded, preview.Entry.GameId, null, 0, "Reviewed notes and tags published separately.", cancellationToken).ConfigureAwait(false);
            return new(true, "Reviewed notes and tags published separately from save snapshots.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(false, $"Metadata publication failed closed: {exception.Message}");
        }
    }

    public Task<(PrivateMetadataEntry? Entry, string? Error)> CommitMetadataPullAsync(PrivateMetadataPreview preview, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_metadataPreviews.Remove(preview.PreviewId, out var approved) || approved != preview || preview.Direction != "Pull")
            return Task.FromResult<(PrivateMetadataEntry?, string?)>((null, "The metadata preview is stale. Preview again."));
        return Task.FromResult<(PrivateMetadataEntry?, string?)>((preview.Entry, null));
    }

    private static string? ValidateConfiguration(PrivateDestinationConfiguration value)
    {
        if (string.IsNullOrWhiteSpace(value.RootPath) || !Path.IsPathFullyQualified(value.RootPath)) return "Choose an absolute local-folder destination path.";
        if (Kind(value.RootPath) == PrivateDestinationKind.WindowsNetworkShare) return "SMB/NAS writing remains disabled until the required locking, reconnect, and concurrent-writer qualification matrix passes.";
        if (value.StorageBudgetBytes is < MinimumBudgetBytes or > MaximumBudgetBytes) return "Choose a storage budget from 64 MiB through 1 TiB.";
        if (value.RetainedSnapshotsPerGame is < 1 or > SaveSyncCoordinator.MaximumRetainedSnapshotsPerGame) return "Choose between 1 and 20 retained snapshots per game.";
        return null;
    }

    private static string? ValidateMetadata(PrivateMetadataEntry entry)
    {
        if (entry.GameId.Value == Guid.Empty || string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Length > 300) return "Metadata package identity is invalid.";
        if (entry.Notes is { Length: > 50_000 } || entry.Tags.Count > 100 || entry.Tags.Any(static tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 100))
            return "Metadata notes or tags exceed their safety limits.";
        if (entry.Tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() != entry.Tags.Count) return "Metadata tags contain duplicates.";
        return null;
    }

    private PrivateDestinationConfiguration? LoadConfiguration()
    {
        try
        {
            if (!File.Exists(_configurationPath)) return null;
            var value = JsonSerializer.Deserialize<PrivateDestinationConfiguration>(File.ReadAllText(_configurationPath));
            return value is not null && ValidateConfiguration(value) is null ? value : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    private static async Task<SaveSnapshotManifest> ReadAndVerifySnapshotAsync(string directory, GameId gameId, Guid snapshotId, CancellationToken token)
    {
        var manifestPath = Path.Combine(directory, "manifest.txt");
        if (!File.Exists(manifestPath)) throw new InvalidDataException("Snapshot manifest is missing.");
        var manifest = JsonSerializer.Deserialize<SaveSnapshotManifest>(await File.ReadAllTextAsync(manifestPath, token).ConfigureAwait(false))
            ?? throw new InvalidDataException("Snapshot manifest is invalid.");
        if (manifest.GameId != gameId || manifest.SnapshotId != snapshotId || manifest.Files.Count > SaveSyncCoordinator.MaximumFiles)
            throw new InvalidDataException("Snapshot identity or file count is invalid.");
        long total = 0;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Files)
        {
            if (!SaveSyncCoordinator.IsSafeRelativePath(entry.RelativePath) || !paths.Add(entry.RelativePath) || entry.Length is < 0 or > SaveSyncCoordinator.MaximumFileBytes)
                throw new InvalidDataException("Snapshot contains an unsafe or case-colliding path.");
            total += entry.Length;
            if (total > SaveSyncCoordinator.MaximumSnapshotBytes) throw new InvalidDataException("Snapshot exceeds its byte budget.");
            var path = SafeCombine(directory, entry.RelativePath);
            EnsureNoReparsePoint(directory, path);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length != entry.Length || !string.Equals(await HashFileAsync(path, token).ConfigureAwait(false), entry.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Integrity verification failed for {entry.RelativePath}.");
        }
        return manifest;
    }

    private static async Task<List<(Guid Id, long Bytes, DateTimeOffset CreatedAtUtc)>> ReadInventoryAsync(string gameRoot, CancellationToken token)
    {
        var result = new List<(Guid, long, DateTimeOffset)>();
        if (!Directory.Exists(gameRoot)) return result;
        foreach (var directory in Directory.EnumerateDirectories(gameRoot).Take(SaveSyncCoordinator.MaximumRetainedSnapshotsPerGame + 1))
        {
            if (!Guid.TryParse(Path.GetFileName(directory), out var id)) continue;
            try
            {
                var manifest = JsonSerializer.Deserialize<SaveSnapshotManifest>(await File.ReadAllTextAsync(Path.Combine(directory, "manifest.txt"), token).ConfigureAwait(false));
                if (manifest is not null) result.Add((id, manifest.Files.Sum(static file => file.Length), manifest.CreatedAtUtc));
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException) { result.Add((id, 0, DateTimeOffset.MinValue)); }
        }
        return result;
    }

    private async Task AppendHealthAsync(string operation, DestinationHealthOutcome outcome, GameId? gameId, Guid? snapshotId, long bytes, string message, CancellationToken token)
    {
        var configuration = Configuration ?? throw new InvalidOperationException("Destination is not configured.");
        Directory.CreateDirectory(configuration.RootPath);
        var item = new DestinationHealthEvent(Guid.NewGuid(), timeProvider.GetUtcNow(), Kind(configuration.RootPath), operation, outcome, gameId, snapshotId, bytes, message);
        var path = Path.Combine(configuration.RootPath, "health.jsonl");
        await File.AppendAllTextAsync(path, JsonSerializer.Serialize(item) + Environment.NewLine, token).ConfigureAwait(false);
        if (new FileInfo(path).Length > 2 * 1024 * 1024)
        {
            var retained = (await File.ReadAllLinesAsync(path, token).ConfigureAwait(false)).TakeLast(MaximumHealthEvents);
            await AtomicWriteTextAsync(path, string.Join(Environment.NewLine, retained) + Environment.NewLine, token).ConfigureAwait(false);
        }
    }

    private async Task TryAppendFailureAsync(string operation, GameId? gameId, Guid? snapshotId, string message)
    {
        try { await AppendHealthAsync(operation, DestinationHealthOutcome.FailedClosed, gameId, snapshotId, 0, message, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException) { }
    }

    private async Task CopyDirectoryVerifiedAsync(string source, string target, CancellationToken token)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(source, file);
            if (!SaveSyncCoordinator.IsSafeRelativePath(relative)) throw new InvalidDataException("Source snapshot contains an unsafe path.");
            EnsureNoReparsePoint(source, file);
            var destination = SafeCombine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, SaveSyncCoordinator.SaveTransferChunkBytes, true);
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, SaveSyncCoordinator.SaveTransferChunkBytes, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await input.CopyToAsync(output, SaveSyncCoordinator.SaveTransferChunkBytes, token).ConfigureAwait(false);
            await output.FlushAsync(token).ConfigureAwait(false);
            if (copyCheckpoint is not null) await copyCheckpoint(relative, token).ConfigureAwait(false);
        }
    }

    private static async Task AtomicWriteTextAsync(string path, string text, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync(text.AsMemory(), token).ConfigureAwait(false);
                await writer.FlushAsync(token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string SafeCombine(string root, string relative)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var full = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!full.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escapes the destination root.");
        return full;
    }

    private static void EnsureNoReparsePoint(string root, string path)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var current = Path.GetFullPath(path);
        while (!string.Equals(current, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Snapshot paths cannot traverse reparse points.");
            current = Path.GetDirectoryName(current) ?? throw new InvalidDataException("Snapshot path has no managed parent.");
            if (!current.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Snapshot path escapes its managed root.");
        }
    }

    private static FileStream AcquireWriterLease(string root)
    {
        Directory.CreateDirectory(root);
        return new FileStream(Path.Combine(root, ".novalauncher-write.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.WriteThrough);
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, SaveSyncCoordinator.SaveTransferChunkBytes, true);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
    }

    private static async Task<bool> IsVerifiedAsync(string directory, GameId gameId, Guid snapshotId, CancellationToken token)
    {
        try { await ReadAndVerifySnapshotAsync(directory, gameId, snapshotId, token).ConfigureAwait(false); return true; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException) { return false; }
    }

    private static async Task<Guid?> ReadHeadAsync(string gameRoot, CancellationToken token)
    {
        var path = Path.Combine(gameRoot, "head.txt");
        return File.Exists(path) && Guid.TryParse(await File.ReadAllTextAsync(path, token).ConfigureAwait(false), out var head) ? head : null;
    }

    private static string DestinationGameRoot(PrivateDestinationConfiguration configuration, GameId gameId) =>
        Path.Combine(configuration.RootPath, "snapshots", gameId.Value.ToString("N"));
    private static PrivateDestinationKind Kind(string path) => path.StartsWith("\\\\", StringComparison.Ordinal) ? PrivateDestinationKind.WindowsNetworkShare : PrivateDestinationKind.LocalFolder;
    private static string DisplayPath(string path) => Kind(path) == PrivateDestinationKind.WindowsNetworkShare ? "Windows-authenticated network share" : Path.GetFullPath(path);
    public void Dispose() => _gate.Dispose();
}
