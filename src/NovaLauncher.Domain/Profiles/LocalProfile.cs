namespace NovaLauncher.Domain.Profiles;

public sealed record LocalProfile(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string>? DiscoveryLocations = null,
    IReadOnlyList<string>? IgnoredPaths = null);

public static class LocalProfileDefaults
{
    public static Guid DefaultProfileId { get; } = new("4f342c1a-4d88-5c61-9fc6-84bb23b29a35");
}
