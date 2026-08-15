using System.Text.Json;
using System.Text.Json.Serialization;

namespace NovaLauncher.Domain.Library;

[JsonConverter(typeof(GameCollectionIdJsonConverter))]
public readonly record struct GameCollectionId
{
    public GameCollectionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A collection ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static GameCollectionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed class GameCollectionIdJsonConverter : JsonConverter<GameCollectionId>
{
    public override GameCollectionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.TryGetGuid(out var value) && value != Guid.Empty)
            return new GameCollectionId(value);
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("A collection ID must be a GUID string or legacy value object.");
        Guid parsed = Guid.Empty;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Invalid collection ID object.");
            var isValue = reader.ValueTextEquals("value") || reader.ValueTextEquals("Value");
            if (!reader.Read()) throw new JsonException("Incomplete collection ID object.");
            if (isValue && reader.TokenType == JsonTokenType.String) reader.TryGetGuid(out parsed);
            else reader.Skip();
        }
        if (parsed == Guid.Empty) throw new JsonException("A collection ID cannot be empty.");
        return new GameCollectionId(parsed);
    }

    public override void Write(Utf8JsonWriter writer, GameCollectionId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
