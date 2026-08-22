using Avalonia;
using Avalonia.Media;
using NovaLauncher.Application.Themes;
using Avalonia.Threading;

namespace NovaLauncher.App;

public sealed class AvaloniaThemeHost : IThemeHost
{
    private static readonly Dictionary<string, Palette> Palettes = new(StringComparer.Ordinal)
    {
        ["nova-dark"] = new("#080D1A", "#10182B", "#172442", "#293858", "#F8FAFF", "#AEB9D2", "#52DAFF", "#9B6DFF", "#53D79A", "#F4BF58", "#FF6B7A", "#D9080D1A", "#263E72", "#1C2A48", "#29446E", "#14213B"),
        ["midnight-blue"] = new("#061320", "#0C2135", "#12334F", "#285674", "#F4FAFF", "#B3D2E3", "#48CFFF", "#7D91FF", "#55D5A0", "#F0BE62", "#FF7582", "#D9061320", "#1D4E72", "#1D4765", "#2B668A", "#15374F"),
        ["ember"] = new("#1A0E12", "#2A171C", "#3E2229", "#713D48", "#FFF8F4", "#DFC2BC", "#FF9B68", "#D56DFF", "#6DDAA2", "#F6C45D", "#FF7180", "#D91A0E12", "#67313D", "#673840", "#8B4D58", "#49262D"),
        ["forest"] = new("#081510", "#10241C", "#17362A", "#2B5C49", "#F3FFF9", "#B8D6C8", "#57D69A", "#7EA6FF", "#57D69A", "#F0C05F", "#FF7380", "#D9081510", "#245742", "#285A46", "#37765C", "#1A4031"),
        ["nova-light"] = new("#EEF3FA", "#FFFFFF", "#E0E9F6", "#AEC0D8", "#15213A", "#4A5A73", "#006FA8", "#7052D9", "#147A54", "#936300", "#BC3345", "#E6EEF3FA", "#D4E5F7", "#D7E4F3", "#C4D9EF", "#AEC4DE"),
    };

    public string CurrentThemeId { get; private set; } = "nova-dark";

    public bool ReduceMotion { get; private set; }

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
        resources["ThemeAccentSecondary"] = Brush.Parse(palette.AccentSecondary);
        resources["ThemeSuccess"] = Brush.Parse(palette.Success);
        resources["ThemeWarning"] = Brush.Parse(palette.Warning);
        resources["ThemeDanger"] = Brush.Parse(palette.Danger);
        resources["ThemeOverlay"] = Brush.Parse(palette.Overlay);
        resources["ThemeNavSelected"] = Brush.Parse(palette.NavSelected);
        resources["ThemeButton"] = Brush.Parse(palette.Button);
        resources["ThemeButtonHover"] = Brush.Parse(palette.ButtonHover);
        resources["ThemeButtonPressed"] = Brush.Parse(palette.ButtonPressed);
        resources["ThemeHeroScrim"] = Brush.Parse(palette.Overlay);
        resources["ThemeCardScrim"] = Brush.Parse(palette.Overlay);
        CurrentThemeId = themeId;
        return true;
    }

    public bool ApplyMotionPreference(bool reduceMotion)
    {
        if (!Dispatcher.UIThread.CheckAccess()) return Dispatcher.UIThread.Invoke(() => ApplyMotionPreference(reduceMotion));
        if (Avalonia.Application.Current is null) return false;
        Avalonia.Application.Current.Resources["MotionDuration"] = reduceMotion
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(160);
        ReduceMotion = reduceMotion;
        return true;
    }

    public bool ApplyAccessibility(AccessibilityPreferences preferences)
    {
        if (!Dispatcher.UIThread.CheckAccess()) return Dispatcher.UIThread.Invoke(() => ApplyAccessibility(preferences));
        if (Avalonia.Application.Current is null) return false;
        var resources = Avalonia.Application.Current.Resources;
        resources["InterfaceFontSize"] = 14d * preferences.TextScale;
        resources["FocusBorderThickness"] = new Thickness(2d * preferences.FocusScale);
        resources["ControllerHintsVisible"] = preferences.ShowControllerHints;
        if (preferences.ContrastPreset == "High")
        {
            resources["ThemeText"] = Brushes.White;
            resources["ThemeMuted"] = Brush.Parse("#E5E7EB");
            resources["ThemeBorder"] = Brushes.White;
            resources["ThemeAccent"] = Brush.Parse("#00E5FF");
        }
        else Apply(CurrentThemeId);
        return true;
    }

    private sealed record Palette(
        string Background,
        string Surface,
        string Elevated,
        string Border,
        string Text,
        string Muted,
        string Accent,
        string AccentSecondary,
        string Success,
        string Warning,
        string Danger,
        string Overlay,
        string NavSelected,
        string Button,
        string ButtonHover,
        string ButtonPressed);
}
