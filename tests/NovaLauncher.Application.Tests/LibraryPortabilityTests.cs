using System.Text;
using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Tests;

public sealed class LibraryPortabilityTests
{
    [Fact]
    public async Task ExportIsSelectiveAndExcludesDeviceScopedAndHistoricalData()
    {
        var store = new MemoryStore();
        using var library = CreateLibrary(store);
        var first = (await library.AddManualGameAsync(Draft("First", "C:\\Games\\First.exe"), CancellationToken.None)).Item!;
        var second = (await library.AddManualGameAsync(Draft("Second", "C:\\Games\\Second.exe"), CancellationToken.None)).Item!;
        store.Document = store.Document! with
        {
            Games = [
            first with { SaveDirectory = "C:\\Private\\Saves", SaveSyncId = Guid.NewGuid(), SaveSyncPeerIds = [Guid.NewGuid()], ScreenshotFolders = ["C:\\Private\\Screens"] },
            second,
        ]
        };
        await library.LoadAsync(CancellationToken.None);
        var portability = new LibraryPortabilityCoordinator(library, new ManualGameDraftValidator(), TimeProvider.System);
        await using var output = new MemoryStream();

        var result = await portability.ExportAsync(output, [first.Id], CancellationToken.None);
        var json = Encoding.UTF8.GetString(output.ToArray());

        Assert.True(result.Success);
        Assert.Contains("First", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Second", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveSync", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Private", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LaunchSessions", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportRequiresValidPresentTargetAndReviewedAtomicCommit()
    {
        var root = Directory.CreateTempSubdirectory("NovaLauncher-portability-");
        try
        {
            var executable = Path.Combine(root.FullName, "Game.exe");
            await File.WriteAllBytesAsync(executable, []);
            var store = new MemoryStore();
            using var library = CreateLibrary(store);
            var portability = new LibraryPortabilityCoordinator(library, new ManualGameDraftValidator(), TimeProvider.System);
            var document = $$"""
                {"schemaVersion":1,"entries":[{"exportId":"{{Guid.NewGuid()}}","name":"Portable","platform":"Windows","source":"Manual","target":"{{executable.Replace("\\", "\\\\")}}","targetKind":0,"arguments":[],"workingDirectory":null,"isFavorite":false,"sourceItemId":null,"notes":"Local note","tags":["Co-op"]}]}
                """;
            await using var input = new MemoryStream(Encoding.UTF8.GetBytes(document));

            var (preview, error) = await portability.PreviewImportAsync(input, CancellationToken.None);
            Assert.Null(error);
            var item = Assert.Single(preview!.Items);
            Assert.Equal(LibraryTransferChange.Add, item.Change);
            Assert.Empty(library.Games);

            var committed = await portability.CommitImportAsync(preview, [item.Index], CancellationToken.None);
            Assert.True(committed.Success);
            Assert.Equal("Portable", Assert.Single(library.Games).Name);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ImportRejectsUnknownSecretFieldsAndMissingExecutables()
    {
        var store = new MemoryStore();
        using var library = CreateLibrary(store);
        var portability = new LibraryPortabilityCoordinator(library, new ManualGameDraftValidator(), TimeProvider.System);
        var unknown = $$"""
            {"schemaVersion":1,"entries":[],"pairingSecret":"{{Guid.NewGuid()}}"}
            """;
        await using var secretInput = new MemoryStream(Encoding.UTF8.GetBytes(unknown));
        var (_, secretError) = await portability.PreviewImportAsync(secretInput, CancellationToken.None);
        Assert.Contains("unknown fields", secretError, StringComparison.OrdinalIgnoreCase);

        var missing = $$"""
            {"schemaVersion":1,"entries":[{"exportId":"{{Guid.NewGuid()}}","name":"Missing","platform":"Windows","source":"Manual","target":"C:\\Missing\\Game.exe","targetKind":0,"arguments":[],"workingDirectory":null,"isFavorite":false,"sourceItemId":null,"notes":null,"tags":null}]}
            """;
        await using var missingInput = new MemoryStream(Encoding.UTF8.GetBytes(missing));
        var (preview, error) = await portability.PreviewImportAsync(missingInput, CancellationToken.None);
        Assert.Null(error);
        Assert.Equal(LibraryTransferChange.Rejected, Assert.Single(preview!.Items).Change);
    }

    private static LibraryCoordinator CreateLibrary(IDocumentStore<GamesDocument> store) =>
        new(store, new ManualGameDraftValidator(), TimeProvider.System);

    private static ManualGameDraft Draft(string name, string target) =>
        new(name, "Windows", target, [], null, LaunchTargetKind.Executable);

    private sealed class MemoryStore : IDocumentStore<GamesDocument>
    {
        public GamesDocument? Document { get; set; }

        public Task<DocumentLoadResult<GamesDocument>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(
            Document is null
                ? new DocumentLoadResult<GamesDocument>(DocumentLoadStatus.NotFound, null, null)
                : new DocumentLoadResult<GamesDocument>(DocumentLoadStatus.Loaded, Document, null));

        public Task<DocumentSaveResult> SaveAsync(GamesDocument document, CancellationToken cancellationToken)
        {
            Document = document;
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null));
        }
    }
}
