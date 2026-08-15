namespace NovaLauncher.Application.Enrichment;

public enum CacheLookupStatus
{
    Miss,
    Fresh,
    Stale,
}

public sealed record CacheLookup<T>(CacheLookupStatus Status, T? Value);

public sealed class ProviderCache<T>(TimeProvider timeProvider, TimeSpan freshness, TimeSpan retention, int maximumEntries)
    where T : class
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public CacheLookup<T> Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                return new CacheLookup<T>(CacheLookupStatus.Miss, null);
            }

            var age = timeProvider.GetUtcNow() - entry.StoredAtUtc;
            if (age > retention)
            {
                _entries.Remove(key);
                return new CacheLookup<T>(CacheLookupStatus.Miss, null);
            }

            entry.LastAccessUtc = timeProvider.GetUtcNow();
            return new CacheLookup<T>(age <= freshness ? CacheLookupStatus.Fresh : CacheLookupStatus.Stale, entry.Value);
        }
    }

    public void Set(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            _entries[key] = new Entry(value, now, now);
            while (_entries.Count > maximumEntries)
            {
                var oldest = _entries.MinBy(static pair => pair.Value.LastAccessUtc).Key;
                _entries.Remove(oldest);
            }
        }
    }

    private sealed class Entry(T value, DateTimeOffset storedAtUtc, DateTimeOffset lastAccessUtc)
    {
        public T Value { get; } = value;

        public DateTimeOffset StoredAtUtc { get; } = storedAtUtc;

        public DateTimeOffset LastAccessUtc { get; set; } = lastAccessUtc;
    }
}
