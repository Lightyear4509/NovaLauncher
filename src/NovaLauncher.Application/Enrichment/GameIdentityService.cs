using System.Globalization;
using System.Text;
using NovaLauncher.Application.Library;
using NovaLauncher.Application.Steam;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Enrichment;

public sealed record GameIdentityCandidate(
    string ProviderId,
    string ProviderItemId,
    string DisplayName,
    int? ReleaseYear,
    string? SteamAppId,
    string MatchReason);

public sealed record GameIdentitySearchResult(
    IReadOnlyList<GameIdentityCandidate> Candidates,
    IReadOnlyList<string> Failures,
    string SuggestedName);

public interface IGameIdentitySearchProvider
{
    string Id { get; }

    Task<(IReadOnlyList<GameIdentityCandidate> Candidates, string? Error)> SearchAsync(
        string normalizedQuery,
        string displayQuery,
        CancellationToken cancellationToken);
}

public interface IGameIdentityService
{
    string SuggestName(string executablePath);

    Task<GameIdentitySearchResult> SearchAsync(LibraryItem game, string query, CancellationToken cancellationToken);

    Task<LibraryMutationResult> ConfirmAsync(GameId gameId, GameIdentityCandidate candidate, CancellationToken cancellationToken);

    Task<LibraryMutationResult> UnlinkAsync(GameId gameId, CancellationToken cancellationToken);
}

public sealed class GameIdentityService(
    ISteamCatalogSource steamCatalog,
    IEnumerable<IGameIdentitySearchProvider> providers,
    LibraryCoordinator library,
    TimeProvider timeProvider) : IGameIdentityService
{
    private const int MaximumCandidates = 20;

    public string SuggestName(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return string.Empty;
        var stem = Path.GetFileNameWithoutExtension(executablePath).Trim();
        if (stem.Length == 0) return string.Empty;
        var builder = new StringBuilder(stem.Length + 8);
        for (var index = 0; index < stem.Length; index++)
        {
            var character = stem[index];
            if (character is '_' or '-' or '.') { builder.Append(' '); continue; }
            if (index > 0 && char.IsUpper(character) && char.IsLower(stem[index - 1])) builder.Append(' ');
            builder.Append(character);
        }
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public async Task<GameIdentitySearchResult> SearchAsync(LibraryItem game, string query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!string.Equals(game.Source, "Manual", StringComparison.OrdinalIgnoreCase))
            return new([], ["Identity matching is available only for manually added games."], game.Name);
        var displayQuery = string.IsNullOrWhiteSpace(query) ? game.Name.Trim() : query.Trim();
        if (displayQuery.Length is < 2 or > 200)
            return new([], ["Enter a search title between 2 and 200 characters."], displayQuery);
        var normalized = Normalize(displayQuery);
        if (normalized.Length < 2) return new([], ["The search title contains too few searchable characters."], displayQuery);

        var candidates = new List<GameIdentityCandidate>();
        var failures = new List<string>();
        try
        {
            var scan = await steamCatalog.ScanAsync(null, cancellationToken).ConfigureAwait(false);
            failures.AddRange(scan.Failures.Take(5).Select(static failure => $"Steam: {failure.Reason}"));
            candidates.AddRange(scan.Games
                .Select(item => (Item: item, Normalized: Normalize(item.Name)))
                .Where(item => item.Normalized == normalized || item.Normalized.Contains(normalized, StringComparison.Ordinal) || normalized.Contains(item.Normalized, StringComparison.Ordinal))
                .OrderByDescending(item => item.Normalized == normalized)
                .ThenBy(item => item.Item.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaximumCandidates)
                .Select(item => new GameIdentityCandidate(
                    "Steam",
                    item.Item.AppId.ToString(CultureInfo.InvariantCulture),
                    item.Item.Name,
                    null,
                    item.Item.AppId.ToString(CultureInfo.InvariantCulture),
                    item.Normalized == normalized ? "Exact normalized title in local Steam library" : "Similar title in local Steam library")));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failures.Add($"Steam discovery: {exception.Message}");
        }

        foreach (var provider in providers.OrderBy(static provider => provider.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await provider.SearchAsync(normalized, displayQuery, cancellationToken).ConfigureAwait(false);
            candidates.AddRange(result.Candidates);
            if (result.Error is not null) failures.Add($"{provider.Id}: {result.Error}");
        }

        var distinct = candidates
            .GroupBy(static candidate => $"{candidate.ProviderId}:{candidate.ProviderItemId}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .Take(MaximumCandidates)
            .ToArray();
        return new(distinct, failures.Take(10).ToArray(), displayQuery);
    }

    public Task<LibraryMutationResult> ConfirmAsync(GameId gameId, GameIdentityCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var identity = new LinkedGameIdentity(
            candidate.ProviderId,
            candidate.ProviderItemId,
            candidate.DisplayName,
            candidate.ReleaseYear,
            candidate.SteamAppId,
            timeProvider.GetUtcNow());
        return library.SetLinkedIdentityAsync(gameId, identity, cancellationToken);
    }

    public Task<LibraryMutationResult> UnlinkAsync(GameId gameId, CancellationToken cancellationToken) =>
        library.SetLinkedIdentityAsync(gameId, null, cancellationToken);

    public static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }
}
