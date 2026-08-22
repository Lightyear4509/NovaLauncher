using System.Text.Json;
using System.Text.Json.Serialization;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Library;

public enum LibraryTransferChange { Add, Replace, Skip, Rejected }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LibraryTransferDocument(int SchemaVersion, IReadOnlyList<LibraryTransferEntry> Entries)
{
    public const int CurrentSchemaVersion = 1;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LibraryTransferEntry(
    Guid ExportId,
    string Name,
    string Platform,
    string Source,
    string Target,
    LaunchTargetKind TargetKind,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory,
    bool IsFavorite,
    string? SourceItemId,
    string? Notes,
    IReadOnlyList<string>? Tags);

public sealed record LibraryImportPreviewItem(
    int Index,
    LibraryTransferEntry Entry,
    LibraryTransferChange Change,
    LibraryItem? Candidate,
    GameId? ExistingGameId,
    string Message)
{
    public bool CanImport => Change is LibraryTransferChange.Add or LibraryTransferChange.Replace;
}

public sealed record LibraryImportPreview(long LibraryRevision, IReadOnlyList<LibraryImportPreviewItem> Items)
{
    public int AcceptedCount => Items.Count(static item => item.CanImport);
}

public sealed record LibraryTransferResult(bool Success, int EntryCount, string Message);

public sealed class LibraryPortabilityCoordinator(LibraryCoordinator library, ManualGameDraftValidator validator, TimeProvider timeProvider)
{
    public const long MaximumPayloadBytes = 16 * 1024 * 1024;
    public const int MaximumEntries = 10_000;

    public async Task<LibraryTransferResult> ExportAsync(
        Stream destination,
        IReadOnlyCollection<GameId> selectedGameIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(selectedGameIds);
        if (!destination.CanWrite) return new(false, 0, "Choose a writable export destination.");
        var selected = selectedGameIds.ToHashSet();
        if (selected.Count is 0 or > MaximumEntries) return new(false, 0, "Select between 1 and 10,000 library entries.");
        var entries = library.Games.Where(game => selected.Contains(game.Id)).Select(ToTransferEntry).ToArray();
        if (entries.Length != selected.Count) return new(false, 0, "The library changed before export. Review the selection again.");

        await JsonSerializer.SerializeAsync(
            destination,
            new LibraryTransferDocument(LibraryTransferDocument.CurrentSchemaVersion, entries),
            LibraryPortabilityJsonContext.Default.LibraryTransferDocument,
            cancellationToken).ConfigureAwait(false);
        if (destination.CanSeek && destination.Length > MaximumPayloadBytes)
            return new(false, 0, "The export exceeds the 16 MiB portability limit.");
        return new(true, entries.Length, $"Exported {entries.Length} reviewed library entry or entries without secrets or device-scoped data.");
    }

    public async Task<(LibraryImportPreview? Preview, string? Error)> PreviewImportAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead) return (null, "Choose a readable import file.");
        if (source.CanSeek && source.Length > MaximumPayloadBytes) return (null, "The import exceeds the 16 MiB limit.");
        LibraryTransferDocument? document;
        try
        {
            document = await JsonSerializer.DeserializeAsync(source, LibraryPortabilityJsonContext.Default.LibraryTransferDocument, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return (null, "The import uses an unsupported schema or contains unknown fields.");
        }
        if (document is null || document.SchemaVersion != LibraryTransferDocument.CurrentSchemaVersion || document.Entries is null)
            return (null, "The import schema is unsupported.");
        if (document.Entries.Count > MaximumEntries) return (null, "The import contains more than 10,000 entries.");

        var items = document.Entries.Select((entry, index) => PreviewEntry(entry, index)).ToArray();
        return (new LibraryImportPreview(library.Revision, items), null);
    }

    public Task<LibraryTransferResult> CommitImportAsync(
        LibraryImportPreview preview,
        IReadOnlyCollection<int> acceptedIndexes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(acceptedIndexes);
        var accepted = acceptedIndexes.ToHashSet();
        if (accepted.Any(index => preview.Items.All(item => item.Index != index || !item.CanImport)))
            return Task.FromResult(new LibraryTransferResult(false, 0, "The reviewed import selection is invalid."));
        return library.CommitPortableImportAsync(preview, accepted, cancellationToken);
    }

    private LibraryImportPreviewItem PreviewEntry(LibraryTransferEntry entry, int index)
    {
        var validation = ValidateEntry(entry);
        if (validation is not null) return new(index, entry, LibraryTransferChange.Rejected, null, null, validation);
        var now = timeProvider.GetUtcNow();
        var id = string.Equals(entry.Source, "Steam", StringComparison.OrdinalIgnoreCase)
            ? GameId.FromSteamAppId(uint.Parse(entry.SourceItemId!, System.Globalization.CultureInfo.InvariantCulture))
            : new GameId(entry.ExportId == Guid.Empty ? Guid.NewGuid() : entry.ExportId);
        var candidate = new LibraryItem(
            id, entry.Name.Trim(), entry.Platform.Trim(), entry.Source,
            new LaunchTarget(entry.Target, entry.Arguments.ToArray(), entry.WorkingDirectory, entry.TargetKind),
            new GameMetadata(null, null, null, null, null, null), entry.IsFavorite, now, now,
            entry.SourceItemId, entry.Name, Notes: NormalizeNotes(entry.Notes), Tags: NormalizeTags(entry.Tags));
        var existing = FindDuplicate(entry);
        if (existing is null) return new(index, entry, LibraryTransferChange.Add, candidate, null, "Ready to add after review.");
        var unchanged = existing.Name == candidate.Name && existing.Platform == candidate.Platform &&
            existing.LaunchTarget == candidate.LaunchTarget && existing.Notes == candidate.Notes &&
            (existing.Tags ?? []).SequenceEqual(candidate.Tags ?? [], StringComparer.OrdinalIgnoreCase);
        return unchanged
            ? new(index, entry, LibraryTransferChange.Skip, candidate, existing.Id, "An equivalent library entry already exists.")
            : new(index, entry, LibraryTransferChange.Replace, candidate, existing.Id, "Review changes before replacing the matching entry.");
    }

    private string? ValidateEntry(LibraryTransferEntry entry)
    {
        if (entry.ExportId == Guid.Empty || entry.Name is not { Length: > 0 and <= 500 } || entry.Platform is not { Length: > 0 and <= 100 } ||
            entry.Notes is { Length: > 50_000 } || entry.Tags is { Count: > 100 } || entry.Tags?.Any(static tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 100) == true)
            return "The entry contains missing or oversized fields.";
        if (entry.Source is not ("Manual" or "Steam")) return "Only manual and Steam library entries are supported.";
        var draft = new ManualGameDraft(entry.Name, entry.Platform, entry.Target, entry.Arguments, entry.WorkingDirectory, entry.TargetKind);
        var draftValidation = validator.Validate(draft);
        if (!draftValidation.IsValid) return string.Join(" ", draftValidation.Errors.Values);
        if (entry.TargetKind == LaunchTargetKind.Executable && !File.Exists(entry.Target))
            return "The executable target is not present on this device; the entry cannot launch safely.";
        if (entry.Source == "Steam" && (!uint.TryParse(entry.SourceItemId, out var appId) || appId == 0 ||
            entry.TargetKind != LaunchTargetKind.Uri || entry.Target != $"steam://run/{appId}"))
            return "The Steam identity and launch URI are inconsistent.";
        return null;
    }

    private LibraryItem? FindDuplicate(LibraryTransferEntry entry) => entry.Source == "Steam"
        ? library.Games.FirstOrDefault(game => game.Source == "Steam" && game.SourceItemId == entry.SourceItemId)
        : library.Games.FirstOrDefault(game => game.Source == "Manual" && game.LaunchTarget.Kind == entry.TargetKind &&
            string.Equals(game.LaunchTarget.Target, entry.Target, StringComparison.OrdinalIgnoreCase));

    private static LibraryTransferEntry ToTransferEntry(LibraryItem game) => new(
        game.Id.Value, game.Name, game.Platform, game.Source, game.LaunchTarget.Target, game.LaunchTarget.Kind,
        game.LaunchTarget.Arguments, game.LaunchTarget.WorkingDirectory, game.IsFavorite, game.SourceItemId,
        game.Notes, game.Tags);

    private static string? NormalizeNotes(string? notes) => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    private static string[]? NormalizeTags(IReadOnlyList<string>? tags)
    {
        var normalized = (tags ?? []).Select(static tag => tag.Trim()).Where(static tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return normalized.Length == 0 ? null : normalized;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(LibraryTransferDocument))]
internal sealed partial class LibraryPortabilityJsonContext : JsonSerializerContext;
