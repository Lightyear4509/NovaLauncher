using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NovaLauncher.Domain.Library;

[JsonConverter(typeof(GameIdJsonConverter))]
public readonly record struct GameId
{
    public GameId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A game ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static GameId New() => new(Guid.NewGuid());

    public static GameId FromSteamAppId(uint appId)
    {
        var identity = Encoding.UTF8.GetBytes("novalauncher:steam:" + appId.ToString(CultureInfo.InvariantCulture));
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(identity, hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new GameId(new Guid(hash[..16]));
    }

    public override string ToString() => Value.ToString("D");

    internal static GameId RecoverLegacyEmpty() => FromStableText("novalauncher:legacy-empty-game-id");

    private static GameId FromStableText(string value)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(value), hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new GameId(new Guid(hash[..16]));
    }
}

public sealed class GameIdJsonConverter : JsonConverter<GameId>
{
    public override GameId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Guid value;
        if (reader.TokenType == JsonTokenType.String && reader.TryGetGuid(out value)) return Create(value);
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("A game ID must be a GUID string or legacy value object.");
        value = Guid.Empty;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Invalid game ID object.");
            var isValue = reader.ValueTextEquals("value") || reader.ValueTextEquals("Value");
            if (!reader.Read()) throw new JsonException("Incomplete game ID object.");
            if (isValue && reader.TokenType == JsonTokenType.String && reader.TryGetGuid(out var parsed)) value = parsed;
            else reader.Skip();
        }
        return Create(value);
    }

    public override void Write(Utf8JsonWriter writer, GameId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);

    private static GameId Create(Guid value) => value == Guid.Empty ? GameId.RecoverLegacyEmpty() : new GameId(value);
}
