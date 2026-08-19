using Avalonia.Media;

namespace Skopos.Highlighting.Rendering;

/// <summary>How a category renders: colour and optionally a font style.</summary>
public readonly record struct CategoryStyle(Color Foreground, bool Italic = false, bool Bold = false);

/// <summary>
/// Colour palette for the highlight categories, separately for the light and dark theme.
/// The shades derive from GitHub Light/Dark — well tuned and free of licensing conflicts.
/// </summary>
public sealed class HighlightPalette
{
    private readonly CategoryStyle[] _styles;

    private HighlightPalette(CategoryStyle[] styles, Color background, Color foreground, Color lineNumber, Color selection, Color currentLine)
    {
        _styles = styles;
        Background = background;
        Foreground = foreground;
        LineNumber = lineNumber;
        Selection = selection;
        CurrentLine = currentLine;
    }

    public Color Background { get; }
    public Color Foreground { get; }
    public Color LineNumber { get; }
    public Color Selection { get; }
    public Color CurrentLine { get; }

    public CategoryStyle this[HighlightCategory category] => _styles[(int)category];

    public static readonly HighlightPalette Light = Build(
        background: 0xFFFFFF,
        foreground: 0x1F2328,
        lineNumber: 0x8C959F,
        selection: 0xB6D6FD,
        currentLine: 0xF6F8FA,
        keyword: 0xCF222E,
        text: 0x0A3069,
        number: 0x0550AE,
        comment: 0x6E7781,
        function: 0x8250DF,
        type: 0x953800,
        variable: 0x1F2328,
        property: 0x0550AE,
        constant: 0x0550AE,
        @operator: 0xCF222E,
        punctuation: 0x6E7781,
        heading: 0x0550AE,
        link: 0x0A3069);

    public static readonly HighlightPalette Dark = Build(
        background: 0x0D1117,
        foreground: 0xE6EDF3,
        lineNumber: 0x6E7681,
        selection: 0x264F78,
        currentLine: 0x161B22,
        keyword: 0xFF7B72,
        text: 0xA5D6FF,
        number: 0x79C0FF,
        comment: 0x8B949E,
        function: 0xD2A8FF,
        type: 0xFFA657,
        variable: 0xE6EDF3,
        property: 0x79C0FF,
        constant: 0x79C0FF,
        @operator: 0xFF7B72,
        punctuation: 0x8B949E,
        heading: 0x79C0FF,
        link: 0xA5D6FF);

    private static HighlightPalette Build(
        uint background, uint foreground, uint lineNumber, uint selection, uint currentLine,
        uint keyword, uint text, uint number, uint comment, uint function, uint type,
        uint variable, uint property, uint constant, uint @operator, uint punctuation,
        uint heading, uint link)
    {
        var styles = new CategoryStyle[Enum.GetValues<HighlightCategory>().Length];
        styles[(int)HighlightCategory.None] = new CategoryStyle(Rgb(foreground));
        styles[(int)HighlightCategory.Keyword] = new CategoryStyle(Rgb(keyword));
        styles[(int)HighlightCategory.String] = new CategoryStyle(Rgb(text));
        styles[(int)HighlightCategory.Number] = new CategoryStyle(Rgb(number));
        styles[(int)HighlightCategory.Comment] = new CategoryStyle(Rgb(comment), Italic: true);
        styles[(int)HighlightCategory.Function] = new CategoryStyle(Rgb(function));
        styles[(int)HighlightCategory.Type] = new CategoryStyle(Rgb(type));
        styles[(int)HighlightCategory.Variable] = new CategoryStyle(Rgb(variable));
        styles[(int)HighlightCategory.Property] = new CategoryStyle(Rgb(property));
        styles[(int)HighlightCategory.Constant] = new CategoryStyle(Rgb(constant));
        styles[(int)HighlightCategory.Operator] = new CategoryStyle(Rgb(@operator));
        styles[(int)HighlightCategory.Punctuation] = new CategoryStyle(Rgb(punctuation));
        styles[(int)HighlightCategory.Heading] = new CategoryStyle(Rgb(heading), Bold: true);
        styles[(int)HighlightCategory.Emphasis] = new CategoryStyle(Rgb(foreground), Italic: true);
        styles[(int)HighlightCategory.Strong] = new CategoryStyle(Rgb(foreground), Bold: true);
        styles[(int)HighlightCategory.Link] = new CategoryStyle(Rgb(link));

        return new HighlightPalette(styles, Rgb(background), Rgb(foreground), Rgb(lineNumber), Rgb(selection), Rgb(currentLine));
    }

    private static Color Rgb(uint value) =>
        Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
}
