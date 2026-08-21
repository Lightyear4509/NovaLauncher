using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;

namespace NovaLauncher.Application.GameTransfer;

public sealed record GameTransferFile(string RelativePath, long Length, string Sha256, DateTimeOffset LastWriteTimeUtc);
public sealed record GameTransferManifest(Guid OfferId, GameId GameId, string PackageName, Guid SenderDeviceId, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, IReadOnlyList<GameTransferFile> Files, long TotalBytes);
public sealed record GameTransferPreview(bool Accepted, string PackageName, string SourceFolder, IReadOnlyList<GameTransferFile> Files, IReadOnlyList<string> Exclusions, long TotalBytes, string? Error);
public sealed record AuthorizedGameTransfer(GameTransferManifest Manifest, string SourceFolder, IReadOnlySet<Guid> RecipientDeviceIds);
public sealed record PeerGameTransferOffer(TrustedSaveSyncPeer Peer, GameTransferManifest Manifest);
public sealed record GameTransferChunkResult(bool Success, byte[]? Bytes, bool EndOfFile, string? Error);
public sealed record GameTransferResult(bool Success, bool Resumable, string Message, long CompletedBytes = 0, long TotalBytes = 0);
public sealed record GameTransferProgress(string PackageName, string RelativePath, long CompletedBytes, long TotalBytes, double BytesPerSecond);
public sealed record GameTransferAuditItem(Guid OperationId, Guid OfferId, Guid PeerDeviceId, string PackageName, long TotalBytes, int FileCount, DateTimeOffset TimestampUtc, string Outcome);
public sealed record ReceivedContentScanResult(bool ScannerAvailable, bool Clean, string Message);

public interface IReceivedContentScanner
{
    Task<ReceivedContentScanResult> ScanAsync(string directory, CancellationToken cancellationToken);
}

public interface IPeerGameTransferTransport
{
    void AttachGameTransferEndpoint(IPeerGameTransferEndpoint endpoint);
    Task<IReadOnlyList<GameTransferManifest>> ListGameTransferOffersAsync(TrustedSaveSyncPeer peer, CancellationToken cancellationToken);
    Task<GameTransferChunkResult> PullGameTransferChunkAsync(TrustedSaveSyncPeer peer, Guid offerId, string relativePath, long offset, int maximumBytes, CancellationToken cancellationToken);
}

public interface IPeerGameTransferEndpoint
{
    Task<IReadOnlyList<GameTransferManifest>> ListAuthorizedGameTransfersAsync(Guid requestingDeviceId, CancellationToken cancellationToken);
    Task<GameTransferChunkResult> ServeGameTransferChunkAsync(Guid requestingDeviceId, Guid offerId, string relativePath, long offset, int maximumBytes, CancellationToken cancellationToken);
}

public interface IGameTransferService : IPeerGameTransferEndpoint
{
    Task<GameTransferPreview> PreviewAsync(LibraryItem game, string sourceFolder, CancellationToken cancellationToken);
    Task<GameTransferResult> AuthorizeAsync(LibraryItem game, GameTransferPreview preview, IReadOnlyCollection<Guid> recipientDeviceIds, bool userAttestedCopyRights, CancellationToken cancellationToken);
    Task<IReadOnlyList<PeerGameTransferOffer>> RefreshOffersAsync(CancellationToken cancellationToken);
    Task<GameTransferResult> DownloadAsync(PeerGameTransferOffer offer, string destination, IProgress<GameTransferProgress>? progress, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameTransferAuditItem>> GetHistoryAsync(CancellationToken cancellationToken);
}
