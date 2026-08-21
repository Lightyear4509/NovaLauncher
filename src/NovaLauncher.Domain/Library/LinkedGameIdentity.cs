namespace NovaLauncher.Domain.Library;

public sealed record LinkedGameIdentity(
    string ProviderId,
    string ProviderItemId,
    string DisplayName,
    int? ReleaseYear,
    string? SteamAppId,
    DateTimeOffset ConfirmedAtUtc);
