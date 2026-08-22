using System.Text;
using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Profiles;
using NovaLauncher.Application.Themes;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.Profiles;
using NovaLauncher.Domain.Settings;

namespace NovaLauncher.Application.Tests;

public sealed class ProfilePortabilityTests
{
    [Fact]
    public async Task BackupLabelsScopeExcludesDeviceStateAndImportsReviewedProfile()
    {
        var root = Directory.CreateTempSubdirectory("NovaLauncher-profile-backup-");
        try
        {
            var executable = Path.Combine(root.FullName, "Game.exe");
            await File.WriteAllBytesAsync(executable, []);
            using var fixture = await Fixture.CreateAsync(executable);
            await using var output = new MemoryStream();

            var exported = await fixture.Portability.ExportActiveProfileAsync(output, CancellationToken.None);
            var json = Encoding.UTF8.GetString(output.ToArray());

            Assert.True(exported.Success);
            Assert.Contains("Profile note", json, StringComparison.Ordinal);
            Assert.DoesNotContain("saveSync", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credential", json, StringComparison.OrdinalIgnoreCase);
            output.Position = 0;
            var (preview, error) = await fixture.Portability.PreviewAsync(output, CancellationToken.None);
            Assert.Null(error);
            Assert.Contains(preview!.ScopeNotes, note => note.Contains("Device-scoped", StringComparison.Ordinal));
            var valid = Assert.Single(preview.Games);
            Assert.True(valid.IsValid);

            var committed = await fixture.Portability.CommitAsync(preview, [valid.Index], false, "Imported", CancellationToken.None);

            Assert.True(committed.Success);
            Assert.Equal(2, fixture.ProfilesStore.Value.Profiles.Count);
            var importedId = fixture.ProfilesStore.Value.ActiveProfileId;
            Assert.Contains(fixture.GamesStore.Value.Games, game => game.ProfileId == importedId && game.Notes == "Profile note");
            Assert.Contains(fixture.CollectionsStore.Value.Collections, collection => collection.ProfileId == importedId && collection.Name == "Shelf");
            Assert.True(fixture.SettingsStore.Value.Settings.ProfileViews!.ContainsKey(importedId.ToString("N")));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task FailedMultiDocumentRestoreRollsBackEarlierDocuments()
    {
        var root = Directory.CreateTempSubdirectory("NovaLauncher-profile-rollback-");
        try
        {
            var executable = Path.Combine(root.FullName, "Game.exe");
            await File.WriteAllBytesAsync(executable, []);
            using var fixture = await Fixture.CreateAsync(executable);
            await using var output = new MemoryStream();
            await fixture.Portability.ExportActiveProfileAsync(output, CancellationToken.None);
            output.Position = 0;
            var (preview, _) = await fixture.Portability.PreviewAsync(output, CancellationToken.None);
            var oldGames = fixture.GamesStore.Value;
            var oldCollections = fixture.CollectionsStore.Value;
            var oldProfiles = fixture.ProfilesStore.Value;
            fixture.SettingsStore.FailNextSave = true;

            var result = await fixture.Portability.CommitAsync(
                preview!, preview!.Games.Select(static item => item.Index).ToArray(), false, "Rollback", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("rolled back", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(oldGames, fixture.GamesStore.Value);
            Assert.Equal(oldCollections, fixture.CollectionsStore.Value);
            Assert.Equal(oldProfiles, fixture.ProfilesStore.Value);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private Fixture(
            MemoryStore<GamesDocument> gamesStore,
            MemoryStore<CollectionsDocument> collectionsStore,
            MemoryStore<ProfilesDocument> profilesStore,
            MemoryStore<SettingsDocument> settingsStore,
            LibraryCoordinator library,
            CollectionCoordinator collections,
            ProfileCoordinator profiles,
            ThemeService themes,
            ProfilePortabilityCoordinator portability)
        {
            GamesStore = gamesStore;
            CollectionsStore = collectionsStore;
            ProfilesStore = profilesStore;
            SettingsStore = settingsStore;
            Library = library;
            Collections = collections;
            Profiles = profiles;
            Themes = themes;
            Portability = portability;
        }

        public MemoryStore<GamesDocument> GamesStore { get; }
        public MemoryStore<CollectionsDocument> CollectionsStore { get; }
        public MemoryStore<ProfilesDocument> ProfilesStore { get; }
        public MemoryStore<SettingsDocument> SettingsStore { get; }
        public LibraryCoordinator Library { get; }
        public CollectionCoordinator Collections { get; }
        public ProfileCoordinator Profiles { get; }
        public ThemeService Themes { get; }
        public ProfilePortabilityCoordinator Portability { get; }

        public static async Task<Fixture> CreateAsync(string executable)
        {
            var now = DateTimeOffset.UtcNow;
            var gamesStore = new MemoryStore<GamesDocument>(GamesDocument.Empty);
            var collectionsStore = new MemoryStore<CollectionsDocument>(CollectionsDocument.Empty);
            var profilesStore = new MemoryStore<ProfilesDocument>(ProfilesDocument.CreateDefault(now));
            var settingsStore = new MemoryStore<SettingsDocument>(SettingsDocument.Default);
            var validator = new ManualGameDraftValidator();
            var library = new LibraryCoordinator(gamesStore, validator, TimeProvider.System);
            var collections = new CollectionCoordinator(collectionsStore, TimeProvider.System);
            var profiles = new ProfileCoordinator(profilesStore, TimeProvider.System);
            var themes = new ThemeService(new Host(), settingsStore);
            await profiles.LoadAsync(CancellationToken.None);
            await library.LoadAsync(CancellationToken.None);
            await collections.LoadAsync(CancellationToken.None);
            await themes.InitializeAsync(CancellationToken.None);
            library.SetActiveProfile(profiles.ActiveProfileId);
            collections.SetActiveProfile(profiles.ActiveProfileId);
            themes.SetActiveProfile(profiles.ActiveProfileId);
            var game = (await library.AddManualGameAsync(
                new ManualGameDraft("Game", "Windows", executable, [], Path.GetDirectoryName(executable), LaunchTargetKind.Executable),
                CancellationToken.None)).Item!;
            await library.SetNotesAndTagsAsync(game.Id, "Profile note", ["Local"], CancellationToken.None);
            await collections.CreateAsync("Shelf", CancellationToken.None);
            await collections.SetMembershipAsync(Assert.Single(collections.Collections).Id, game.Id, true, CancellationToken.None);
            await themes.SaveLibraryPreferencesAsync(
                new LibraryViewPreferences("List", "Large", "Name", "Manual", "Windows", "Available", false),
                CancellationToken.None);
            var portability = new ProfilePortabilityCoordinator(
                gamesStore, collectionsStore, profilesStore, settingsStore, profiles, library, collections, themes, validator, TimeProvider.System);
            return new(gamesStore, collectionsStore, profilesStore, settingsStore, library, collections, profiles, themes, portability);
        }

        public void Dispose()
        {
            Themes.Dispose();
            Profiles.Dispose();
            Collections.Dispose();
            Library.Dispose();
        }
    }

    private sealed class MemoryStore<T>(T initial) : IDocumentStore<T> where T : class, IVersionedDocument
    {
        public T Value { get; private set; } = initial;
        public bool FailNextSave { get; set; }
        public Task<DocumentLoadResult<T>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentLoadResult<T>(DocumentLoadStatus.Loaded, Value, null));
        public Task<DocumentSaveResult> SaveAsync(T document, CancellationToken cancellationToken)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Failed, "Injected failure"));
            }
            Value = document;
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null));
        }
    }

    private sealed class Host : IThemeHost
    {
        public string CurrentThemeId { get; private set; } = "nova-dark";
        public bool ReduceMotion { get; private set; }
        public bool Apply(string themeId) { CurrentThemeId = themeId; return true; }
        public bool ApplyMotionPreference(bool reduceMotion) { ReduceMotion = reduceMotion; return true; }
    }
}
