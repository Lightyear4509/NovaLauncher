using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using NovaLauncher.Application.Persistence;

namespace NovaLauncher.Infrastructure.Persistence;

public sealed class BackupArchiveService : IBackupArchiveService
{
    private const long MaximumArchiveBytes = 128L * 1024 * 1024;
    private static readonly DateTimeOffset DeterministicZipTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] DocumentNames = ["games.json", "collections.json", "settings.json"];
    private readonly string _dataRoot;
    private readonly IAtomicFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;

    public BackupArchiveService(string dataRoot, IAtomicFileSystem fileSystem, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        _dataRoot = Path.GetFullPath(dataRoot);
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<BackupExportResult> ExportAsync(
        string destinationPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var fullDestination = Path.GetFullPath(destinationPath);

        await using var backupLock = await _fileSystem
            .AcquireExclusiveLockAsync(Path.Combine(_dataRoot, ".backup.lock"), cancellationToken)
            .ConfigureAwait(false);

        return await ExportCoreAsync(fullDestination, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BackupRestorePreview> PreviewRestoreAsync(
        string archivePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var documents = await ReadAndValidateArchiveAsync(archivePath, cancellationToken).ConfigureAwait(false);
            return new BackupRestorePreview(true, documents.Keys.Order(StringComparer.Ordinal).ToArray(), null);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new BackupRestorePreview(false, [], exception.Message);
        }
    }

    public async Task<BackupRestoreResult> RestoreAsync(
        string archivePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        Dictionary<string, byte[]> documents;
        try
        {
            documents = await ReadAndValidateArchiveAsync(archivePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new BackupRestoreResult(false, null, exception.Message);
        }

        await using var backupLock = await _fileSystem
            .AcquireExclusiveLockAsync(Path.Combine(_dataRoot, ".backup.lock"), cancellationToken)
            .ConfigureAwait(false);

        _fileSystem.CreateDirectory(_dataRoot);
        var timestamp = _timeProvider.GetUtcNow().ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture);
        var backupDirectory = Path.Combine(_dataRoot, "Backups");
        _fileSystem.CreateDirectory(backupDirectory);
        var preRestorePath = Path.Combine(backupDirectory, $"pre-restore-{timestamp}.zip");
        if (DocumentNames.Any(name => _fileSystem.FileExists(Path.Combine(_dataRoot, name))))
        {
            var export = await ExportCoreAsync(preRestorePath, cancellationToken).ConfigureAwait(false);
            if (!export.Succeeded)
            {
                return new BackupRestoreResult(false, null, $"Pre-restore backup failed: {export.Error}");
            }
        }
        else
        {
            await CreateEmptyStateArchiveAsync(preRestorePath, cancellationToken).ConfigureAwait(false);
        }

        var transactionDirectory = Path.Combine(_dataRoot, $".restore-{Guid.NewGuid():N}");
        _fileSystem.CreateDirectory(transactionDirectory);
        var replacedNames = new List<string>();

        try
        {
            foreach (var pair in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stagedPath = Path.Combine(transactionDirectory, pair.Key);
                await _fileSystem.WriteAllBytesDurableAsync(stagedPath, pair.Value, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var pair in documents)
            {
                var primaryPath = Path.Combine(_dataRoot, pair.Key);
                var rollbackPath = Path.Combine(transactionDirectory, $"{pair.Key}.rollback");
                if (_fileSystem.FileExists(primaryPath))
                {
                    _fileSystem.CopyFile(primaryPath, rollbackPath, overwrite: false);
                }

                var stagedPath = Path.Combine(transactionDirectory, pair.Key);
                if (_fileSystem.FileExists(primaryPath))
                {
                    _fileSystem.ReplaceFile(stagedPath, primaryPath, $"{primaryPath}.bak");
                }
                else
                {
                    _fileSystem.MoveFile(stagedPath, primaryPath, overwrite: false);
                }

                replacedNames.Add(pair.Key);
            }

            return new BackupRestoreResult(true, preRestorePath, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RollBack(replacedNames, transactionDirectory);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RollBack(replacedNames, transactionDirectory);
            return new BackupRestoreResult(false, preRestorePath, $"Restore failed and was rolled back: {exception.Message}");
        }
        finally
        {
            TryDeleteTransactionDirectory(transactionDirectory);
        }
    }

    private async Task<BackupExportResult> ExportCoreAsync(
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var documents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            foreach (var name in DocumentNames)
            {
                var path = Path.Combine(_dataRoot, name);
                if (!_fileSystem.FileExists(path))
                {
                    continue;
                }

                var content = await _fileSystem.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                ValidateDocument(name, content);
                documents.Add(name, content);
            }

            if (documents.Count == 0)
            {
                return new BackupExportResult(false, null, "There are no valid NovaLauncher documents to export.");
            }

            var directory = Path.GetDirectoryName(destinationPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    16_384,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var pair in documents.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                    {
                        var entry = archive.CreateEntry(pair.Key, CompressionLevel.Optimal);
                        entry.LastWriteTime = DeterministicZipTimestamp;
                        await using var entryStream = entry.Open();
                        await entryStream.WriteAsync(pair.Value, cancellationToken).ConfigureAwait(false);
                    }
                }

                File.Move(temporaryPath, destinationPath, overwrite: true);
            }
            finally
            {
                File.Delete(temporaryPath);
            }

            return new BackupExportResult(true, destinationPath, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new BackupExportResult(false, null, exception.Message);
        }
    }

    private static async Task CreateEmptyStateArchiveAsync(
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            4_096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // An empty archive records that the destination had no canonical documents.
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static async Task<Dictionary<string, byte[]>> ReadAndValidateArchiveAsync(
        string archivePath,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(archivePath);
        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists || fileInfo.Length == 0 || fileInfo.Length > MaximumArchiveBytes)
        {
            throw new InvalidDataException("The backup archive is missing, empty, or exceeds 128 MiB.");
        }

        var documents = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 16_384, true);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        if (archive.Entries.Count is < 1 or > 3)
        {
            throw new InvalidDataException("A backup must contain one to three NovaLauncher documents.");
        }

        long totalLength = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!DocumentNames.Contains(entry.FullName, StringComparer.Ordinal) ||
                !string.Equals(entry.Name, entry.FullName, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unsafe or unknown backup entry '{entry.FullName}'.");
            }

            if (!documents.TryAdd(entry.FullName, []))
            {
                throw new InvalidDataException($"Duplicate backup entry '{entry.FullName}'.");
            }

            totalLength += entry.Length;
            if (entry.Length <= 0 || entry.Length > MaximumArchiveBytes || totalLength > MaximumArchiveBytes)
            {
                throw new InvalidDataException("Backup content exceeds the extraction safety limit.");
            }

            await using var entryStream = entry.Open();
            using var memory = new MemoryStream(capacity: checked((int)entry.Length));
            await entryStream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            var content = memory.ToArray();
            ValidateDocument(entry.FullName, content);
            documents[entry.FullName] = content;
        }

        return documents;
    }

    private static void ValidateDocument(string name, byte[] content)
    {
        string? validationError;
        try
        {
            validationError = name switch
            {
                "games.json" => Validate(
                    JsonSerializer.Deserialize(content, PersistenceJsonContext.Default.GamesDocument),
                    new GamesDocumentPolicy()),
                "collections.json" => Validate(
                    JsonSerializer.Deserialize(content, PersistenceJsonContext.Default.CollectionsDocument),
                    new CollectionsDocumentPolicy()),
                "settings.json" => Validate(
                    JsonSerializer.Deserialize(content, PersistenceJsonContext.Default.SettingsDocument),
                    new SettingsDocumentPolicy()),
                _ => "Unknown document.",
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid {name}: malformed JSON.", exception);
        }

        if (validationError is not null)
        {
            throw new InvalidDataException($"Invalid {name}: {validationError}");
        }
    }

    private static string? Validate<TDocument>(TDocument? document, IDocumentPolicy<TDocument> policy)
        where TDocument : class, IVersionedDocument
    {
        if (document is null)
        {
            return "Document is empty.";
        }

        if (document.SchemaVersion != policy.CurrentSchemaVersion)
        {
            return $"Expected schema {policy.CurrentSchemaVersion}, received {document.SchemaVersion}.";
        }

        return policy.Validate(document);
    }

    private void RollBack(IEnumerable<string> replacedNames, string transactionDirectory)
    {
        foreach (var name in replacedNames.Reverse())
        {
            var primaryPath = Path.Combine(_dataRoot, name);
            var rollbackPath = Path.Combine(transactionDirectory, $"{name}.rollback");
            try
            {
                if (_fileSystem.FileExists(rollbackPath))
                {
                    _fileSystem.MoveFile(rollbackPath, primaryPath, overwrite: true);
                }
                else if (_fileSystem.FileExists(primaryPath))
                {
                    _fileSystem.DeleteFile(primaryPath);
                }
            }
            catch (IOException)
            {
                // The pre-restore archive is retained for explicit recovery.
            }
            catch (UnauthorizedAccessException)
            {
                // The pre-restore archive is retained for explicit recovery.
            }
        }
    }

    private static void TryDeleteTransactionDirectory(string transactionDirectory)
    {
        try
        {
            if (Directory.Exists(transactionDirectory))
            {
                Directory.Delete(transactionDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A later startup cleanup may remove abandoned transaction staging.
        }
        catch (UnauthorizedAccessException)
        {
            // A later startup cleanup may remove abandoned transaction staging.
        }
    }
}
