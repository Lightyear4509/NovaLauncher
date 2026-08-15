using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Enrichment;

public static class MetadataMerger
{
    public static GameMetadata Merge(GameMetadata current, IReadOnlyList<MetadataSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(snapshots);
        return new GameMetadata(
            MergeValue(current.Description, snapshots, static item => NormalizeText(item.Description)),
            MergeValue<IReadOnlyList<string>>(current.Genres, snapshots, static item => NormalizeList(item.Genres)),
            MergeValue<IReadOnlyList<string>>(current.Developers, snapshots, static item => NormalizeList(item.Developers)),
            MergeValue<IReadOnlyList<string>>(current.Publishers, snapshots, static item => NormalizeList(item.Publishers)),
            MergeStructValue(current.ReleaseDate, snapshots, static item => item.ReleaseDate),
            MergeStructValue(current.Rating, snapshots, static item => NormalizeRating(item.Rating)));
    }

    private static MetadataValue<T>? MergeValue<T>(
        MetadataValue<T>? current,
        IReadOnlyList<MetadataSnapshot> snapshots,
        Func<MetadataSnapshot, T?> selector)
    {
        if (current?.Provenance.IsManual == true)
        {
            return current;
        }

        foreach (var snapshot in snapshots)
        {
            var value = selector(snapshot);
            if (value is not null)
            {
                return new MetadataValue<T>(
                    value,
                    new MetadataProvenance(snapshot.ProviderId, snapshot.ProviderItemId, snapshot.RetrievedAtUtc, false));
            }
        }

        return current;
    }

    private static MetadataValue<T>? MergeStructValue<T>(
        MetadataValue<T>? current,
        IReadOnlyList<MetadataSnapshot> snapshots,
        Func<MetadataSnapshot, T?> selector)
        where T : struct
    {
        if (current?.Provenance.IsManual == true)
        {
            return current;
        }

        foreach (var snapshot in snapshots)
        {
            var value = selector(snapshot);
            if (value.HasValue)
            {
                return new MetadataValue<T>(
                    value.Value,
                    new MetadataProvenance(snapshot.ProviderId, snapshot.ProviderItemId, snapshot.RetrievedAtUtc, false));
            }
        }

        return current;
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[]? NormalizeList(IReadOnlyList<string>? values)
    {
        var normalized = values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToArray();
        return normalized is { Length: > 0 } ? normalized : null;
    }

    private static decimal? NormalizeRating(decimal? rating) =>
        rating is >= 0 and <= 100 ? decimal.Round(rating.Value, 2) : null;
}
