using Avalonia;
using Avalonia.Media;
using NovaLauncher.Application.Themes;
using Avalonia.Threading;

namespace NovaLauncher.App;

public sealed class AvaloniaThemeHost : IThemeHost
{
    private static readonly Dictionary<string, Palette> Palettes = new(StringComparer.Ordinal)
    {
        ["nova-dark"] = new("#0C1017", "#121821", "#18202B", "#293340", "#F2F5F8", "#98A4B3", "#4C8DFF", "#6BA2FF", "#4CCB83", "#E6B450", "#F06472", "#E60C1017", "#192A43", "#1B2430", "#253243", "#151D27"),
        ["nova-light"] = new("#F3F5F8", "#FFFFFF", "#E9EDF2", "#C9D0D9", "#18212D", "#5F6B79", "#276FDB", "#4C8DFF", "#168451", "#946200", "#C53F50", "#E8F3F5F8", "#E5EDF9", "#EEF1F5", "#E1E7EE", "#D3DAE3"),
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
        resources["ThemeSurfaceSubtle"] = Brush.Parse(themeId == "nova-light" ? "#F5F7FA" : "#151C26");
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
