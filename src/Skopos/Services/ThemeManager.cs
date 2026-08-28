using Avalonia;
using Avalonia.Styling;
using Skopos.Core.Settings;
using Skopos.Highlighting.Rendering;

namespace Skopos.Services;

/// <summary>
/// Theming. With the "system" choice we react to changes <b>at runtime</b> — the user
/// switches the system to dark and Skopos follows, without a restart.
/// </summary>
internal static class ThemeManager
{
    /// <summary>Raised when the appearance changes — windows should pick up the new palette.</summary>
    public static event Action? PaletteChanged;

    public static HighlightPalette Palette { get; private set; } = HighlightPalette.Light;

    public static void Attach(Application application)
    {
        application.ActualThemeVariantChanged += (_, _) => Refresh(application);
        Refresh(application);
    }

    public static void Apply(Application application, ThemePreference preference)
    {
        application.RequestedThemeVariant = preference switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default, // "Default" means follow the system
        };

        Refresh(application);
    }

    private static void Refresh(Application application)
    {
        var palette = application.ActualThemeVariant == ThemeVariant.Dark
            ? HighlightPalette.Dark
            : HighlightPalette.Light;

        if (ReferenceEquals(palette, Palette)) return;

        Palette = palette;
        PaletteChanged?.Invoke();
    }
}
