using System.IO.Compression;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Infrastructure.Persistence;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class BackupArchiveServiceTests
{
    private static readonly string[] ExportedDocumentNames = ["games.json", "settings.json"];

    [Fact]
    public async Task ExportIncludesOnlyValidatedDocumentsAndPreviewListsThem()
    {
        await using var fixture = PersistenceTestFixture.Create();
        await fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("Exported"), CancellationToken.None);
        await fixture.SettingsStore.SaveAsync(SettingsDocument.Default, CancellationToken.None);
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root, "secret.txt"),
            "must-not-export",
            CancellationToken.None);
        var service = new BackupArchiveService(fixture.Root, fixture.FileSystem, fixture.Clock);
        var archivePath = Path.Combine(fixture.Root, "export.zip");

        var export = await service.ExportAsync(archivePath, CancellationToken.None);
        var preview = await service.PreviewRestoreAsync(archivePath, CancellationToken.None);

        Assert.True(export.Succeeded, export.Error);
        Assert.True(preview.IsValid, preview.Error);
        Assert.Equal(ExportedDocumentNames, preview.Documents);
    }

    [Fact]
    public async Task ExportRefusesEmptyDataRoot()
    {
        await using var fixture = PersistenceTestFixture.Create();
        var service = new BackupArchiveService(fixture.Root, fixture.FileSystem, fixture.Clock);

        var export = await service.ExportAsync(Path.Combine(fixture.Root, "empty.zip"), CancellationToken.None);

        Assert.False(export.Succeeded);
        Assert.Contains("no valid", export.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportRefusesInvalidCanonicalDocument()
    {
        await using var fixture = PersistenceTestFixture.Create();
        Directory.CreateDirectory(fixture.Root);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "settings.json"), "{bad", CancellationToken.None);
        var service = new BackupArchiveService(fixture.Root, fixture.FileSystem, fixture.Clock);

        var export = await service.ExportAsync(Path.Combine(fixture.Root, "invalid.zip"), CancellationToken.None);

        Assert.False(export.Succeeded);
        Assert.Contains("invalid", export.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewRejectsMissingAndEmptyArchives()
    {
        await using var fixture = PersistenceTestFixture.Create();
        Directory.CreateDirectory(fixture.Root);
        var emptyPath = Path.Combine(fixture.Root, "empty.zip");
        await File.WriteAllBytesAsync(emptyPath, [], CancellationToken.None);
        var service = new BackupArchiveService(fixture.Root, fixture.FileSystem, fixture.Clock);

        var missing = await service.PreviewRestoreAsync(Path.Combine(fixture.Root, "missing.zip"), CancellationToken.None);
        var empty = await service.PreviewRestoreAsync(emptyPath, CancellationToken.None);

        Assert.False(missing.IsValid);
        Assert.False(empty.IsValid);
    }

    [Fact]
    public async Task RestoreRejectsInvalidArchiveWithoutCreatingDocuments()
    {
        await using var fixture = PersistenceTestFixture.Create();
        Directory.CreateDirectory(fixture.Root);
        var invalidPath = Path.Combine(fixture.Root, "invalid.zip");
        await File.WriteAllTextAsync(invalidPath, "not a zip", CancellationToken.None);
        var service = new BackupArchiveService(fixture.Root, fixture.FileSystem, fixture.Clock);

        var restore = await service.RestoreAsync(invalidPath, CancellationToken.None);

        Assert.False(restore.Succeeded);
        Assert.Null(restore.PreRestoreBackupPath);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "games.json")));
    }

    [Fact]
    public async Task PreviewRejectsTraversalEntry()
    {
        await using var fixture = PersistenceTestFixture.Create();
        Directory.CreateDirectory(fixture.Root);
        var archivePath = Path.Combine(fixture.Root, "unsafe.zip");
        await using (var stream = File.Create(archivePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../games.json");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("{}");
        }

        var service = new BackupArchiveService(fixture.Root, fixture.FileSystem, fixture.Clock);
        var preview = await service.PreviewRestoreAsync(archivePath, CancellationToken.None);

        Assert.False(preview.IsValid);
        Assert.Contains("Unsafe", preview.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewRejectsDuplicateEmptyAndOverCountArchives()
    {
        await using var fixture = PersistenceTestFixture.Create();
        Directory.CreateDirectory(fixture.Root);
        var validGames = await CreateValidGamesJsonAsync(fixture);
        var duplicatePath = Path.Combine(fixture.Root, "duplicate.zip");
        await CreateArchiveAsync(duplicatePath, [
            ("games.json", validGames),
            ("games.json", validGames),
        ]);
        var emptyEntryPath = Path.Combine(fixture.Root, "empty-entry.zip");
        await CreateArchiveAsync(emptyEntryPath, [("games.json", "")]);
        var overCountPath = Path.Combine(fixture.Root, "over-count.zip");
        await CreateArchiveAsync(overCountPath, [
            ("games.json", validGames),
            ("games.json", validGames),
            ("games.json", validGames),
            ("games.json", validGames),
        ]);
        var service = new BackupArchiveService(fixture.Root, fixture.FileSystem, fixture.Clock);

        Assert.False((await service.PreviewRestoreAsync(duplicatePath, CancellationToken.None)).IsValid);
        Assert.False((await service.PreviewRestoreAsync(emptyEntryPath, CancellationToken.None)).IsValid);
        Assert.False((await service.PreviewRestoreAsync(overCountPath, CancellationToken.None)).IsValid);
    }

    [Fact]
    public async Task RestoreCommitsValidatedArchiveAndCreatesPreRestoreBackup()
    {
        await using var source = PersistenceTestFixture.Create();
        await using var destination = PersistenceTestFixture.Create();
        await source.GamesStore.SaveAsync(PersistenceTestFixture.Games("Incoming"), CancellationToken.None);
        await destination.GamesStore.SaveAsync(PersistenceTestFixture.Games("Existing"), CancellationToken.None);
        var archivePath = Path.Combine(source.Root, "export.zip");
        var sourceService = new BackupArchiveService(source.Root, source.FileSystem, source.Clock);
        Assert.True((await sourceService.ExportAsync(archivePath, CancellationToken.None)).Succeeded);

        var destinationService = new BackupArchiveService(destination.Root, destination.FileSystem, destination.Clock);
        var restore = await destinationService.RestoreAsync(archivePath, CancellationToken.None);
        var load = await destination.GamesStore.LoadAsync(CancellationToken.None);

        Assert.True(restore.Succeeded, restore.Error);
        Assert.True(File.Exists(restore.PreRestoreBackupPath));
        Assert.Equal("Incoming", Assert.Single(Assert.IsType<GamesDocument>(load.Document).Games).Name);
    }

    [Fact]
    public async Task RestoreIntoEmptyProfileRecordsEmptyPreRestoreState()
    {
        await using var source = PersistenceTestFixture.Create();
        await using var destination = PersistenceTestFixture.Create();
        await source.GamesStore.SaveAsync(PersistenceTestFixture.Games("Incoming"), CancellationToken.None);
        var archivePath = Path.Combine(source.Root, "export.zip");
        var sourceService = new BackupArchiveService(source.Root, source.FileSystem, source.Clock);
        Assert.True((await sourceService.ExportAsync(archivePath, CancellationToken.None)).Succeeded);
        var destinationService = new BackupArchiveService(destination.Root, destination.FileSystem, destination.Clock);

        var restore = await destinationService.RestoreAsync(archivePath, CancellationToken.None);

        Assert.True(restore.Succeeded, restore.Error);
        Assert.True(File.Exists(restore.PreRestoreBackupPath));
        Assert.Equal(
            "Incoming",
            Assert.Single(Assert.IsType<GamesDocument>(
                (await destination.GamesStore.LoadAsync(CancellationToken.None)).Document).Games).Name);
    }

    [Fact]
    public async Task MidCommitFailureRollsBackAlreadyReplacedDocuments()
    {
        await using var source = PersistenceTestFixture.Create();
        await using var destination = PersistenceTestFixture.Create();
        await source.GamesStore.SaveAsync(PersistenceTestFixture.Games("Incoming"), CancellationToken.None);
        await source.SettingsStore.SaveAsync(SettingsDocument.Default with
        {
            Settings = SettingsDocument.Default.Settings with { ThemeId = "incoming" },
        }, CancellationToken.None);
        await destination.GamesStore.SaveAsync(PersistenceTestFixture.Games("Existing"), CancellationToken.None);
        await destination.SettingsStore.SaveAsync(SettingsDocument.Default, CancellationToken.None);
        var archivePath = Path.Combine(source.Root, "export.zip");
        var sourceService = new BackupArchiveService(source.Root, source.FileSystem, source.Clock);
        Assert.True((await sourceService.ExportAsync(archivePath, CancellationToken.None)).Succeeded);

        var failingFileSystem = new SecondReplaceFailureFileSystem(destination.FileSystem);
        var destinationService = new BackupArchiveService(destination.Root, failingFileSystem, destination.Clock);
        var restore = await destinationService.RestoreAsync(archivePath, CancellationToken.None);
        var games = await destination.GamesStore.LoadAsync(CancellationToken.None);
        var settings = await destination.SettingsStore.LoadAsync(CancellationToken.None);

        Assert.False(restore.Succeeded);
        Assert.Equal("Existing", Assert.Single(Assert.IsType<GamesDocument>(games.Document).Games).Name);
        Assert.Equal("nova-dark", Assert.IsType<SettingsDocument>(settings.Document).Settings.ThemeId);
        Assert.True(File.Exists(restore.PreRestoreBackupPath));
    }

    private sealed class SecondReplaceFailureFileSystem(IAtomicFileSystem inner) : DelegatingAtomicFileSystem(inner)
    {
        private int _replaceCount;

        public override void ReplaceFile(string sourcePath, string destinationPath, string backupPath)
        {
            _replaceCount++;
            if (_replaceCount == 2)
            {
                throw new IOException("Injected second replacement failure.");
            }

            base.ReplaceFile(sourcePath, destinationPath, backupPath);
        }
    }

    private static async Task<string> CreateValidGamesJsonAsync(PersistenceTestFixture fixture)
    {
        await fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("Archive"), CancellationToken.None);
        return await File.ReadAllTextAsync(Path.Combine(fixture.Root, "games.json"), CancellationToken.None);
    }

    private static async Task CreateArchiveAsync(
        string path,
        IReadOnlyList<(string Name, string Content)> entries)
    {
        await using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var item in entries)
        {
            var entry = archive.CreateEntry(item.Name);
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync(item.Content);
        }
    }
}
