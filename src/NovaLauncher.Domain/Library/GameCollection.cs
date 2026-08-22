using System.Diagnostics.CodeAnalysis;

namespace NovaLauncher.Domain.Library;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Collection is the established product-domain term, not an implementation collection type.")]
public sealed record GameCollection(
    GameCollectionId Id,
    string Name,
    IReadOnlyList<GameId> GameIds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    Guid? ProfileId = null);
