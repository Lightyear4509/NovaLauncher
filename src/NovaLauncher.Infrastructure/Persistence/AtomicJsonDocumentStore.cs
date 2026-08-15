using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Globalization;
using NovaLauncher.Application.Persistence;

namespace NovaLauncher.Infrastructure.Persistence;

public sealed class AtomicJsonDocumentStore<TDocument> : IDocumentStore<TDocument>
    where TDocument : class, IVersionedDocument
{
    private const int MaximumDocumentBytes = 64 * 1024 * 1024;
    private readonly string _primaryPath;
    private readonly string _backupPath;
    private readonly string _lockPath;
    private readonly string _globalLockPath;
    private readonly IAtomicFileSystem _fileSystem;
    private readonly IDocumentPolicy<TDocument> _policy;
    private readonly JsonTypeInfo<TDocument> _jsonTypeInfo;
    private readonly TimeProvider _timeProvider;

    public AtomicJsonDocumentStore(
        string dataRoot,
        IAtomicFileSystem fileSystem,
        IDocumentPolicy<TDocument> policy,
        JsonTypeInfo<TDocument> jsonTypeInfo,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        var root = Path.GetFullPath(dataRoot);
        _primaryPath = Path.Combine(root, policy.FileName);
        _backupPath = $"{_primaryPath}.bak";
        _lockPath = $"{_primaryPath}.lock";
        _globalLockPath = Path.Combine(root, ".backup.lock");
    }

    public async Task<DocumentLoadResult<TDocument>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var globalLock = await _fileSystem
            .AcquireExclusiveLockAsync(_globalLockPath, cancellationToken)
            .ConfigureAwait(false);
        await using var documentLock = await _fileSystem
            .AcquireExclusiveLockAsync(_lockPath, cancellationToken)
            .ConfigureAwait(false);

        if (!_fileSystem.FileExists(_primaryPath))
        {
            return new DocumentLoadResult<TDocument>(DocumentLoadStatus.NotFound, null, null);
        }

        var primary = await ReadAndValidateAsync(_primaryPath, cancellationToken).ConfigureAwait(false);
        if (primary.Status == ReadStatus.Valid)
        {
            return new DocumentLoadResult<TDocument>(DocumentLoadStatus.Loaded, primary.Document, null);
        }

        if (primary.Status == ReadStatus.LegacyValid)
        {
            return new DocumentLoadResult<TDocument>(
                DocumentLoadStatus.MigratedLegacy,
                primary.Document,
                $"A legacy {_policy.FileName} was loaded in memory; save to commit schema {_policy.CurrentSchemaVersion}.");
        }

        if (primary.Status == ReadStatus.NewerSchema)
        {
            return new DocumentLoadResult<TDocument>(
                DocumentLoadStatus.UnsupportedNewerSchema,
                null,
                $"{_policy.FileName} uses schema {primary.SchemaVersion}; this build supports {_policy.CurrentSchemaVersion}.");
        }

        if (_fileSystem.FileExists(_backupPath))
        {
            var backup = await ReadAndValidateAsync(_backupPath, cancellationToken).ConfigureAwait(false);
            if (backup.Status == ReadStatus.Valid)
            {
                return new DocumentLoadResult<TDocument>(
                    DocumentLoadStatus.RecoveredFromBackup,
                    backup.Document,
                    $"The primary {_policy.FileName} is invalid; the last-known-good backup was loaded.");
            }
        }

        return new DocumentLoadResult<TDocument>(
            DocumentLoadStatus.Unrecoverable,
            null,
            $"Neither {_policy.FileName} nor its backup contains a supported valid document.");
    }

    public async Task<DocumentSaveResult> SaveAsync(TDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var validationError = ValidateForCurrentSchema(document);
        if (validationError is not null)
        {
            return new DocumentSaveResult(DocumentSaveStatus.Failed, validationError);
        }

        var content = JsonSerializer.SerializeToUtf8Bytes(document, _jsonTypeInfo);
        if (content.Length > MaximumDocumentBytes)
        {
            return new DocumentSaveResult(DocumentSaveStatus.Failed, "The document exceeds the 64 MiB safety limit.");
        }

        await using var globalLock = await _fileSystem
            .AcquireExclusiveLockAsync(_globalLockPath, cancellationToken)
            .ConfigureAwait(false);
        await using var documentLock = await _fileSystem
            .AcquireExclusiveLockAsync(_lockPath, cancellationToken)
            .ConfigureAwait(false);

        var directory = Path.GetDirectoryName(_primaryPath)!;
        _fileSystem.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{_policy.FileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            if (_fileSystem.FileExists(_primaryPath))
            {
                var existing = await ReadAndValidateAsync(_primaryPath, cancellationToken).ConfigureAwait(false);
                if (existing.Status == ReadStatus.NewerSchema)
                {
                    return new DocumentSaveResult(
                        DocumentSaveStatus.Failed,
                        "Refusing to overwrite a document created by a newer NovaLauncher schema.");
                }

                if (existing.Status != ReadStatus.Valid)
                {
                    PreserveInvalidPrimary();
                }
            }

            await _fileSystem.WriteAllBytesDurableAsync(temporaryPath, content, cancellationToken).ConfigureAwait(false);
            var staged = await ReadAndValidateAsync(temporaryPath, cancellationToken).ConfigureAwait(false);
            if (staged.Status != ReadStatus.Valid)
            {
                return new DocumentSaveResult(DocumentSaveStatus.Failed, "Staged document validation failed.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (_fileSystem.FileExists(_primaryPath) &&
                (await ReadAndValidateAsync(_primaryPath, cancellationToken).ConfigureAwait(false)).Status == ReadStatus.Valid)
            {
                _fileSystem.ReplaceFile(temporaryPath, _primaryPath, _backupPath);
            }
            else
            {
                if (_fileSystem.FileExists(_primaryPath))
                {
                    _fileSystem.DeleteFile(_primaryPath);
                }

                _fileSystem.MoveFile(temporaryPath, _primaryPath, overwrite: false);
            }

            return new DocumentSaveResult(DocumentSaveStatus.Saved, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new DocumentSaveResult(DocumentSaveStatus.Failed, $"Persistence failed: {exception.Message}");
        }
        finally
        {
            if (_fileSystem.FileExists(temporaryPath))
            {
                _fileSystem.DeleteFile(temporaryPath);
            }
        }
    }

    private string? ValidateForCurrentSchema(TDocument document)
    {
        if (document.SchemaVersion != _policy.CurrentSchemaVersion)
        {
            return $"Expected schema {_policy.CurrentSchemaVersion}, received {document.SchemaVersion}.";
        }

        return _policy.Validate(document);
    }

    private async Task<ReadResult> ReadAndValidateAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await _fileSystem.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0 || bytes.Length > MaximumDocumentBytes)
            {
                return new ReadResult(ReadStatus.Invalid, null, null);
            }

            var document = JsonSerializer.Deserialize(bytes, _jsonTypeInfo);
            if (document is null)
            {
                return new ReadResult(ReadStatus.Invalid, null, null);
            }

            if (document.SchemaVersion > _policy.CurrentSchemaVersion)
            {
                return new ReadResult(ReadStatus.NewerSchema, null, document.SchemaVersion);
            }

            return ValidateForCurrentSchema(document) is null
                ? new ReadResult(ReadStatus.Valid, document, document.SchemaVersion)
                : new ReadResult(ReadStatus.Invalid, null, document.SchemaVersion);
        }
        catch (JsonException)
        {
            return await TryReadLegacyGamesDocumentAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            return new ReadResult(ReadStatus.Invalid, null, null);
        }
    }

    private async Task<ReadResult> TryReadLegacyGamesDocumentAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (typeof(TDocument) != typeof(GamesDocument))
        {
            return new ReadResult(ReadStatus.Invalid, null, null);
        }

        try
        {
            var bytes = await _fileSystem.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var games = JsonSerializer.Deserialize(bytes, PersistenceJsonContext.Default.ListLibraryItem);
            if (games is null)
            {
                return new ReadResult(ReadStatus.Invalid, null, null);
            }

            var legacyDocument = new GamesDocument(GamesDocument.CurrentSchemaVersion, games);
            if (new GamesDocumentPolicy().Validate(legacyDocument) is not null)
            {
                return new ReadResult(ReadStatus.Invalid, null, null);
            }

            return new ReadResult(ReadStatus.LegacyValid, (TDocument)(object)legacyDocument, 0);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return new ReadResult(ReadStatus.Invalid, null, null);
        }
    }

    private void PreserveInvalidPrimary()
    {
        var timestamp = _timeProvider.GetUtcNow().ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture);
        var invalidPath = $"{_primaryPath}.invalid-{timestamp}";
        _fileSystem.CopyFile(_primaryPath, invalidPath, overwrite: false);
    }

    private enum ReadStatus
    {
        Valid,
        Invalid,
        NewerSchema,
        LegacyValid,
    }

    private sealed record ReadResult(ReadStatus Status, TDocument? Document, int? SchemaVersion);
}
