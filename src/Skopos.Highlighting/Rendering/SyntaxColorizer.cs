using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Skopos.Highlighting.Rendering;

/// <summary>
/// Carries the spans from <see cref="SyntaxAnalysis"/> over to colours in the editor.
/// AvaloniaEdit calls <see cref="ColorizeLine"/> only for visible lines, so colouring costs
/// time proportional to the window, not to the size of the file.
/// </summary>
public sealed class SyntaxColorizer : DocumentColorizingTransformer
{
    private readonly Dictionary<HighlightCategory, IBrush> _brushes = [];
    private IReadOnlyList<HighlightSpan> _spans = [];
    private HighlightPalette _palette = HighlightPalette.Light;

    public void SetPalette(HighlightPalette palette)
    {
        _palette = palette;
        _brushes.Clear();
    }

    public void SetSpans(IReadOnlyList<HighlightSpan> spans) => _spans = spans;

    public void Clear() => _spans = [];

    protected override void ColorizeLine(DocumentLine line)
    {
        if (_spans.Count == 0 || line.Length == 0) return;

        var lineStart = line.Offset;
        var lineEnd = line.EndOffset;

        for (var i = FindFirstSpan(lineStart); i < _spans.Count; i++)
        {
            var span = _spans[i];
            if (span.Start >= lineEnd) break;
            if (span.End <= lineStart) continue;

            var from = Math.Max(span.Start, lineStart);
            var to = Math.Min(span.End, lineEnd);
            if (to <= from) continue;

            var style = _palette[span.Category];
            var brush = GetBrush(span.Category, style.Foreground);

            ChangeLinePart(from, to, element =>
            {
                element.TextRunProperties.SetForegroundBrush(brush);

                if (style.Italic || style.Bold)
                {
                    var typeface = element.TextRunProperties.Typeface;
                    element.TextRunProperties.SetTypeface(new Typeface(
                        typeface.FontFamily,
                        style.Italic ? FontStyle.Italic : typeface.Style,
                        style.Bold ? FontWeight.Bold : typeface.Weight));
                }
            });
        }
    }

    /// <summary>Spans are sorted by start, so a binary search finds the first relevant one.</summary>
    private int FindFirstSpan(int offset)
    {
        var low = 0;
        var high = _spans.Count - 1;
        var result = _spans.Count;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (_spans[mid].End > offset)
            {
                result = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return result;
    }

    private IBrush GetBrush(HighlightCategory category, Color color)
    {
        if (_brushes.TryGetValue(category, out var brush)) return brush;

        brush = new ImmutableSolidColorBrush(color);
        _brushes[category] = brush;
        return brush;
    }
}
