namespace NovaLauncher.Application.Steam;

public sealed record SteamGameCandidate(
    uint AppId,
    string Name,
    string InstallDirectory,
    string ManifestPath,
    string LibraryRoot);

public sealed record SteamImportFailure(string Path, string Reason);

public sealed record SteamCatalogScanResult(
    IReadOnlyList<SteamGameCandidate> Games,
    IReadOnlyList<SteamImportFailure> Failures,
    IReadOnlyList<string> LibraryRoots);

public interface ISteamCatalogSource
{
    Task<SteamCatalogScanResult> ScanAsync(string? manualSteamRoot, CancellationToken cancellationToken);
}

public enum SteamImportChange
{
    Add,
    Update,
    Unchanged,
}

public sealed record SteamImportPreviewItem(
    uint AppId,
    string Name,
    string LibraryRoot,
    SteamImportChange Change);

public sealed record SteamImportPreview(
    Guid PreviewId,
    long LibraryRevision,
    IReadOnlyList<SteamImportPreviewItem> Items,
    IReadOnlyList<SteamImportFailure> Failures,
    IReadOnlyList<string> LibraryRoots,
    int Added,
    int Updated,
    int Unchanged);

public enum SteamImportCommitStatus
{
    Saved,
    NoPreview,
    PreviewStale,
    PersistenceFailed,
}

public sealed record SteamImportCommitResult(SteamImportCommitStatus Status, int Imported, string? Error);
