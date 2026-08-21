using NovaLauncher.Application.Persistence;

namespace NovaLauncher.Infrastructure.Persistence;

public static class DocumentStoreFactory
{
    public static IDocumentStore<GamesDocument> CreateGamesStore(
        string dataRoot,
        IAtomicFileSystem fileSystem,
        TimeProvider timeProvider) =>
        new AtomicJsonDocumentStore<GamesDocument>(
            dataRoot,
            fileSystem,
            new GamesDocumentPolicy(),
            PersistenceJsonContext.Default.GamesDocument,
            timeProvider);

    public static IDocumentStore<CollectionsDocument> CreateCollectionsStore(
        string dataRoot,
        IAtomicFileSystem fileSystem,
        TimeProvider timeProvider) =>
        new AtomicJsonDocumentStore<CollectionsDocument>(
            dataRoot,
            fileSystem,
            new CollectionsDocumentPolicy(),
            PersistenceJsonContext.Default.CollectionsDocument,
            timeProvider);

    public static IDocumentStore<SettingsDocument> CreateSettingsStore(
        string dataRoot,
        IAtomicFileSystem fileSystem,
        TimeProvider timeProvider) =>
        new AtomicJsonDocumentStore<SettingsDocument>(
            dataRoot,
            fileSystem,
            new SettingsDocumentPolicy(),
            PersistenceJsonContext.Default.SettingsDocument,
            timeProvider,
            new SettingsDocumentMigrator());

    public static IDocumentStore<AchievementsDocument> CreateAchievementsStore(
        string dataRoot,
        IAtomicFileSystem fileSystem,
        TimeProvider timeProvider) =>
        new AtomicJsonDocumentStore<AchievementsDocument>(
            dataRoot,
            fileSystem,
            new AchievementsDocumentPolicy(),
            PersistenceJsonContext.Default.AchievementsDocument,
            timeProvider);

    public static IDocumentStore<SaveSyncDocument> CreateSaveSyncStore(
        string dataRoot,
        IAtomicFileSystem fileSystem,
        TimeProvider timeProvider) =>
        new AtomicJsonDocumentStore<SaveSyncDocument>(
            dataRoot,
            fileSystem,
            new SaveSyncDocumentPolicy(),
            PersistenceJsonContext.Default.SaveSyncDocument,
            timeProvider);
}
