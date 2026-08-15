using NovaLauncher.Application.Library;

namespace NovaLauncher.Application.Steam;

public sealed class SteamImportCoordinator(
    ISteamCatalogSource catalogSource,
    LibraryCoordinator library)
{
    private SteamImportPreview? _preview;
    private IReadOnlyList<SteamGameCandidate> _candidates = [];

    public async Task<SteamImportPreview> PreviewAsync(
        string? manualSteamRoot,
        CancellationToken cancellationToken)
    {
        var scan = await catalogSource.ScanAsync(manualSteamRoot, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        _preview = await library.CreateSteamImportPreviewAsync(scan, cancellationToken).ConfigureAwait(false);
        _candidates = scan.Games;
        return _preview;
    }

    public async Task<SteamImportCommitResult> CommitAsync(CancellationToken cancellationToken)
    {
        var preview = _preview;
        if (preview is null)
        {
            return new SteamImportCommitResult(SteamImportCommitStatus.NoPreview, 0, "Preview the Steam import first.");
        }

        var result = await library.CommitSteamImportAsync(preview, _candidates, cancellationToken).ConfigureAwait(false);
        if (result.Status == SteamImportCommitStatus.Saved)
        {
            _preview = null;
            _candidates = [];
        }

        return result;
    }
}
