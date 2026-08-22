using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Profiles;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.Profiles;

namespace NovaLauncher.Application.Tests;

public sealed class ProfileCoordinatorTests
{
    [Fact]
    public async Task RegistryPersistsExplicitActiveProfileAndProtectsDefault()
    {
        var store = new ProfileStore();
        using var profiles = new ProfileCoordinator(store, TimeProvider.System);
        await profiles.LoadAsync(CancellationToken.None);
        Assert.Equal(LocalProfileDefaults.DefaultProfileId, profiles.ActiveProfileId);

        Assert.Equal(DocumentSaveStatus.Saved, (await profiles.CreateAsync("Couch", CancellationToken.None)).Status);
        var couch = profiles.Profiles.Single(profile => profile.Name == "Couch");
        Assert.Equal(DocumentSaveStatus.Saved, (await profiles.SwitchAsync(couch.Id, CancellationToken.None)).Status);

        Assert.Equal(couch.Id, profiles.ActiveProfileId);
        Assert.Equal(couch.Id, store.Document!.ActiveProfileId);
        Assert.Equal(DocumentSaveStatus.Failed, (await profiles.DeleteAsync(couch.Id, CancellationToken.None)).Status);
        Assert.Equal(DocumentSaveStatus.Failed, (await profiles.DeleteAsync(LocalProfileDefaults.DefaultProfileId, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task DiscoveryAndIgnoredPathsAreNormalizedAndProfileScoped()
    {
        var root = Directory.CreateTempSubdirectory("NovaLauncher-discovery-");
        try
        {
            var store = new ProfileStore();
            using var profiles = new ProfileCoordinator(store, TimeProvider.System);
            await profiles.LoadAsync(CancellationToken.None);
            Assert.Equal(DocumentSaveStatus.Saved, (await profiles.AddDiscoveryLocationAsync(root.FullName, CancellationToken.None)).Status);
            Assert.Equal(DocumentSaveStatus.Saved, (await profiles.AddIgnoredPathAsync(Path.Combine(root.FullName, "Ignore"), CancellationToken.None)).Status);
            Assert.Single(profiles.ActiveProfile.DiscoveryLocations!);
            Assert.Single(profiles.ActiveProfile.IgnoredPaths!);

            await profiles.CreateAsync("Other", CancellationToken.None);
            await profiles.SwitchAsync(profiles.Profiles.Single(profile => profile.Name == "Other").Id, CancellationToken.None);
            Assert.Null(profiles.ActiveProfile.DiscoveryLocations);
            Assert.Null(profiles.ActiveProfile.IgnoredPaths);
            Assert.Equal(DocumentSaveStatus.Failed, (await profiles.AddDiscoveryLocationAsync("\\\\server\\games", CancellationToken.None)).Status);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task LibraryAndCollectionsNeverExposeOrMutateAnotherProfile()
    {
        var gamesStore = new GamesStore();
        var collectionsStore = new CollectionsStore();
        using var library = new LibraryCoordinator(gamesStore, new ManualGameDraftValidator(), TimeProvider.System);
        using var collections = new CollectionCoordinator(collectionsStore, TimeProvider.System);
        await library.LoadAsync(CancellationToken.None);
        await collections.LoadAsync(CancellationToken.None);
        var defaultGame = (await library.AddManualGameAsync(Draft("Default game"), CancellationToken.None)).Item!;
        await collections.CreateAsync("Default shelf", CancellationToken.None);

        var secondProfile = Guid.NewGuid();
        library.SetActiveProfile(secondProfile);
        collections.SetActiveProfile(secondProfile);
        Assert.Empty(library.Games);
        Assert.Empty(collections.Collections);
        var secondGame = (await library.AddManualGameAsync(Draft("Second game"), CancellationToken.None)).Item!;
        await collections.CreateAsync("Second shelf", CancellationToken.None);

        library.SetActiveProfile(LocalProfileDefaults.DefaultProfileId);
        collections.SetActiveProfile(LocalProfileDefaults.DefaultProfileId);
        Assert.Equal(defaultGame.Id, Assert.Single(library.Games).Id);
        Assert.Equal("Default shelf", Assert.Single(collections.Collections).Name);
        await library.SetNotesAndTagsAsync(defaultGame.Id, "Changed in default", ["Default"], CancellationToken.None);

        library.SetActiveProfile(secondProfile);
        collections.SetActiveProfile(secondProfile);
        var isolated = Assert.Single(library.Games);
        Assert.Equal(secondGame.Id, isolated.Id);
        Assert.Null(isolated.Notes);
        Assert.Equal("Second shelf", Assert.Single(collections.Collections).Name);
        Assert.Equal(2, gamesStore.Document!.Games.Count);
        Assert.Equal(2, collectionsStore.Document!.Collections.Count);
    }

    private static ManualGameDraft Draft(string name) =>
        new(name, "Windows", $"C:\\Games\\{name}.exe", [], null, LaunchTargetKind.Executable);

    private sealed class ProfileStore : IDocumentStore<ProfilesDocument>
    {
        public ProfilesDocument? Document { get; private set; }
        public Task<DocumentLoadResult<ProfilesDocument>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(
            Document is null
                ? new DocumentLoadResult<ProfilesDocument>(DocumentLoadStatus.NotFound, null, null)
                : new DocumentLoadResult<ProfilesDocument>(DocumentLoadStatus.Loaded, Document, null));
        public Task<DocumentSaveResult> SaveAsync(ProfilesDocument document, CancellationToken cancellationToken)
        {
            Document = document;
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null));
        }
    }

    private sealed class GamesStore : IDocumentStore<GamesDocument>
    {
        public GamesDocument? Document { get; private set; }
        public Task<DocumentLoadResult<GamesDocument>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(
            new DocumentLoadResult<GamesDocument>(DocumentLoadStatus.NotFound, null, null));
        public Task<DocumentSaveResult> SaveAsync(GamesDocument document, CancellationToken cancellationToken)
        {
            Document = document;
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null));
        }
    }

    private sealed class CollectionsStore : IDocumentStore<CollectionsDocument>
    {
        public CollectionsDocument? Document { get; private set; }
        public Task<DocumentLoadResult<CollectionsDocument>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(
            new DocumentLoadResult<CollectionsDocument>(DocumentLoadStatus.NotFound, null, null));
        public Task<DocumentSaveResult> SaveAsync(CollectionsDocument document, CancellationToken cancellationToken)
        {
            Document = document;
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null));
        }
    }
}
