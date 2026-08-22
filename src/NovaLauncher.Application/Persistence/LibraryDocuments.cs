using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.Settings;
using NovaLauncher.Domain.Achievements;
using NovaLauncher.Domain.SaveSync;
using NovaLauncher.Domain.Profiles;

namespace NovaLauncher.Application.Persistence;

public sealed record GamesDocument(
    int SchemaVersion,
    IReadOnlyList<LibraryItem> Games) : IVersionedDocument
{
    public const int CurrentSchemaVersion = 3;

    public static GamesDocument Empty { get; } = new(CurrentSchemaVersion, []);
}

public sealed record CollectionsDocument(
    int SchemaVersion,
    IReadOnlyList<GameCollection> Collections) : IVersionedDocument
{
    public const int CurrentSchemaVersion = 1;

    public static CollectionsDocument Empty { get; } = new(CurrentSchemaVersion, []);
}

public sealed record SettingsDocument(
    int SchemaVersion,
    LauncherSettings Settings) : IVersionedDocument
{
    public const int CurrentSchemaVersion = 3;

    public static SettingsDocument Default { get; } = new(CurrentSchemaVersion, LauncherSettings.Default);
}

public sealed record AchievementCacheEntry(GameAchievements Achievements);

public sealed record AchievementsDocument(
    int SchemaVersion,
    string AccountFingerprint,
    IReadOnlyList<AchievementCacheEntry> Games) : IVersionedDocument
{
    public const int CurrentSchemaVersion = 1;

    public static AchievementsDocument Empty(string accountFingerprint) => new(CurrentSchemaVersion, accountFingerprint, []);
}

public sealed record SaveSyncDocument(
    int SchemaVersion,
    SaveSyncSettings Settings) : IVersionedDocument
{
    public const int CurrentSchemaVersion = 1;

    public static SaveSyncDocument CreateDefault() => new(CurrentSchemaVersion, SaveSyncSettings.CreateDefault());
}

public sealed record ProfilesDocument(
    int SchemaVersion,
    Guid ActiveProfileId,
    IReadOnlyList<LocalProfile> Profiles) : IVersionedDocument
{
    public const int CurrentSchemaVersion = 1;

    public static ProfilesDocument CreateDefault(DateTimeOffset now) => new(
        CurrentSchemaVersion,
        LocalProfileDefaults.DefaultProfileId,
        [new LocalProfile(LocalProfileDefaults.DefaultProfileId, "Default", now, now)]);
}
