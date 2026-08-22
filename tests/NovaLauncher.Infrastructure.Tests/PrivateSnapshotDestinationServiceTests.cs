using System.Security.Cryptography;
using System.Text.Json;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;
using NovaLauncher.Infrastructure.SaveSync;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class PrivateSnapshotDestinationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NovaLauncherTests", Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset _now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConfigurePreviewAndPublishCommitVerifiedHeadLast()
    {
        var gameId = new GameId(Guid.NewGuid());
        var snapshot = await CreateSnapshotAsync(gameId, null, "save.dat", [1, 2, 3]);
        var destination = Path.Combine(_root, "destination");
        using var service = CreateService();

        Assert.True((await service.ConfigureAsync(new(destination, 128 * 1024 * 1024, 3), default)).Success);
        var (preview, error) = await service.PreviewPublishAsync(gameId, default);
        Assert.Null(error);
        Assert.NotNull(preview);
        Assert.True(preview.CanPublish);
        Assert.True((await service.PublishAsync(preview, default)).Success);

        var gameRoot = Path.Combine(destination, "snapshots", gameId.Value.ToString("N"));
        Assert.Equal(snapshot.ToString("D"), await File.ReadAllTextAsync(Path.Combine(gameRoot, "head.txt")));
        Assert.True(File.Exists(Path.Combine(gameRoot, snapshot.ToString("N"), "save.dat")));
        Assert.Empty(Directory.EnumerateDirectories(gameRoot, ".staging-*"));
        Assert.Contains(await service.GetHealthHistoryAsync(default), item => item.Operation == "Publish" && item.Outcome == DestinationHealthOutcome.Succeeded);
    }

    [Fact]
    public async Task TamperingAfterPreviewFailsClosedAndDoesNotPublishHead()
    {
        var gameId = new GameId(Guid.NewGuid());
        var snapshot = await CreateSnapshotAsync(gameId, null, "save.dat", [1, 2, 3]);
        var destination = Path.Combine(_root, "destination");
        using var service = CreateService();
        await service.ConfigureAsync(new(destination, 128 * 1024 * 1024, 3), default);
        var (preview, _) = await service.PreviewPublishAsync(gameId, default);
        await File.WriteAllBytesAsync(Path.Combine(SourceSnapshot(gameId, snapshot), "save.dat"), [9, 9, 9]);

        var result = await service.PublishAsync(preview!, default);

        Assert.False(result.Success);
        Assert.False(File.Exists(Path.Combine(destination, "snapshots", gameId.Value.ToString("N"), "head.txt")));
        Assert.Contains("failed closed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewCannotBeReplayedAndBudgetRejectsOversizedSnapshot()
    {
        var gameId = new GameId(Guid.NewGuid());
        var snapshot = await CreateSnapshotAsync(gameId, null, "large-a.dat", new byte[40 * 1024 * 1024]);
        var directory = SourceSnapshot(gameId, snapshot);
        var second = new byte[40 * 1024 * 1024];
        await File.WriteAllBytesAsync(Path.Combine(directory, "large-b.dat"), second);
        var manifestPath = Path.Combine(directory, "manifest.txt");
        var manifest = JsonSerializer.Deserialize<SaveSnapshotManifest>(await File.ReadAllTextAsync(manifestPath))!;
        manifest = manifest with { Files = manifest.Files.Append(new SaveFileEntry("large-b.dat", second.Length, Convert.ToHexString(SHA256.HashData(second)))).ToArray() };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
        var destination = Path.Combine(_root, "destination");
        using var service = CreateService();
        await service.ConfigureAsync(new(destination, PrivateSnapshotDestinationService.MinimumBudgetBytes, 1), default);

        var (preview, _) = await service.PreviewPublishAsync(gameId, default);

        Assert.NotNull(preview);
        Assert.False(preview.CanPublish);
        Assert.False((await service.PublishAsync(preview, default)).Success);
    }

    [Fact]
    public async Task CorruptHeadCanBeQuarantinedOnlyAfterVerifiedParentIsRechecked()
    {
        var gameId = new GameId(Guid.NewGuid());
        var parent = await CreateSnapshotAsync(gameId, null, "save.dat", [1]);
        var child = await CreateSnapshotAsync(gameId, parent, "save.dat", [2]);
        var destination = Path.Combine(_root, "destination");
        using var service = CreateService();
        await service.ConfigureAsync(new(destination, 128 * 1024 * 1024, 3), default);
        foreach (var id in new[] { parent, child })
        {
            await File.WriteAllTextAsync(Path.Combine(SourceGame(gameId), "head.txt"), id.ToString("D"));
            var (publish, _) = await service.PreviewPublishAsync(gameId, default);
            Assert.True((await service.PublishAsync(publish!, default)).Success);
        }
        var childFile = Path.Combine(destination, "snapshots", gameId.Value.ToString("N"), child.ToString("N"), "save.dat");
        await File.WriteAllBytesAsync(childFile, [7]);

        var (repair, error) = await service.PreviewQuarantineAsync(gameId, child, default);
        Assert.Null(error);
        Assert.True(repair!.CanRepair);
        Assert.Equal(parent, repair.VerifiedParentSnapshotId);
        Assert.True((await service.QuarantineAsync(repair, default)).Success);

        var gameRoot = Path.Combine(destination, "snapshots", gameId.Value.ToString("N"));
        Assert.Equal(parent.ToString("D"), await File.ReadAllTextAsync(Path.Combine(gameRoot, "head.txt")));
        Assert.False(Directory.Exists(Path.Combine(gameRoot, child.ToString("N"))));
        Assert.Single(Directory.EnumerateDirectories(Path.Combine(destination, "quarantine", gameId.Value.ToString("N"))));
    }

    [Fact]
    public async Task NotesAndTagsRequireSeparateOneTimePushAndPullPreviews()
    {
        var gameId = new GameId(Guid.NewGuid());
        var destination = Path.Combine(_root, "destination");
        using var service = CreateService();
        await service.ConfigureAsync(new(destination, 128 * 1024 * 1024, 3, SyncNotesAndTags: true), default);
        var entry = new PrivateMetadataEntry(gameId, "Reviewed game", "private note", ["Co-op", "Favorite"], _now);

        var (push, pushError) = await service.PreviewMetadataPushAsync(entry, default);
        Assert.Null(pushError);
        Assert.DoesNotContain("target", JsonSerializer.Serialize(push!.Entry), StringComparison.OrdinalIgnoreCase);
        Assert.True((await service.CommitMetadataPushAsync(push, default)).Success);
        Assert.False((await service.CommitMetadataPushAsync(push, default)).Success);

        var (pull, pullError) = await service.PreviewMetadataPullAsync(gameId, default);
        Assert.Null(pullError);
        Assert.Equal("Pull", pull!.Direction);
        var (received, commitError) = await service.CommitMetadataPullAsync(pull, default);
        Assert.Null(commitError);
        Assert.Equal(entry.GameId, received!.GameId);
        Assert.Equal(entry.Name, received.Name);
        Assert.Equal(entry.Notes, received.Notes);
        Assert.Equal(entry.Tags, received.Tags);
        Assert.Equal(entry.UpdatedAtUtc, received.UpdatedAtUtc);
        Assert.Null((await service.CommitMetadataPullAsync(pull, default)).Entry);
    }

    [Fact]
    public async Task CancellationDuringCopyLeavesPartialStageUnacceptedAndNoHead()
    {
        var gameId = new GameId(Guid.NewGuid());
        var snapshot = await CreateSnapshotAsync(gameId, null, "first.dat", [1, 2, 3]);
        var directory = SourceSnapshot(gameId, snapshot);
        var second = new byte[] { 4, 5, 6 };
        await File.WriteAllBytesAsync(Path.Combine(directory, "second.dat"), second);
        var manifestPath = Path.Combine(directory, "manifest.txt");
        var manifest = JsonSerializer.Deserialize<SaveSnapshotManifest>(await File.ReadAllTextAsync(manifestPath))!;
        manifest = manifest with { Files = manifest.Files.Append(new SaveFileEntry("second.dat", second.Length, Convert.ToHexString(SHA256.HashData(second)))).ToArray() };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
        var copied = 0;
        using var service = new PrivateSnapshotDestinationService(_root, new FixedTimeProvider(_now), (_, _) =>
        {
            if (Interlocked.Increment(ref copied) == 1) throw new OperationCanceledException("Injected interruption");
            return Task.CompletedTask;
        });
        var destination = Path.Combine(_root, "destination");
        await service.ConfigureAsync(new(destination, 128 * 1024 * 1024, 3), default);
        var (preview, _) = await service.PreviewPublishAsync(gameId, default);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.PublishAsync(preview!, default));

        var gameRoot = Path.Combine(destination, "snapshots", gameId.Value.ToString("N"));
        Assert.False(File.Exists(Path.Combine(gameRoot, "head.txt")));
        Assert.Empty(Directory.Exists(gameRoot)
            ? Directory.EnumerateDirectories(gameRoot).Where(path => Guid.TryParse(Path.GetFileName(path), out _))
            : []);
    }

    private PrivateSnapshotDestinationService CreateService() => new(_root, new FixedTimeProvider(_now));

    private async Task<Guid> CreateSnapshotAsync(GameId gameId, Guid? parent, string relativePath, byte[] content)
    {
        var snapshotId = Guid.NewGuid();
        var directory = SourceSnapshot(gameId, snapshotId);
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, relativePath), content);
        var hash = Convert.ToHexString(SHA256.HashData(content));
        var manifest = new SaveSnapshotManifest(snapshotId, parent, gameId, Guid.NewGuid(), _now, [new(relativePath, content.Length, hash)], []);
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest.txt"), JsonSerializer.Serialize(manifest));
        await File.WriteAllTextAsync(Path.Combine(SourceGame(gameId), "head.txt"), snapshotId.ToString("D"));
        return snapshotId;
    }

    private string SourceGame(GameId gameId) => Path.Combine(_root, "SaveSync", "Snapshots", gameId.Value.ToString("N"));
    private string SourceSnapshot(GameId gameId, Guid snapshotId) => Path.Combine(SourceGame(gameId), snapshotId.ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
