namespace NovaLauncher.Domain.Library;

public sealed record MetadataValue<T>(
    T Value,
    MetadataProvenance Provenance);

public sealed record MetadataProvenance(
    string Source,
    string? SourceItemId,
    DateTimeOffset RetrievedAtUtc,
    bool IsManual);

public sealed record GameMetadata(
    MetadataValue<string>? Description,
    MetadataValue<IReadOnlyList<string>>? Genres,
    MetadataValue<IReadOnlyList<string>>? Developers,
    MetadataValue<IReadOnlyList<string>>? Publishers,
    MetadataValue<DateOnly>? ReleaseDate,
    MetadataValue<decimal>? Rating);
