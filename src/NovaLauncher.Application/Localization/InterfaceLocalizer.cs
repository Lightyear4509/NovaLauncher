namespace NovaLauncher.Application.Localization;

public enum InterfaceText { Home, Library, Saves, Settings, AccessibilityApplied }

public static class InterfaceLocalizer
{
    public const string ReviewedCulture = "en-US";
    internal const string PseudoCulture = "en-XA";
    private static readonly Dictionary<InterfaceText, string> English = new()
    {
        [InterfaceText.Home] = "Home",
        [InterfaceText.Library] = "Library",
        [InterfaceText.Saves] = "Saves",
        [InterfaceText.Settings] = "Settings",
        [InterfaceText.AccessibilityApplied] = "Accessibility preferences applied.",
    };
    public static IReadOnlyList<string> ReviewedCultures { get; } = [ReviewedCulture];
    public static string Get(InterfaceText key, string culture)
    {
        if (!English.TryGetValue(key, out var value)) throw new ArgumentOutOfRangeException(nameof(key));
        return culture == PseudoCulture ? $"⟦{Expand(value)}⟧" : value;
    }
    private static string Expand(string value) => string.Concat(value.Select(static character => character switch
    {
        'a' or 'A' or 'e' or 'E' or 'i' or 'I' or 'o' or 'O' or 'u' or 'U' => character + character.ToString().ToLowerInvariant(),
        _ => character.ToString(),
    }));
}
