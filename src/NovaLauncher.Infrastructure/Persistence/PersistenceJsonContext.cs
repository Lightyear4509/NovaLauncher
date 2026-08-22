using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Infrastructure.Persistence;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(GamesDocument))]
[JsonSerializable(typeof(CollectionsDocument))]
[JsonSerializable(typeof(SettingsDocument))]
[JsonSerializable(typeof(AchievementsDocument))]
[JsonSerializable(typeof(SaveSyncDocument))]
[JsonSerializable(typeof(ProfilesDocument))]
[JsonSerializable(typeof(List<LibraryItem>))]
[ExcludeFromCodeCoverage(Justification = "System.Text.Json source generation supplies the executable members.")]
internal sealed partial class PersistenceJsonContext : JsonSerializerContext;
