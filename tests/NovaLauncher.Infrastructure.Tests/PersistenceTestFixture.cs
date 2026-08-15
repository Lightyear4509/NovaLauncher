using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Library;
using NovaLauncher.Infrastructure.Persistence;

namespace NovaLauncher.Infrastructure.Tests;

internal sealed class PersistenceTestFixture : IAsyncDisposable
{
    private PersistenceTestFixture(string root)
    {
        Root = root;
        FileSystem = new PhysicalAtomicFileSystem();
        Clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero));
        GamesStore = DocumentStoreFactory.CreateGamesStore(root, FileSystem, Clock);
        CollectionsStore = DocumentStoreFactory.CreateCollectionsStore(root, FileSystem, Clock);
        SettingsStore = DocumentStoreFactory.CreateSettingsStore(root, FileSystem, Clock);
    }

    public string Root { get; }

    public PhysicalAtomicFileSystem FileSystem { get; }

    public TimeProvider Clock { get; }

    public IDocumentStore<GamesDocument> GamesStore { get; }

    public IDocumentStore<CollectionsDocument> CollectionsStore { get; }

    public IDocumentStore<SettingsDocument> SettingsStore { get; }

    public static PersistenceTestFixture Create()
    {
        var root = Path.Combine(Path.GetTempPath(), $"NovaLauncher-Persistence-{Guid.NewGuid():N}");
        return new PersistenceTestFixture(root);
    }

    public static GamesDocument Games(string name) => new(
        GamesDocument.CurrentSchemaVersion,
        [
            new LibraryItem(
                GameId.New(),
                name,
                "Windows",
                "Manual",
                new LaunchTarget("C:\\Games\\Example.exe", [], "C:\\Games", LaunchTargetKind.Executable),
                new GameMetadata(null, null, null, null, null, null),
                IsFavorite: false,
                new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero))
        ]);

    public async ValueTask DisposeAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 9)
            {
                await Task.Delay(50);
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
