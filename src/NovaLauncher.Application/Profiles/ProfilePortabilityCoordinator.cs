using System.Text.Json;
using System.Text.Json.Serialization;
using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Themes;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.Profiles;
using NovaLauncher.Domain.Settings;

namespace NovaLauncher.Application.Profiles;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProfileBackupDocument(
    int SchemaVersion,
    string ProfileName,
    IReadOnlyList<string>? DiscoveryLocations,
    IReadOnlyList<string>? IgnoredPaths,
    ProfileViewSettings ViewSettings,
    IReadOnlyList<ProfileGameTransfer> Games,
    IReadOnlyList<ProfileCollectionTransfer> Collections)
{
    public const int CurrentSchemaVersion = 1;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProfileGameTransfer(
    LibraryTransferEntry Entry,
    bool RunAsAdministrator,
    IReadOnlyList<GameLaunchAction>? LaunchActions,
    bool HiddenFromSharedScreen);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProfileCollectionTransfer(string Name, IReadOnlyList<Guid> GameExportIds);

public sealed record ProfileBackupPreviewItem(
    int Index,
    ProfileGameTransfer Game,
    bool IsValid,
    string Message);

public sealed record ProfileBackupPreview(
    ProfileBackupDocument Document,
    IReadOnlyList<ProfileBackupPreviewItem> Games,
    IReadOnlyList<string> ScopeNotes)
{
    public int ValidGameCount => Games.Count(static item => item.IsValid);
}

public sealed record ProfileBackupResult(bool Success, int GameCount, string Message);

public sealed class ProfilePortabilityCoordinator(
    IDocumentStore<GamesDocument> gamesStore,
    IDocumentStore<CollectionsDocument> collectionsStore,
    IDocumentStore<ProfilesDocument> profilesStore,
    IDocumentStore<SettingsDocument> settingsStore,
    ProfileCoordinator profiles,
    LibraryCoordinator library,
    CollectionCoordinator collections,
    IThemeService themes,
    ManualGameDraftValidator validator,
    TimeProvider timeProvider)
{
    public const long MaximumPayloadBytes = 32 * 1024 * 1024;
    public const int MaximumGames = 10_000;

    public async Task<ProfileBackupResult> ExportActiveProfileAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) return new(false, 0, "Choose a writable profile-backup destination.");
        var profile = profiles.ActiveProfile;
        if (library.Games.Count > MaximumGames) return new(false, 0, "The active profile exceeds the 10,000-game backup limit.");
        var games = library.Games.Select(game => new ProfileGameTransfer(
            new LibraryTransferEntry(
                game.Id.Value, game.Name, game.Platform, game.Source, game.LaunchTarget.Target, game.LaunchTarget.Kind,
                game.LaunchTarget.Arguments, game.LaunchTarget.WorkingDirectory, game.IsFavorite, game.SourceItemId,
                game.Notes, game.Tags),
            game.RunAsAdministrator,
            game.LaunchActions,
            game.HiddenFromSharedScreen)).ToArray();
        var profileView = new ProfileViewSettings(
            themes.LibraryPreferences.ViewMode,
            themes.LibraryPreferences.CardSize,
            themes.LibraryPreferences.Sort,
            themes.LibraryPreferences.SourceFilter,
            themes.LibraryPreferences.PlatformFilter,
            themes.LibraryPreferences.AvailabilityFilter,
            themes.LibraryPreferences.FavoritesOnly,
            string.Join(',', themes.HomePreferences.SectionOrder),
            string.Join(',', themes.HomePreferences.HiddenSections.Order(StringComparer.Ordinal)),
            themes.LibraryPreferences.SharedScreenMode);
        var document = new ProfileBackupDocument(
            ProfileBackupDocument.CurrentSchemaVersion,
            profile.Name,
            profile.DiscoveryLocations,
            profile.IgnoredPaths,
            profileView,
            games,
            collections.Collections.Select(collection => new ProfileCollectionTransfer(
                collection.Name,
                collection.GameIds.Select(static id => id.Value).ToArray())).ToArray());
        await using var buffer = new MemoryStream();
        await JsonSerializer.SerializeAsync(buffer, document, ProfilePortabilityJsonContext.Default.ProfileBackupDocument, cancellationToken).ConfigureAwait(false);
        if (buffer.Length > MaximumPayloadBytes) return new(false, 0, "The profile backup exceeds the 32 MiB limit.");
        buffer.Position = 0;
        await buffer.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        return new(true, games.Length,
            $"Exported profile-scoped library, collections, notes, tags, launch preferences, discovery rules, and layout for {profile.Name}. Device credentials and save-sync state were excluded.");
    }

    public async Task<(ProfileBackupPreview? Preview, string? Error)> PreviewAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead) return (null, "Choose a readable profile backup.");
        if (source.CanSeek && source.Length > MaximumPayloadBytes) return (null, "The profile backup exceeds the 32 MiB limit.");
        ProfileBackupDocument? document;
        try
        {
            document = await JsonSerializer.DeserializeAsync(source, ProfilePortabilityJsonContext.Default.ProfileBackupDocument, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return (null, "The profile backup uses an unsupported schema or contains unknown fields.");
        }
        if (document is null || document.SchemaVersion != ProfileBackupDocument.CurrentSchemaVersion ||
            document.Games is null || document.Collections is null || document.ViewSettings is null)
            return (null, "The profile backup schema is unsupported.");
        if (document.Games.Count > MaximumGames || document.Collections.Count > 1_000)
            return (null, "The profile backup exceeds game or collection limits.");
        if (string.IsNullOrWhiteSpace(document.ProfileName) || document.ProfileName.Length > 100 ||
            !ValidPaths(document.DiscoveryLocations, 50) || !ValidPaths(document.IgnoredPaths, 200))
            return (null, "The profile identity or discovery paths are invalid.");
        var ids = new HashSet<Guid>();
        var items = document.Games.Select((game, index) =>
        {
            var error = ValidateGame(game, ids);
            return new ProfileBackupPreviewItem(index, game, error is null, error ?? "Ready to restore after explicit review.");
        }).ToArray();
        if (document.Collections.Any(collection => string.IsNullOrWhiteSpace(collection.Name) || collection.Name.Length > 200 ||
            collection.GameExportIds is null || collection.GameExportIds.Count > MaximumGames ||
            collection.GameExportIds.Distinct().Count() != collection.GameExportIds.Count))
            return (null, "A profile collection is invalid.");
        return (new(document, items,
        [
            "Profile-scoped: library entries, collections, notes, tags, launch preferences, discovery rules, Library filters, and Home layout.",
            "Device-scoped and excluded: pairing credentials, trusted devices, save-sync transport state, update state, API keys, logs, and diagnostics.",
            "Missing or unsafe launch targets are rejected and remain unchecked."
        ]), null);
    }

    public async Task<ProfileBackupResult> CommitAsync(
        ProfileBackupPreview preview,
        IReadOnlyCollection<int> acceptedGameIndexes,
        bool replaceActiveProfile,
        string? importedProfileName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var accepted = acceptedGameIndexes.ToHashSet();
        if (accepted.Any(index => preview.Games.All(item => item.Index != index || !item.IsValid)))
            return new(false, 0, "The profile restore selection contains rejected or unknown games.");
        var gamesLoad = await gamesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var collectionsLoad = await collectionsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var profilesLoad = await profilesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var settingsLoad = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (gamesLoad.Document is null || collectionsLoad.Document is null || profilesLoad.Document is null || settingsLoad.Document is null)
            return new(false, 0, "Current profile documents could not be loaded safely before restore.");
        var oldGames = gamesLoad.Document;
        var oldCollections = collectionsLoad.Document;
        var oldProfiles = profilesLoad.Document;
        var oldSettings = settingsLoad.Document;
        var targetProfileId = replaceActiveProfile ? profiles.ActiveProfileId : Guid.NewGuid();
        var targetName = replaceActiveProfile ? profiles.ActiveProfile.Name : NormalizeImportedName(importedProfileName, preview.Document.ProfileName, oldProfiles);
        if (targetName is null) return new(false, 0, "Choose a unique imported profile name of 1–100 characters.");
        var now = timeProvider.GetUtcNow();
        var idMap = new Dictionary<Guid, GameId>();
        var importedGames = new List<LibraryItem>();
        foreach (var item in preview.Games.Where(item => accepted.Contains(item.Index)).OrderBy(static item => item.Index))
        {
            var transfer = item.Game;
            var id = transfer.Entry.Source == "Steam"
                ? GameId.FromSteamAppId(uint.Parse(transfer.Entry.SourceItemId!, System.Globalization.CultureInfo.InvariantCulture))
                : new GameId(transfer.Entry.ExportId);
            if (oldGames.Games.Any(game => (game.ProfileId ?? LocalProfileDefaults.DefaultProfileId) == targetProfileId && game.Id == id))
                id = GameId.New();
            idMap[transfer.Entry.ExportId] = id;
            importedGames.Add(new LibraryItem(
                id, transfer.Entry.Name.Trim(), transfer.Entry.Platform.Trim(), transfer.Entry.Source,
                new LaunchTarget(transfer.Entry.Target, transfer.Entry.Arguments, transfer.Entry.WorkingDirectory, transfer.Entry.TargetKind),
                new GameMetadata(null, null, null, null, null, null), transfer.Entry.IsFavorite, now, now,
                transfer.Entry.SourceItemId, transfer.Entry.Name, RunAsAdministrator: transfer.RunAsAdministrator,
                LaunchActions: transfer.LaunchActions, Notes: NormalizeNotes(transfer.Entry.Notes),
                Tags: NormalizeTags(transfer.Entry.Tags), ProfileId: targetProfileId,
                HiddenFromSharedScreen: transfer.HiddenFromSharedScreen));
        }
        var importedCollections = preview.Document.Collections.Select(collection => new GameCollection(
            GameCollectionId.New(), collection.Name.Trim(),
            collection.GameExportIds.Where(idMap.ContainsKey).Select(exportId => idMap[exportId]).Distinct().ToArray(),
            now, now, targetProfileId)).ToArray();
        var newGames = oldGames.Games.Where(game => !replaceActiveProfile || (game.ProfileId ?? LocalProfileDefaults.DefaultProfileId) != targetProfileId)
            .Concat(importedGames).ToArray();
        var newCollections = oldCollections.Collections.Where(collection => !replaceActiveProfile || (collection.ProfileId ?? LocalProfileDefaults.DefaultProfileId) != targetProfileId)
            .Concat(importedCollections).ToArray();
        var profile = new LocalProfile(targetProfileId, targetName, replaceActiveProfile ? profiles.ActiveProfile.CreatedAtUtc : now, now,
            preview.Document.DiscoveryLocations, preview.Document.IgnoredPaths);
        var newProfileList = oldProfiles.Profiles.Where(item => item.Id != targetProfileId).Append(profile).ToArray();
        var newProfiles = oldProfiles with { ActiveProfileId = targetProfileId, Profiles = newProfileList };
        var views = new Dictionary<string, ProfileViewSettings>(oldSettings.Settings.ProfileViews ?? new Dictionary<string, ProfileViewSettings>(), StringComparer.Ordinal)
        {
            [targetProfileId.ToString("N")] = preview.Document.ViewSettings,
        };
        var newSettings = oldSettings with { Settings = oldSettings.Settings with { ProfileViews = views } };

        var saves = new List<(string Name, Func<Task<DocumentSaveResult>> Save, Func<Task<DocumentSaveResult>> Rollback)>
        {
            ("games", () => gamesStore.SaveAsync(new GamesDocument(GamesDocument.CurrentSchemaVersion, newGames), cancellationToken),
                () => gamesStore.SaveAsync(oldGames, CancellationToken.None)),
            ("collections", () => collectionsStore.SaveAsync(new CollectionsDocument(CollectionsDocument.CurrentSchemaVersion, newCollections), cancellationToken),
                () => collectionsStore.SaveAsync(oldCollections, CancellationToken.None)),
            ("profiles", () => profilesStore.SaveAsync(newProfiles, cancellationToken),
                () => profilesStore.SaveAsync(oldProfiles, CancellationToken.None)),
            ("settings", () => settingsStore.SaveAsync(newSettings, cancellationToken),
                () => settingsStore.SaveAsync(oldSettings, CancellationToken.None)),
        };
        var completed = new List<(string Name, Func<Task<DocumentSaveResult>> Rollback)>();
        foreach (var operation in saves)
        {
            var save = await operation.Save().ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved)
            {
                var rollbackFailures = new List<string>();
                foreach (var prior in completed.AsEnumerable().Reverse())
                    if ((await prior.Rollback().ConfigureAwait(false)).Status != DocumentSaveStatus.Saved) rollbackFailures.Add(prior.Name);
                return new(false, 0, rollbackFailures.Count == 0
                    ? $"Profile restore failed while saving {operation.Name}; earlier documents were rolled back."
                    : $"Profile restore failed while saving {operation.Name}; rollback also failed for {string.Join(", ", rollbackFailures)}.");
            }
            completed.Add((operation.Name, operation.Rollback));
        }
        return new(true, importedGames.Count,
            $"Restored {importedGames.Count} reviewed games into profile {targetName}. Device-scoped credentials and save-sync state were not changed.");
    }

    private string? ValidateGame(ProfileGameTransfer game, HashSet<Guid> ids)
    {
        var entry = game.Entry;
        if (entry is null || entry.ExportId == Guid.Empty || !ids.Add(entry.ExportId) ||
            entry.Name is not { Length: > 0 and <= 500 } || entry.Platform is not { Length: > 0 and <= 100 } ||
            entry.Source is not ("Manual" or "Steam") || entry.Arguments is null ||
            entry.Notes is { Length: > 50_000 } || entry.Tags is { Count: > 100 } ||
            entry.Tags?.Any(static tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 100) == true)
            return "The game contains duplicate, missing, or oversized fields.";
        var validation = validator.Validate(new ManualGameDraft(
            entry.Name, entry.Platform, entry.Target, entry.Arguments, entry.WorkingDirectory, entry.TargetKind));
        if (!validation.IsValid) return string.Join(" ", validation.Errors.Values);
        if (entry.TargetKind == LaunchTargetKind.Executable && !File.Exists(entry.Target))
            return "The executable is missing on this device and cannot be restored as launchable.";
        if (entry.Source == "Steam" && (!uint.TryParse(entry.SourceItemId, out var appId) || appId == 0 ||
            entry.TargetKind != LaunchTargetKind.Uri || entry.Target != $"steam://run/{appId}"))
            return "The Steam identity and URI are inconsistent.";
        if (game.RunAsAdministrator && entry.TargetKind != LaunchTargetKind.Executable)
            return "Administrator launch is valid only for executable targets.";
        if (game.LaunchActions is { Count: > 12 } || game.LaunchActions?.Any(action =>
            action.Id == Guid.Empty || string.IsNullOrWhiteSpace(action.Label) || action.Label.Length > 40 ||
            !validator.Validate(new ManualGameDraft(entry.Name, entry.Platform, action.Target.Target,
                action.Target.Arguments, action.Target.WorkingDirectory, action.Target.Kind)).IsValid) == true)
            return "An additional launch action is invalid.";
        return null;
    }

    private static bool ValidPaths(IReadOnlyList<string>? paths, int maximum) =>
        paths is null || paths.Count <= maximum && paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == paths.Count &&
        paths.All(static path => path.Length <= 1_024 && Path.IsPathFullyQualified(path) &&
            !path.StartsWith("\\\\", StringComparison.Ordinal) && !path.StartsWith("\\\\?\\", StringComparison.Ordinal));

    private static string? NormalizeImportedName(string? requested, string fallback, ProfilesDocument profiles)
    {
        var name = string.IsNullOrWhiteSpace(requested) ? $"{fallback} (Imported)" : requested.Trim();
        return name.Length is > 0 and <= 100 && profiles.Profiles.All(profile => !string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
            ? name : null;
    }

    private static string? NormalizeNotes(string? notes) => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    private static string[]? NormalizeTags(IReadOnlyList<string>? tags)
    {
        var normalized = (tags ?? []).Select(static tag => tag.Trim()).Where(static tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return normalized.Length == 0 ? null : normalized;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(ProfileBackupDocument))]
internal sealed partial class ProfilePortabilityJsonContext : JsonSerializerContext;
