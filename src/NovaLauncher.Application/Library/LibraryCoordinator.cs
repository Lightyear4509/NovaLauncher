using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Steam;
using NovaLauncher.Domain.Library;

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

    public async Task<LibraryMutationResult> SetManualCoverAsync(
        GameId gameId,
        ArtworkReference? cover,
        CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = FindIndex(gameId);
            if (index < 0) return PersistenceFailure("The selected game no longer exists.");
            var existing = _games[index];
            if (!string.Equals(existing.Source, "Manual", StringComparison.OrdinalIgnoreCase))
                return PersistenceFailure("Custom covers are available only for manually added games.");
            var placeholder = new ArtworkReference(
                ArtworkKind.Cover,
                "placeholder://cover",
                new MetadataProvenance("NovaLauncher", null, timeProvider.GetUtcNow(), IsManual: false),
                IsPlaceholder: true);
            var current = existing.Artwork;
            var updatedArtwork = new GameArtwork(
                cover ?? placeholder,
                current?.Hero ?? placeholder with { Kind = ArtworkKind.Hero, Location = "placeholder://hero" },
                current?.Logo ?? placeholder with { Kind = ArtworkKind.Logo, Location = "placeholder://logo" },
                current?.Background ?? placeholder with { Kind = ArtworkKind.Background, Location = "placeholder://background" });
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
