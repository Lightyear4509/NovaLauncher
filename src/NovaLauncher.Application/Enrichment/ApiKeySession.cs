namespace NovaLauncher.Application.Enrichment;

public interface IApiKeySession
{
    bool HasSteamGridDbKey { get; }

    string? GetSteamGridDbKey();

    void SetSteamGridDbKey(string? value);
}

public sealed class ApiKeySession(string? initialSteamGridDbKey) : IApiKeySession
{
    private string? _steamGridDbKey = Normalize(initialSteamGridDbKey);

    public bool HasSteamGridDbKey => _steamGridDbKey is not null;

    public string? GetSteamGridDbKey() => _steamGridDbKey;

    public void SetSteamGridDbKey(string? value) => _steamGridDbKey = Normalize(value);

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > 512 ? null : trimmed;
    }
}
