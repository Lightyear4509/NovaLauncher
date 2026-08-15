namespace NovaLauncher.Application.Persistence;

public sealed record BackupExportResult(bool Succeeded, string? ArchivePath, string? Error);

public sealed record BackupRestorePreview(
    bool IsValid,
    IReadOnlyList<string> Documents,
    string? Error);

public sealed record BackupRestoreResult(
    bool Succeeded,
    string? PreRestoreBackupPath,
    string? Error);

public interface IBackupArchiveService
{
    Task<BackupExportResult> ExportAsync(string destinationPath, CancellationToken cancellationToken);

    Task<BackupRestorePreview> PreviewRestoreAsync(string archivePath, CancellationToken cancellationToken);

    Task<BackupRestoreResult> RestoreAsync(string archivePath, CancellationToken cancellationToken);
}
