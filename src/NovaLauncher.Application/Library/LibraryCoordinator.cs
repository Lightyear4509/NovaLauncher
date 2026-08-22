using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Steam;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;

namespace NovaLauncher.Application.Library;

public enum LibraryMutationStatus
{
    Saved,
    ValidationFailed,
    PersistenceFailed,
}

public sealed record LibraryMutationResult(
    LibraryMutationStatus Status,
    LibraryItem? Item,
    IReadOnlyDictionary<string, string> Errors,
    string? Error);

public sealed record DuplicateMergePreview(LibraryItem Survivor, LibraryItem Duplicate, LibraryItem? Merged, string? Error)
{
    public bool CanMerge => Merged is not null && Error is null;
}

public enum LibraryLoadState
{
    NotLoaded,
    Empty,
    Ready,
    Recovered,
    Failed,
}

public enum LibrarySort
{
    Name,
    Platform,
    RecentlyUpdated,
}

public sealed class LibraryCoordinator(
    IDocumentStore<GamesDocument> store,
    ManualGameDraftValidator validator,
    TimeProvider timeProvider) : IDisposable
{
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private LibraryItem[] _games = [];
    private long _revision;

    public IReadOnlyList<LibraryItem> Games => _games;

    public LibraryLoadState LoadState { get; private set; }

    public string? LoadWarning { get; private set; }

    public void Dispose() => _mutationGate.Dispose();

    public async Task<DocumentLoadResult<GamesDocument>> LoadAsync(CancellationToken cancellationToken)
    {
        var result = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (result.Document is not null)
        {
            _games = result.Document.Games.ToArray();
            _revision++;
        }

        LoadWarning = result.Warning;
        LoadState = result.Status switch
        {
            DocumentLoadStatus.NotFound => LibraryLoadState.Empty,
            DocumentLoadStatus.Loaded or DocumentLoadStatus.MigratedLegacy =>
                _games.Length == 0 ? LibraryLoadState.Empty : LibraryLoadState.Ready,
            DocumentLoadStatus.RecoveredFromBackup => LibraryLoadState.Recovered,
            _ => LibraryLoadState.Failed,
        };

        return result;
    }

    public async Task<LibraryMutationResult> EditManualGameAsync(
        GameId gameId,
        ManualGameDraft draft,
        CancellationToken cancellationToken)
    {
        var validation = validator.Validate(draft);
        if (!validation.IsValid)
        {
            return ValidationFailure(validation);
        }

        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = FindIndex(gameId);
            if (index < 0)
            {
                return PersistenceFailure("The selected game no longer exists.");
            }

            var existing = _games[index];
            var updated = existing with
            {
                Name = draft.Name.Trim(),
                Platform = draft.Platform.Trim(),
                LaunchTarget = CreateLaunchTarget(draft),
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            };
            return await SaveReplacementAsync(index, updated, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public Task<LibraryMutationResult> RemoveAsync(GameId gameId, CancellationToken cancellationToken) =>
        MutateExistingAsync(gameId, item: null, cancellationToken);

    public DuplicateMergePreview PreviewDuplicateMerge(GameId survivorId, GameId duplicateId)
    {
        var survivor = _games.FirstOrDefault(game => game.Id == survivorId);
        var duplicate = _games.FirstOrDefault(game => game.Id == duplicateId);
        if (survivor is null || duplicate is null || survivorId == duplicateId)
            return new(survivor!, duplicate!, null, "Choose two different existing game records.");

        var identityError = ValidateDuplicateIdentity(survivor, duplicate);
        if (identityError is not null) return new(survivor, duplicate, null, identityError);
        if (survivor.SaveSyncId is not null && duplicate.SaveSyncId is not null && survivor.SaveSyncId != duplicate.SaveSyncId)
            return new(survivor, duplicate, null, "The records have different save-sync identities and cannot be merged automatically.");
        if (!CompatibleOptionalPath(survivor.SaveDirectory, duplicate.SaveDirectory))
            return new(survivor, duplicate, null, "The records use different save folders. Choose and verify one before merging.");
        if (survivor.LinkedIdentity is not null && duplicate.LinkedIdentity is not null &&
            (!string.Equals(survivor.LinkedIdentity.ProviderId, duplicate.LinkedIdentity.ProviderId, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(survivor.LinkedIdentity.ProviderItemId, duplicate.LinkedIdentity.ProviderItemId, StringComparison.Ordinal)))
            return new(survivor, duplicate, null, "The records have different confirmed provider identities. Unlink or rematch one before merging.");

        var now = timeProvider.GetUtcNow();
        var merged = survivor with
        {
            Metadata = PreferMetadata(survivor.Metadata, duplicate.Metadata),
            Artwork = PreferArtwork(survivor.Artwork, duplicate.Artwork),
            IsFavorite = survivor.IsFavorite || duplicate.IsFavorite,
            AddedAtUtc = survivor.AddedAtUtc <= duplicate.AddedAtUtc ? survivor.AddedAtUtc : duplicate.AddedAtUtc,
            UpdatedAtUtc = now,
            TotalPlayTime = survivor.TotalPlayTime >= duplicate.TotalPlayTime ? survivor.TotalPlayTime : duplicate.TotalPlayTime,
            LastPlayedAtUtc = Latest(survivor.LastPlayedAtUtc, duplicate.LastPlayedAtUtc),
            SaveDirectory = survivor.SaveDirectory ?? duplicate.SaveDirectory,
            SaveSyncId = survivor.SaveSyncId ?? duplicate.SaveSyncId,
            SaveSyncLabel = survivor.SaveSyncLabel ?? duplicate.SaveSyncLabel,
            LinkedIdentity = survivor.LinkedIdentity ?? duplicate.LinkedIdentity,
        };
        return new(survivor, duplicate, merged, null);
    }

    public async Task<LibraryMutationResult> CommitDuplicateMergeAsync(
        DuplicateMergePreview preview,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!preview.CanMerge) return PersistenceFailure(preview.Error ?? "The duplicate merge preview is invalid.");
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var survivorIndex = FindIndex(preview.Survivor.Id);
            var duplicateIndex = FindIndex(preview.Duplicate.Id);
            if (survivorIndex < 0 || duplicateIndex < 0 || survivorIndex == duplicateIndex)
                return PersistenceFailure("The library changed after preview. Review the duplicate again.");
            if (_games[survivorIndex] != preview.Survivor || _games[duplicateIndex] != preview.Duplicate)
                return PersistenceFailure("The library changed after preview. Review the duplicate again.");

            var staged = _games.Where(game => game.Id != preview.Duplicate.Id).ToList();
            var stagedSurvivorIndex = staged.FindIndex(game => game.Id == preview.Survivor.Id);
            staged[stagedSurvivorIndex] = preview.Merged!;
            var save = await store.SaveAsync(new GamesDocument(GamesDocument.CurrentSchemaVersion, staged), cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved) return PersistenceFailure(save.Error);
            _games = staged.ToArray();
            _revision++;
            return Success(preview.Merged!);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task<LibraryMutationResult> ApplyEnrichmentAsync(
        GameId gameId,
        GameMetadata metadata,
        GameArtwork artwork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(artwork);
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = FindIndex(gameId);
            if (index < 0)
            {
                return PersistenceFailure("The selected game no longer exists.");
            }

            var updated = _games[index] with
            {
                Metadata = metadata,
                Artwork = artwork,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            };
            return await SaveReplacementAsync(index, updated, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public Task<LibraryMutationResult> SetManualCoverAsync(
        GameId gameId,
        ArtworkReference? cover,
        CancellationToken cancellationToken) =>
        SetManualArtworkAsync(gameId, ArtworkKind.Cover, cover, cancellationToken);

    public async Task<LibraryMutationResult> SetManualArtworkAsync(
        GameId gameId,
        ArtworkKind kind,
        ArtworkReference? artwork,
        CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = FindIndex(gameId);
            if (index < 0) return PersistenceFailure("The selected game no longer exists.");
            var existing = _games[index];
            if (!string.Equals(existing.Source, "Manual", StringComparison.OrdinalIgnoreCase))
                return PersistenceFailure("Custom artwork is available only for manually added games.");
            var placeholder = new ArtworkReference(
                ArtworkKind.Cover,
                "placeholder://cover",
                new MetadataProvenance("NovaLauncher", null, timeProvider.GetUtcNow(), IsManual: false),
                IsPlaceholder: true);
            var current = existing.Artwork;
            var currentCover = current?.Cover ?? placeholder;
            var currentHero = current?.Hero ?? placeholder with { Kind = ArtworkKind.Hero, Location = "placeholder://hero" };
            var currentLogo = current?.Logo ?? placeholder with { Kind = ArtworkKind.Logo, Location = "placeholder://logo" };
            var currentBackground = current?.Background ?? placeholder with { Kind = ArtworkKind.Background, Location = "placeholder://background" };
            var replacement = artwork ?? placeholder with { Kind = kind, Location = $"placeholder://{kind.ToString().ToLowerInvariant()}" };
            var updatedArtwork = kind switch
            {
                ArtworkKind.Cover => new GameArtwork(replacement, currentHero, currentLogo, currentBackground),
                ArtworkKind.Hero => new GameArtwork(currentCover, replacement, currentLogo, currentBackground),
                ArtworkKind.Logo => new GameArtwork(currentCover, currentHero, replacement, currentBackground),
                _ => new GameArtwork(currentCover, currentHero, currentLogo, replacement),
            };
            var updated = existing with { Artwork = updatedArtwork, UpdatedAtUtc = timeProvider.GetUtcNow() };
            return await SaveReplacementAsync(index, updated, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task<LibraryMutationResult> SetSaveDirectoryAsync(
        GameId gameId,
        string? saveDirectory,
        CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = FindIndex(gameId);
            if (index < 0) return PersistenceFailure("The selected game no longer exists.");
            var existing = _games[index];
            if (!string.Equals(existing.Source, "Manual", StringComparison.OrdinalIgnoreCase))
                return PersistenceFailure("Save synchronization is unavailable for Steam-imported games.");
            string? normalized = null;
            if (!string.IsNullOrWhiteSpace(saveDirectory))
            {
                if (!Path.IsPathFullyQualified(saveDirectory) || !Directory.Exists(saveDirectory))
                    return PersistenceFailure("Choose an existing absolute local save folder.");
                normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(saveDirectory));
                if (normalized.StartsWith("\\\\", StringComparison.Ordinal) || normalized.StartsWith("\\\\?\\", StringComparison.Ordinal))
                    return PersistenceFailure("Network and device paths cannot be used as save folders.");
            }

            return await SaveReplacementAsync(
                index,
                existing with { SaveDirectory = normalized, UpdatedAtUtc = timeProvider.GetUtcNow() },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task<LibraryMutationResult> SetSaveSyncIdAsync(
        GameId gameId,
        Guid? saveSyncId,
        string? saveSyncLabel,
        CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = FindIndex(gameId);
            if (index < 0) return PersistenceFailure("The selected game no longer exists.");
            var existing = _games[index];
            if (!string.Equals(existing.Source, "Manual", StringComparison.OrdinalIgnoreCase))
                return PersistenceFailure("Only manually added games can use a shared save identity.");
            if (saveSyncId == Guid.Empty) return PersistenceFailure("The shared save identity is invalid.");
            var normalizedLabel = string.IsNullOrWhiteSpace(saveSyncLabel) ? null : saveSyncLabel.Trim();
            if (normalizedLabel is { Length: > 200 }) return PersistenceFailure("The shared save label is too long.");
            if (saveSyncId is { } candidate && _games.Any(game => game.Id != gameId && game.SaveSyncId == candidate))
                return PersistenceFailure("That shared save identity is already linked to another local game.");
            return await SaveReplacementAsync(
                index,
                existing with { SaveSyncId = saveSyncId, SaveSyncLabel = normalizedLabel, UpdatedAtUtc = timeProvider.GetUtcNow() },
                cancellationToken).ConfigureAwait(false);
        }
        finally { _mutationGate.Release(); }
    }

    public async Task<LibraryMutationResult> SetSaveSyncPeersAsync(
        GameId gameId,
        IReadOnlyList<Guid>? peerDeviceIds,
        CancellationToken cancellationToken)
    {
        var normalized = peerDeviceIds?.Distinct().Order().ToArray();
        if (normalized is { Length: > SaveSyncSettings.MaximumTrustedPeers } || normalized?.Any(static id => id == Guid.Empty) == true)
            return PersistenceFailure("The save-sync destination list is invalid.");
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = FindIndex(gameId);
            if (index < 0) return PersistenceFailure("The selected game no longer exists.");
            var existing = _games[index];
            if (!string.Equals(existing.Source, "Manual", StringComparison.OrdinalIgnoreCase))
                return PersistenceFailure("Save-sync destinations are available only for manually added games.");
            return await SaveReplacementAsync(
                index,
                existing with { SaveSyncPeerIds = normalized is { Length: > 0 } ? normalized : null, UpdatedAtUtc = timeProvider.GetUtcNow() },
                cancellationToken).ConfigureAwait(false);
        }
        finally { _mutationGate.Release(); }
    }

    public async Task<LibraryMutationResult> SetManualMetadataAsync(
        GameId gameId,
        string? description,
        IReadOnlyList<string>? genres,
        IReadOnlyList<string>? developers,
        IReadOnlyList<string>? publishers,
        DateOnly? releaseDate,
        CancellationToken cancellationToken)
    {
        if (description is { Length: > 10_000 } ||
            !ValidManualList(genres) || !ValidManualList(developers) || !ValidManualList(publishers))
            return PersistenceFailure("Manual metadata exceeds the supported bounds.");
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = FindIndex(gameId);
            if (index < 0) return PersistenceFailure("The selected game no longer exists.");
            var existing = _games[index];
            if (!string.Equals(existing.Source, "Manual", StringComparison.OrdinalIgnoreCase))
                return PersistenceFailure("Manual metadata overrides are available only for manually added games.");
            var provenance = new MetadataProvenance("Manual", null, timeProvider.GetUtcNow(), IsManual: true);
            var updatedMetadata = existing.Metadata with
            {
                Description = string.IsNullOrWhiteSpace(description) ? null : new(description.Trim(), provenance),
                Genres = ValueOrNull(genres, provenance),
                Developers = ValueOrNull(developers, provenance),
                Publishers = ValueOrNull(publishers, provenance),
                ReleaseDate = releaseDate is null ? null : new(releaseDate.Value, provenance),
            };
            return await SaveReplacementAsync(index, existing with { Metadata = updatedMetadata, UpdatedAtUtc = timeProvider.GetUtcNow() }, cancellationToken).ConfigureAwait(false);
        }
        finally { _mutationGate.Release(); }
    }

    public async Task<LibraryMutationResult> ClearManualMetadataProtectionAsync(GameId gameId, CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = FindIndex(gameId);
            if (index < 0) return PersistenceFailure("The selected game no longer exists.");
            var existing = _games[index];
            var metadata = existing.Metadata;
            var lastKnown = new MetadataProvenance("NovaLauncherLastKnown", null, timeProvider.GetUtcNow(), IsManual: false);
            var updated = metadata with
            {
                Description = ReleaseManualProtection(metadata.Description, lastKnown),
                Genres = ReleaseManualProtection(metadata.Genres, lastKnown),
                Developers = ReleaseManualProtection(metadata.Developers, lastKnown),
                Publishers = ReleaseManualProtection(metadata.Publishers, lastKnown),
                ReleaseDate = ReleaseManualProtection(metadata.ReleaseDate, lastKnown),
                Rating = ReleaseManualProtection(metadata.Rating, lastKnown),
            };
            return await SaveReplacementAsync(index, existing with { Metadata = updated, UpdatedAtUtc = timeProvider.GetUtcNow() }, cancellationToken).ConfigureAwait(false);
        }
        finally { _mutationGate.Release(); }
    }

    public async Task<LibraryMutationResult> SetLinkedIdentityAsync(
        GameId gameId,
        LinkedGameIdentity? identity,
        CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = FindIndex(gameId);
            if (index < 0) return PersistenceFailure("The selected game no longer exists.");
            var existing = _games[index];
            if (!string.Equals(existing.Source, "Manual", StringComparison.OrdinalIgnoreCase))
                return PersistenceFailure("Provider linking is available only for manually added games.");
            var error = ValidateLinkedIdentity(identity);
            if (error is not null) return PersistenceFailure(error);
            if (identity is not null && _games.Any(game => game.Id != gameId &&
                string.Equals(game.LinkedIdentity?.ProviderId, identity.ProviderId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(game.LinkedIdentity?.ProviderItemId, identity.ProviderItemId, StringComparison.Ordinal)))
                return PersistenceFailure("That provider identity is already linked to another local game.");
            return await SaveReplacementAsync(
                index,
                existing with { LinkedIdentity = identity, UpdatedAtUtc = timeProvider.GetUtcNow() },
                cancellationToken).ConfigureAwait(false);
        }
        finally { _mutationGate.Release(); }
    }

    public async Task<LibraryMutationResult> ToggleFavoriteAsync(GameId gameId, CancellationToken cancellationToken)
    {
        var existing = _games.FirstOrDefault(game => game.Id == gameId);
        return existing is null
            ? PersistenceFailure("The selected game no longer exists.")
            : await MutateExistingAsync(
                gameId,
                existing with { IsFavorite = !existing.IsFavorite, UpdatedAtUtc = timeProvider.GetUtcNow() },
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<LibraryMutationResult> SetRunAsAdministratorAsync(
        GameId gameId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var existing = _games.FirstOrDefault(game => game.Id == gameId);
        if (existing is null) return PersistenceFailure("The selected game no longer exists.");
        if (enabled && existing.LaunchTarget.Kind != LaunchTargetKind.Executable)
            return PersistenceFailure("Administrator launch is available only for executable targets.");
        return await MutateExistingAsync(
            gameId,
            existing with { RunAsAdministrator = enabled, UpdatedAtUtc = timeProvider.GetUtcNow() },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<LibraryMutationResult> SaveLaunchActionAsync(
        GameId gameId,
        GameLaunchAction action,
        CancellationToken cancellationToken)
    {
        var existing = _games.FirstOrDefault(game => game.Id == gameId);
        if (existing is null) return PersistenceFailure("The selected game no longer exists.");
        var label = action.Label.Trim();
        if (action.Id == Guid.Empty || label.Length is < 1 or > 40 || label.Any(char.IsControl))
            return PersistenceFailure("The launch action name must contain 1–40 safe characters.");
        var validation = validator.Validate(new ManualGameDraft(
            existing.Name,
            existing.Platform,
            action.Target.Target,
            action.Target.Arguments,
            action.Target.WorkingDirectory,
            action.Target.Kind));
        if (!validation.IsValid)
            return new(LibraryMutationStatus.ValidationFailed, null, validation.Errors, "The launch action is invalid.");
        var actions = (existing.LaunchActions ?? [])
            .Where(candidate => candidate.Id != action.Id)
            .Append(action with { Label = label })
            .ToArray();
        if (actions.Length > 12) return PersistenceFailure("A game can have at most 12 additional launch actions.");
        return await MutateExistingAsync(gameId, existing with { LaunchActions = actions, UpdatedAtUtc = timeProvider.GetUtcNow() }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LibraryMutationResult> RemoveLaunchActionAsync(GameId gameId, Guid actionId, CancellationToken cancellationToken)
    {
        var existing = _games.FirstOrDefault(game => game.Id == gameId);
        if (existing is null) return PersistenceFailure("The selected game no longer exists.");
        var actions = (existing.LaunchActions ?? []).Where(action => action.Id != actionId).ToArray();
        if (actions.Length == (existing.LaunchActions?.Count ?? 0)) return PersistenceFailure("The launch action no longer exists.");
        return await MutateExistingAsync(gameId, existing with { LaunchActions = actions, UpdatedAtUtc = timeProvider.GetUtcNow() }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LibraryMutationResult> AddPlayTimeAsync(
        GameId gameId,
        TimeSpan elapsed,
        DateTimeOffset launchedAtUtc,
        CancellationToken cancellationToken)
    {
        if (elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromDays(7))
            return PersistenceFailure("The measured play session was outside the accepted range.");
        var existing = _games.FirstOrDefault(game => game.Id == gameId);
        if (existing is null) return PersistenceFailure("The launched game no longer exists.");
        return await MutateExistingAsync(
            gameId,
            existing with
            {
                TotalPlayTime = existing.TotalPlayTime + elapsed,
                LastPlayedAtUtc = launchedAtUtc,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            },
            cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<LibraryItem> Query(string? searchText, bool favoritesOnly, LibrarySort sort = LibrarySort.Name)
    {
        var tokens = (searchText ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var filtered = _games
            .Where(game => !favoritesOnly || game.IsFavorite)
            .Where(game => tokens.All(token => Matches(game, token)));
        IOrderedEnumerable<LibraryItem> ordered = sort switch
        {
            LibrarySort.Platform => filtered.OrderBy(static game => game.Platform, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static game => game.Name, StringComparer.OrdinalIgnoreCase),
            LibrarySort.RecentlyUpdated => filtered.OrderByDescending(static game => game.UpdatedAtUtc)
                .ThenBy(static game => game.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(static game => game.Name, StringComparer.OrdinalIgnoreCase),
        };
        return ordered.ThenBy(static game => game.Id.Value).ToArray();
    }

    public async Task<SteamImportPreview> CreateSteamImportPreviewAsync(
        SteamCatalogScanResult scan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scan);
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return CreateSteamImportPreviewCore(scan);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private SteamImportPreview CreateSteamImportPreviewCore(SteamCatalogScanResult scan)
    {
        var steamBySourceId = _games
            .Where(static game => string.Equals(game.Source, "Steam", StringComparison.OrdinalIgnoreCase) && game.SourceItemId is not null)
            .GroupBy(static game => game.SourceItemId!, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var steamById = _games
            .Where(static game => string.Equals(game.Source, "Steam", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(static game => game.Id);
        var items = scan.Games
            .OrderBy(static game => game.AppId)
            .Select(candidate =>
            {
                var sourceId = candidate.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var existing = steamBySourceId.GetValueOrDefault(sourceId) ??
                    steamById.GetValueOrDefault(GameId.FromSteamAppId(candidate.AppId));
                var change = existing is null
                    ? SteamImportChange.Add
                    : NeedsSteamUpdate(existing, candidate) ? SteamImportChange.Update : SteamImportChange.Unchanged;
                return new SteamImportPreviewItem(candidate.AppId, candidate.Name, candidate.LibraryRoot, change);
            })
            .ToArray();
        return new SteamImportPreview(
            Guid.NewGuid(),
            _revision,
            items,
            scan.Failures,
            scan.LibraryRoots,
            items.Count(static item => item.Change == SteamImportChange.Add),
            items.Count(static item => item.Change == SteamImportChange.Update),
            items.Count(static item => item.Change == SteamImportChange.Unchanged));
    }

    public async Task<SteamImportCommitResult> CommitSteamImportAsync(
        SteamImportPreview preview,
        IReadOnlyList<SteamGameCandidate> candidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(candidates);
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (preview.LibraryRevision != _revision)
            {
                return new SteamImportCommitResult(
                    SteamImportCommitStatus.PreviewStale,
                    0,
                    "The library changed after preview. Preview the import again.");
            }

            var now = timeProvider.GetUtcNow();
            var staged = _games.ToList();
            var indexBySourceId = staged
                .Select(static (game, index) => (game, index))
                .Where(static pair => string.Equals(pair.game.Source, "Steam", StringComparison.OrdinalIgnoreCase) && pair.game.SourceItemId is not null)
                .GroupBy(static pair => pair.game.SourceItemId!, StringComparer.Ordinal)
                .ToDictionary(static group => group.Key, static group => group.First().index, StringComparer.Ordinal);
            var indexById = staged
                .Select(static (game, index) => (game, index))
                .Where(static pair => string.Equals(pair.game.Source, "Steam", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(static pair => pair.game.Id, static pair => pair.index);
            foreach (var candidate in candidates.OrderBy(static item => item.AppId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceId = candidate.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var index = indexBySourceId.GetValueOrDefault(sourceId, -1);
                if (index < 0)
                {
                    index = indexById.GetValueOrDefault(GameId.FromSteamAppId(candidate.AppId), -1);
                }

                if (index < 0)
                {
                    staged.Add(CreateSteamGame(candidate, now));
                    indexBySourceId[sourceId] = staged.Count - 1;
                    indexById[GameId.FromSteamAppId(candidate.AppId)] = staged.Count - 1;
                    continue;
                }

                var existing = staged[index];
                if (NeedsSteamUpdate(existing, candidate))
                {
                    staged[index] = existing with
                    {
                        Name = string.Equals(existing.Name, existing.ImportedName, StringComparison.Ordinal)
                            ? candidate.Name
                            : existing.Name,
                        Source = "Steam",
                        SourceItemId = candidate.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ImportedName = candidate.Name,
                        UpdatedAtUtc = now,
                    };
                }
            }

            var save = await store.SaveAsync(
                new GamesDocument(GamesDocument.CurrentSchemaVersion, staged),
                cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved)
            {
                return new SteamImportCommitResult(SteamImportCommitStatus.PersistenceFailed, 0, save.Error);
            }

            _games = staged.ToArray();
            _revision++;
            return new SteamImportCommitResult(
                SteamImportCommitStatus.Saved,
                preview.Added + preview.Updated,
                null);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task<LibraryMutationResult> AddManualGameAsync(
        ManualGameDraft draft,
        CancellationToken cancellationToken)
    {
        var validation = validator.Validate(draft);
        if (!validation.IsValid)
        {
            return new LibraryMutationResult(
                LibraryMutationStatus.ValidationFailed,
                null,
                validation.Errors,
                null);
        }

        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = timeProvider.GetUtcNow();
            var item = new LibraryItem(
                GameId.New(),
                draft.Name.Trim(),
                draft.Platform.Trim(),
                "Manual",
                CreateLaunchTarget(draft),
                new GameMetadata(null, null, null, null, null, null),
                IsFavorite: false,
                now,
                now);
            var staged = _games.Append(item).ToArray();
            var save = await store.SaveAsync(
                new GamesDocument(GamesDocument.CurrentSchemaVersion, staged),
                cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved)
            {
                return new LibraryMutationResult(
                    LibraryMutationStatus.PersistenceFailed,
                    null,
                    new Dictionary<string, string>(),
                    save.Error);
            }

            _games = staged;
            _revision++;
            return new LibraryMutationResult(
                LibraryMutationStatus.Saved,
                item,
                new Dictionary<string, string>(),
                null);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private async Task<LibraryMutationResult> MutateExistingAsync(
        GameId gameId,
        LibraryItem? item,
        CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = FindIndex(gameId);
            if (index < 0)
            {
                return PersistenceFailure("The selected game no longer exists.");
            }

            var staged = _games.ToList();
            var changed = item ?? staged[index];
            if (item is null)
            {
                staged.RemoveAt(index);
            }
            else
            {
                staged[index] = item;
            }

            var save = await store.SaveAsync(
                new GamesDocument(GamesDocument.CurrentSchemaVersion, staged),
                cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved)
            {
                return PersistenceFailure(save.Error);
            }

            _games = staged.ToArray();
            _revision++;
            return Success(changed);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private async Task<LibraryMutationResult> SaveReplacementAsync(
        int index,
        LibraryItem updated,
        CancellationToken cancellationToken)
    {
        var staged = _games.ToArray();
        staged[index] = updated;
        var save = await store.SaveAsync(
            new GamesDocument(GamesDocument.CurrentSchemaVersion, staged),
            cancellationToken).ConfigureAwait(false);
        if (save.Status != DocumentSaveStatus.Saved)
        {
            return PersistenceFailure(save.Error);
        }

        _games = staged;
        _revision++;
        return Success(updated);
    }

    private int FindIndex(GameId id)
    {
        for (var index = 0; index < _games.Length; index++)
        {
            if (_games[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static LaunchTarget CreateLaunchTarget(ManualGameDraft draft) => new(
        draft.Target.Trim(),
        draft.Arguments.ToArray(),
        string.IsNullOrWhiteSpace(draft.WorkingDirectory) ? null : draft.WorkingDirectory.Trim(),
        draft.TargetKind);

    private static bool Matches(LibraryItem game, string token) =>
        game.Name.Contains(token, StringComparison.OrdinalIgnoreCase) ||
        game.Platform.Contains(token, StringComparison.OrdinalIgnoreCase) ||
        game.Source.Contains(token, StringComparison.OrdinalIgnoreCase) ||
        Contains(game.Metadata.Genres?.Value, token) ||
        Contains(game.Metadata.Developers?.Value, token) ||
        Contains(game.Metadata.Publishers?.Value, token);

    private static bool Contains(IReadOnlyList<string>? values, string token) =>
        values?.Any(value => value.Contains(token, StringComparison.OrdinalIgnoreCase)) == true;

    private static bool ValidManualList(IReadOnlyList<string>? values) =>
        values is null || values.Count <= 50 && values.All(static value => !string.IsNullOrWhiteSpace(value) && value.Length <= 200);

    private static MetadataValue<IReadOnlyList<string>>? ValueOrNull(IReadOnlyList<string>? values, MetadataProvenance provenance) =>
        values is null || values.Count == 0 ? null : new(values.Select(static value => value.Trim()).ToArray(), provenance);

    private static MetadataValue<T>? ReleaseManualProtection<T>(MetadataValue<T>? value, MetadataProvenance lastKnown) =>
        value?.Provenance.IsManual == true ? value with { Provenance = lastKnown } : value;

    private static string? ValidateDuplicateIdentity(LibraryItem survivor, LibraryItem duplicate)
    {
        if (!string.Equals(survivor.Source, duplicate.Source, StringComparison.OrdinalIgnoreCase))
            return "Records from different launch sources cannot be merged.";
        if (string.Equals(survivor.Source, "Steam", StringComparison.OrdinalIgnoreCase))
            return string.Equals(survivor.SourceItemId, duplicate.SourceItemId, StringComparison.Ordinal)
                ? null
                : "Steam records must have the same stable Steam identity.";
        return string.Equals(NormalizePath(survivor.LaunchTarget.Target), NormalizePath(duplicate.LaunchTarget.Target), StringComparison.OrdinalIgnoreCase)
            ? null
            : "Manual records must resolve to the same executable target.";
    }

    private static string? ValidateLinkedIdentity(LinkedGameIdentity? identity)
    {
        if (identity is null) return null;
        if (identity.ProviderId is not ("Steam" or "SteamGridDB") ||
            string.IsNullOrWhiteSpace(identity.ProviderItemId) || identity.ProviderItemId.Length > 128 ||
            string.IsNullOrWhiteSpace(identity.DisplayName) || identity.DisplayName.Length > 500 ||
            identity.ReleaseYear is < 1970 or > 2200)
            return "The linked provider identity is invalid.";
        if (identity.SteamAppId is { } appId &&
            (!uint.TryParse(appId, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed == 0))
            return "The confirmed Steam App ID is invalid.";
        return null;
    }

    private static bool CompatibleOptionalPath(string? first, string? second) =>
        first is null || second is null || string.Equals(NormalizePath(first), NormalizePath(second), StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) { return path.Trim(); }
    }

    private static DateTimeOffset? Latest(DateTimeOffset? first, DateTimeOffset? second) =>
        first is null ? second : second is null || first >= second ? first : second;

    private static GameMetadata PreferMetadata(GameMetadata first, GameMetadata second) => new(
        Prefer(first.Description, second.Description),
        Prefer(first.Genres, second.Genres),
        Prefer(first.Developers, second.Developers),
        Prefer(first.Publishers, second.Publishers),
        Prefer(first.ReleaseDate, second.ReleaseDate),
        Prefer(first.Rating, second.Rating));

    private static MetadataValue<T>? Prefer<T>(MetadataValue<T>? first, MetadataValue<T>? second) =>
        first?.Provenance.IsManual == true ? first : second?.Provenance.IsManual == true ? second : first ?? second;

    private static GameArtwork? PreferArtwork(GameArtwork? first, GameArtwork? second)
    {
        if (first is null) return second;
        if (second is null) return first;
        return new(
            PreferArtworkReference(first.Cover, second.Cover),
            PreferArtworkReference(first.Hero, second.Hero),
            PreferArtworkReference(first.Logo, second.Logo),
            PreferArtworkReference(first.Background, second.Background));
    }

    private static ArtworkReference PreferArtworkReference(ArtworkReference first, ArtworkReference second) =>
        first.Provenance.IsManual || !first.IsPlaceholder ? first : second;

    private static bool NeedsSteamUpdate(LibraryItem existing, SteamGameCandidate candidate) =>
        !string.Equals(existing.Source, "Steam", StringComparison.Ordinal) ||
        !string.Equals(existing.SourceItemId, candidate.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
        !string.Equals(existing.ImportedName, candidate.Name, StringComparison.Ordinal);

    private static LibraryItem CreateSteamGame(SteamGameCandidate candidate, DateTimeOffset now) => new(
        GameId.FromSteamAppId(candidate.AppId),
        candidate.Name,
        "Windows",
        "Steam",
        new LaunchTarget(
            $"steam://run/{candidate.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            [],
            null,
            LaunchTargetKind.Uri),
        new GameMetadata(null, null, null, null, null, null),
        false,
        now,
        now,
        candidate.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        candidate.Name);

    private static LibraryMutationResult ValidationFailure(DraftValidationResult validation) =>
        new(LibraryMutationStatus.ValidationFailed, null, validation.Errors, null);

    private static LibraryMutationResult PersistenceFailure(string? error) =>
        new(LibraryMutationStatus.PersistenceFailed, null, new Dictionary<string, string>(), error);

    private static LibraryMutationResult Success(LibraryItem item) =>
        new(LibraryMutationStatus.Saved, item, new Dictionary<string, string>(), null);
}
