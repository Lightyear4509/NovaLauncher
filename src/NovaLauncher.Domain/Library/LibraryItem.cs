namespace NovaLauncher.Domain.Library;

public sealed record GameLaunchSession(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    TimeSpan Duration);

public sealed record LibraryItem(
    GameId Id,
    string Name,
    string Platform,
    string Source,
    LaunchTarget LaunchTarget,
    GameMetadata Metadata,
    bool IsFavorite,
    DateTimeOffset AddedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? SourceItemId = null,
    string? ImportedName = null,
    GameArtwork? Artwork = null,
    TimeSpan TotalPlayTime = default,
    DateTimeOffset? LastPlayedAtUtc = null,
    bool RunAsAdministrator = false,
    string? SaveDirectory = null,
    Guid? SaveSyncId = null,
    string? SaveSyncLabel = null,
    LinkedGameIdentity? LinkedIdentity = null,
    IReadOnlyList<Guid>? SaveSyncPeerIds = null,
    IReadOnlyList<GameLaunchAction>? LaunchActions = null,
    string? Notes = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<GameLaunchSession>? LaunchSessions = null,
    IReadOnlyList<string>? ScreenshotFolders = null,
    Guid? ProfileId = null,
    bool HiddenFromSharedScreen = false);
