namespace NovaLauncher.Application.Persistence;

public enum DocumentLoadStatus
{
    NotFound = 0,
    Loaded = 1,
    RecoveredFromBackup = 2,
    UnsupportedNewerSchema = 3,
    Unrecoverable = 4,
    MigratedLegacy = 5,
}

public sealed record DocumentLoadResult<TDocument>(
    DocumentLoadStatus Status,
    TDocument? Document,
    string? Warning)
    where TDocument : class, IVersionedDocument;

public enum DocumentSaveStatus
{
    Saved = 0,
    Cancelled = 1,
    Failed = 2,
}

public sealed record DocumentSaveResult(
    DocumentSaveStatus Status,
    string? Error);
