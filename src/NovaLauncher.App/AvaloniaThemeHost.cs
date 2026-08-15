using Avalonia;
using Avalonia.Media;
using NovaLauncher.Application.Themes;
using Avalonia.Threading;

namespace NovaLauncher.App;

public sealed class AvaloniaThemeHost : IThemeHost
{
    private static readonly Dictionary<string, Palette> Palettes = new(StringComparer.Ordinal)
    {
        ["nova-dark"] = new("#0B1020", "#111831", "#172448", "#253760", "#F7F8FC", "#B8C1DF", "#63D8FF", "#253760", "#36558C", "#182A50"),
        ["midnight-blue"] = new("#071525", "#0C223B", "#123454", "#24577E", "#F3FAFF", "#B6D5EA", "#42C8FF", "#24577E", "#326F9D", "#163C5A"),
        ["ember"] = new("#1B1012", "#2B1719", "#402124", "#71383B", "#FFF7F2", "#E4C2B8", "#FF9A62", "#71383B", "#965052", "#51272A"),
        ["forest"] = new("#091612", "#10251D", "#17372B", "#286049", "#F2FFF8", "#B7D8C7", "#58D69A", "#286049", "#347A5C", "#193E30"),
        ["nova-light"] = new("#EEF3FA", "#FFFFFF", "#DCE7F5", "#A9BEDA", "#15213A", "#465873", "#006EA8", "#D5E3F4", "#BDD3EC", "#A9BEDA"),
    };

    public string CurrentThemeId { get; private set; } = "nova-dark";

    public bool Apply(string themeId)
    {
        if (!Dispatcher.UIThread.CheckAccess()) return Dispatcher.UIThread.Invoke(() => Apply(themeId));
        if (!Palettes.TryGetValue(themeId, out var palette) || Avalonia.Application.Current is null) return false;
        var resources = Avalonia.Application.Current.Resources;
        resources["ThemeBackground"] = Brush.Parse(palette.Background);
        resources["ThemeSurface"] = Brush.Parse(palette.Surface);
        resources["ThemeElevated"] = Brush.Parse(palette.Elevated);
        resources["ThemeBorder"] = Brush.Parse(palette.Border);
        resources["ThemeText"] = Brush.Parse(palette.Text);
        resources["ThemeMuted"] = Brush.Parse(palette.Muted);
        resources["ThemeAccent"] = Brush.Parse(palette.Accent);
        resources["ThemeButton"] = Brush.Parse(palette.Button);
        resources["ThemeButtonHover"] = Brush.Parse(palette.ButtonHover);
        resources["ThemeButtonPressed"] = Brush.Parse(palette.ButtonPressed);
        CurrentThemeId = themeId;
        return true;
    }

    private sealed record Palette(string Background, string Surface, string Elevated, string Border, string Text, string Muted, string Accent, string Button, string ButtonHover, string ButtonPressed);
}
