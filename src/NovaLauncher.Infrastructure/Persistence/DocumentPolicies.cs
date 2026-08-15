using NovaLauncher.Application.Persistence;

namespace NovaLauncher.Infrastructure.Persistence;

public sealed class GamesDocumentPolicy : IDocumentPolicy<GamesDocument>
{
    public string FileName => "games.json";

    public int CurrentSchemaVersion => GamesDocument.CurrentSchemaVersion;

    public string? Validate(GamesDocument document)
    {
        if (document.Games is null)
        {
            return "The games collection is required.";
        }

        var ids = new HashSet<Guid>();
        var saveSyncIds = new HashSet<Guid>();
        foreach (var game in document.Games)
        {
            if (game.Id.Value == Guid.Empty)
            {
                return "Every game requires a non-empty stable ID.";
            }

            if (!ids.Add(game.Id.Value))
            {
                return $"Duplicate game ID '{game.Id}'.";
            }

            if (game.SaveSyncId is { } saveSyncId && (saveSyncId == Guid.Empty || !saveSyncIds.Add(saveSyncId)))
            {
                return "Shared save identities must be non-empty and unique within the local library.";
            }
            if (game.SaveSyncLabel is { Length: > 200 }) return "A shared save label exceeds 200 characters.";

            if (string.IsNullOrWhiteSpace(game.Name) || game.Name.Length > 500)
            {
                return "Every game requires a name of 1–500 characters.";
            }

            if (string.IsNullOrWhiteSpace(game.Platform) || string.IsNullOrWhiteSpace(game.Source))
            {
                return "Every game requires platform and source values.";
            }

            if (game.LaunchTarget is null || string.IsNullOrWhiteSpace(game.LaunchTarget.Target))
            {
                return "Every game requires a launch target.";
            }

            if (game.UpdatedAtUtc < game.AddedAtUtc)
            {
                return "A game's update time cannot precede its add time.";
            }

            if (game.TotalPlayTime < TimeSpan.Zero || game.TotalPlayTime > TimeSpan.FromDays(36500))
            {
                return "A game's total playtime is outside the accepted range.";
            }

            if (game.RunAsAdministrator && game.LaunchTarget.Kind != NovaLauncher.Domain.Library.LaunchTargetKind.Executable)
            {
                return "Administrator launch is allowed only for executable targets.";
            }

            if (game.SourceItemId is { Length: > 128 } || game.ImportedName is { Length: > 500 })
            {
                return "Game source identity or imported name exceeds its length limit.";
            }

            if (string.Equals(game.Source, "Steam", StringComparison.OrdinalIgnoreCase) &&
                game.SourceItemId is { } steamId &&
                (!uint.TryParse(steamId, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var appId) ||
                 appId == 0 ||
                 game.Id != NovaLauncher.Domain.Library.GameId.FromSteamAppId(appId)))
            {
                return "Steam source identity is invalid or inconsistent with the stable game ID.";
            }

            if (game.Metadata is null)
            {
                return "Every game requires a metadata object.";
            }

            var metadataError = ValidateMetadata(game.Metadata);
            if (metadataError is not null)
            {
                return metadataError;
            }

            if (game.Artwork is { } artwork)
            {
                var references = new[] { artwork.Cover, artwork.Hero, artwork.Logo, artwork.Background };
                if (references.Any(static reference => reference is null))
                {
                    return "Artwork requires cover, hero, logo, and background references.";
                }

                foreach (var reference in references)
                {
                    if (reference.Location.Length > 2048 ||
                        !Uri.TryCreate(reference.Location, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != "placeholder" && uri.Scheme != "managed-artwork") ||
                        (uri.Scheme == "managed-artwork" && !IsValidManagedArtworkUri(uri)))
                    {
                        return "Artwork locations must be bounded HTTPS, managed-artwork, or NovaLauncher placeholder URIs.";
                    }
                }
            }
        }

        return null;
    }

    private static bool IsValidManagedArtworkUri(Uri uri)
    {
        if (!string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            uri.OriginalString.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        var fileName = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        var extension = Path.GetExtension(fileName);
        return fileName.Length is > 0 and <= 180 && fileName == Path.GetFileName(fileName) &&
            extension is ".png" or ".jpg" or ".webp";
    }

    private static string? ValidateMetadata(NovaLauncher.Domain.Library.GameMetadata metadata)
    {
        if (metadata.Description?.Value.Length > 10_000 || metadata.Rating?.Value is < 0 or > 100)
        {
            return "Metadata description or rating exceeds its bounds.";
        }

        foreach (var values in new[] { metadata.Genres?.Value, metadata.Developers?.Value, metadata.Publishers?.Value })
        {
            if (values is { Count: > 100 } || values?.Any(static value => string.IsNullOrWhiteSpace(value) || value.Length > 256) == true)
            {
                return "Metadata list values exceed their count or length bounds.";
            }
        }

        return null;
    }
}

public sealed class AchievementsDocumentPolicy : IDocumentPolicy<AchievementsDocument>
{
    public string FileName => "achievements.json";

    public int CurrentSchemaVersion => AchievementsDocument.CurrentSchemaVersion;

    public string? Validate(AchievementsDocument document)
    {
        if (document.AccountFingerprint.Length is < 16 or > 128 || document.Games is null || document.Games.Count > 20_000)
            return "Achievement cache identity or game count is invalid.";
        var games = new HashSet<Guid>();
        foreach (var entry in document.Games)
        {
            var game = entry.Achievements;
            if (!games.Add(game.GameId.Value) || game.Items is null || game.Items.Count > 2_000 || game.Provider.Length is < 1 or > 64)
                return "Achievement cache contains invalid or duplicate games.";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in game.Items)
            {
                if (!ids.Add(item.Id.ToString()) || item.Name.Length is < 1 or > 500 || item.Description.Length > 2_000 ||
                    item.Id.Value.Length is < 1 or > 256 || item.Provider.Length is < 1 or > 64 ||
                    (!item.IsUnlocked && item.UnlockedAtUtc is not null))
                    return "Achievement cache contains invalid achievement data.";
            }
        }

        return null;
    }
}

public sealed class CollectionsDocumentPolicy : IDocumentPolicy<CollectionsDocument>
{
    public string FileName => "collections.json";

    public int CurrentSchemaVersion => CollectionsDocument.CurrentSchemaVersion;

    public string? Validate(CollectionsDocument document)
    {
        if (document.Collections is null)
        {
            return "The collections collection is required.";
        }

        var ids = new HashSet<Guid>();
        foreach (var collection in document.Collections)
        {
            if (!ids.Add(collection.Id.Value))
            {
                return $"Duplicate collection ID '{collection.Id}'.";
            }

            if (string.IsNullOrWhiteSpace(collection.Name) || collection.Name.Length > 200)
            {
                return "Every collection requires a name of 1–200 characters.";
            }

            if (collection.GameIds is null || collection.GameIds.Distinct().Count() != collection.GameIds.Count)
            {
                return "Collection membership must contain unique game IDs.";
            }
        }

        return null;
    }
}

public sealed class SettingsDocumentPolicy : IDocumentPolicy<SettingsDocument>
{
    public string FileName => "settings.json";

    public int CurrentSchemaVersion => SettingsDocument.CurrentSchemaVersion;

    public string? Validate(SettingsDocument document)
    {
        if (document.Settings is null || string.IsNullOrWhiteSpace(document.Settings.ThemeId))
        {
            return "Settings require a theme ID.";
        }

        if (document.Settings.TailscalePeerAddress is { Length: > 0 } peer &&
            !NovaLauncher.Application.SaveSync.TailscalePeerValidator.TryNormalize(peer, out _, out _))
        {
            return "The configured Tailscale peer address is invalid or outside the Tailscale address ranges.";
        }

        return document.Settings.ThemeId.Length > 100
            ? "The theme ID cannot exceed 100 characters."
            : null;
    }
}

public sealed class SaveSyncDocumentPolicy : IDocumentPolicy<SaveSyncDocument>
{
    public string FileName => "save-sync.json";

    public int CurrentSchemaVersion => SaveSyncDocument.CurrentSchemaVersion;

    public string? Validate(SaveSyncDocument document)
    {
        var settings = document.Settings;
        if (settings is null || settings.DeviceId == Guid.Empty) return "Save sync requires a stable device ID.";
        if (string.IsNullOrWhiteSpace(settings.DeviceName) || settings.DeviceName.Length > 100) return "The device name is invalid.";
        if (settings.Port is < 1024 or > 65535) return "The save-sync port is outside the permitted range.";
        if ((settings.PendingInvitationId is null) != (settings.PendingInvitationExpiresAtUtc is null))
            return "A pending save-sync invitation requires both identity and expiration.";
        var hasPendingCode = settings.PendingCodeSalt is not null || settings.PendingCodeHash is not null;
        if ((settings.PendingInvitationId is not null) != hasPendingCode ||
            (settings.PendingCodeSalt is null) != (settings.PendingCodeHash is null))
            return "A pending save-sync invitation requires a complete code verifier.";
        if (settings.PendingCodeFailedAttempts is < 0 or > 3 ||
            (settings.PendingInvitationId is null && settings.PendingCodeFailedAttempts != 0))
            return "The invitation failure counter is invalid.";
        if (hasPendingCode)
        {
            try
            {
                if (Convert.FromBase64String(settings.PendingCodeSalt!).Length != 16 ||
                    Convert.FromBase64String(settings.PendingCodeHash!).Length != 32)
                    return "The invitation code verifier is invalid.";
            }
            catch (FormatException) { return "The invitation code verifier is invalid."; }
        }
        if (settings.PeerAddress is { Length: > 0 } peer &&
            !NovaLauncher.Application.SaveSync.TailscalePeerValidator.TryNormalize(peer, out _, out _))
            return "The save-sync peer is not a Tailscale address.";
        if (settings.Games.Count > 10_000 || settings.Games.Select(static game => game.GameId).Distinct().Count() != settings.Games.Count)
            return "Save-sync game state is excessive or duplicated.";
        foreach (var game in settings.Games)
        {
            if (game.LastObservedFiles.Count > 20_000) return "A save manifest contains too many files.";
            if (game.LastObservedFiles.Any(static file => file.Length < 0 || file.Length > NovaLauncher.Application.SaveSync.SaveSyncCoordinator.MaximumFileBytes ||
                !NovaLauncher.Application.SaveSync.SaveSyncCoordinator.IsSafeRelativePath(file.RelativePath))) return "A save manifest contains an unsafe file.";
        }
        return null;
    }
}
