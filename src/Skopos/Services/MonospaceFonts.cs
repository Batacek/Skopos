using Avalonia.Media;

namespace Skopos.Services;

/// <summary>
/// Only monospace fonts are offered. Enumerating system fonts is slow, so it must not happen
/// before the first window — the list is filled only when settings are opened.
/// </summary>
internal static class MonospaceFonts
{
    /// <summary>
    /// Chain of preferred fonts. We do not bundle a font of our own, so we rely on whatever
    /// the platform provides.
    /// The last entry is a generic family that Avalonia resolves on its own.
    /// </summary>
    public const string DefaultFamily =
        "JetBrains Mono, Cascadia Code, Cascadia Mono, Consolas, SF Mono, Menlo, DejaVu Sans Mono, Liberation Mono, monospace";

    /// <summary>Known monospace families we want to list if they happen to be installed.</summary>
    private static readonly string[] KnownMonospace =
    [
        "JetBrains Mono", "Cascadia Code", "Cascadia Mono", "Consolas", "Courier New",
        "DejaVu Sans Mono", "Fira Code", "Hack", "IBM Plex Mono", "Iosevka",
        "Liberation Mono", "Lucida Console", "Menlo", "Monaco", "Noto Sans Mono",
        "Roboto Mono", "SF Mono", "Source Code Pro", "Ubuntu Mono",
    ];

    /// <summary>
    /// Walks the system fonts and returns the monospace ones. Call this only once the user opens
    /// settings — enumeration is an order of magnitude slower than anything else in startup.
    /// </summary>
    public static IReadOnlyList<string> Enumerate()
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var family in FontManager.Current.SystemFonts)
                installed.Add(family.Name);
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException)
        {
            // Life without the list is fine — the user can type the family by hand.
        }

        var result = KnownMonospace.Where(installed.Contains).ToList();
        result.Sort(StringComparer.CurrentCulture);
        return result;
    }

    /// <summary>An empty value in settings means "pick per platform".</summary>
    public static FontFamily Resolve(string? family) =>
        string.IsNullOrWhiteSpace(family) ? new FontFamily(DefaultFamily) : new FontFamily(family);
}
