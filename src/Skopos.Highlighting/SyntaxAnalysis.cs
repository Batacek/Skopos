namespace Skopos.Highlighting;

/// <summary>
/// A highlighted span. Offsets are in UTF-16 units, so they index straight into a C# string
/// and into an AvaloniaEdit document — that is how the tree-sitter binding returns them.
/// </summary>
public readonly record struct HighlightSpan(int Start, int End, HighlightCategory Category)
{
    public int Length => End - Start;
}

/// <summary>A foldable range.</summary>
public readonly record struct FoldRegion(int Start, int End, string Preview);

/// <summary>The result of a single pass — colours and folds from the same tree.</summary>
public sealed record SyntaxAnalysis(IReadOnlyList<HighlightSpan> Spans, IReadOnlyList<FoldRegion> Folds)
{
    public static readonly SyntaxAnalysis Empty = new([], []);
}
