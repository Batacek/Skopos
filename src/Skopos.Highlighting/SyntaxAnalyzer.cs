using TreeSitter;
using TsRange = TreeSitter.Range;

namespace Skopos.Highlighting;

/// <summary>
/// Wires tree-sitter up to categories and foldable ranges. An instance holds the loaded
/// grammar and the compiled query, so a second file of the same language comes for free.
/// </summary>
public sealed class SyntaxAnalyzer : IDisposable
{
    /// <summary>
    /// Captures from the inline pass rank behind every block capture, so on an identical range
    /// the block grammar wins. No single query will ever hold this many patterns.
    /// </summary>
    private const int InlinePriorityOffset = 1_000_000;

    private readonly LanguageDefinition _definition;
    private readonly Language _language;
    private readonly Query _query;
    private readonly SyntaxAnalyzer? _inline;

    private SyntaxAnalyzer(LanguageDefinition definition, Language language, Query query, SyntaxAnalyzer? inline)
    {
        _definition = definition;
        _language = language;
        _query = query;
        _inline = inline;
    }

    public LanguageDefinition Definition => _definition;

    /// <summary>The inline grammar is missing — markdown loses bold, italics and links.</summary>
    public bool IsInlinePassMissing => _definition.InlineGrammar is not null && _inline is null;

    /// <summary>
    /// Loads the grammar and compiles the query. Returns <c>null</c> when the native library is
    /// missing — the file then shows as plain text instead of taking the app down.
    /// </summary>
    public static SyntaxAnalyzer? TryCreate(LanguageDefinition definition)
    {
        Language? language = null;
        Query? query = null;
        SyntaxAnalyzer? inline = null;

        try
        {
            language = new Language(definition.Library, definition.Symbol);
            query = new Query(language, LoadQuery(definition.QueryResource));

            // A missing inline grammar is no reason to hide the block structure.
            if (definition.InlineGrammar is { } inlineDefinition)
                inline = TryCreate(inlineDefinition);

            return new SyntaxAnalyzer(definition, language, query, inline);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException
                                      or BadImageFormatException or FileNotFoundException
                                      or ArgumentException or InvalidOperationException)
        {
            inline?.Dispose();
            query?.Dispose();
            language?.Dispose();
            return null;
        }
    }

    public SyntaxAnalysis Analyze(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return SyntaxAnalysis.Empty;

        using var parser = new Parser(_language);
        using var tree = parser.Parse(text);
        if (tree is null) return SyntaxAnalysis.Empty;

        ct.ThrowIfCancellationRequested();

        var captures = new List<RawCapture>();
        CollectCaptures(_query, tree.RootNode, captures, priorityOffset: 0, ct);

        var folds = new List<FoldRegion>();
        CollectFolds(tree.RootNode, text, folds, ct);

        // Second pass with the inline grammar over the ranges of the `inline` nodes.
        if (_inline is not null)
        {
            var ranges = CollectInlineRanges(tree.RootNode, ct);
            if (ranges.Count > 0) _inline.AppendInlineCaptures(text, ranges, captures, ct);
        }

        folds.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        return new SyntaxAnalysis(Flatten(captures, ct), folds);
    }

    private void AppendInlineCaptures(string text, List<TsRange> ranges, List<RawCapture> captures, CancellationToken ct)
    {
        using var parser = new Parser(_language);
        parser.IncludedRanges = [.. ranges];

        using var tree = parser.Parse(text);
        if (tree is null) return;

        CollectCaptures(_query, tree.RootNode, captures, InlinePriorityOffset, ct);
    }

    private static void CollectCaptures(Query query, Node root, List<RawCapture> captures, int priorityOffset, CancellationToken ct)
    {
        using var cursor = query.Execute(root);
        var counter = 0;

        foreach (var capture in cursor.Captures)
        {
            if ((++counter & 0xFF) == 0) ct.ThrowIfCancellationRequested();

            var category = HighlightCategories.FromCaptureName(capture.Name);
            if (category == HighlightCategory.None) continue;

            var node = capture.Node;
            if (node.EndIndex > node.StartIndex)
                captures.Add(new RawCapture(node.StartIndex, node.EndIndex, category, priorityOffset + capture.PatternIndex));
        }
    }

    private List<TsRange> CollectInlineRanges(Node root, CancellationToken ct)
    {
        var ranges = new List<TsRange>();
        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var node = stack.Pop();

            if (node.Type == _definition.InlineNodeType)
            {
                ranges.Add(node.Range);
                continue; // the block grammar has nothing left inside `inline`
            }

            foreach (var child in node.Children) stack.Push(child);
        }

        // ts_parser_set_included_ranges requires sorted, non-overlapping ranges.
        ranges.Sort(static (a, b) => a.StartIndex.CompareTo(b.StartIndex));
        return ranges;
    }

    /// <summary>
    /// Merges captures into non-overlapping spans. A nested capture beats the one enclosing it
    /// (the <c>**</c> delimiters colour as punctuation inside bold text); on an identical range
    /// the earlier query pattern wins.
    /// </summary>
    private static List<HighlightSpan> Flatten(List<RawCapture> captures, CancellationToken ct)
    {
        if (captures.Count == 0) return [];

        captures.Sort(static (a, b) =>
        {
            var byStart = a.Start.CompareTo(b.Start);
            if (byStart != 0) return byStart;
            var byLength = b.End.CompareTo(a.End); // longer (enclosing) first
            return byLength != 0 ? byLength : a.Priority.CompareTo(b.Priority);
        });

        var result = new List<HighlightSpan>(captures.Count);
        var stack = new Stack<RawCapture>();
        var position = 0;
        var previousStart = -1;
        var previousEnd = -1;

        void Emit(int start, int end, HighlightCategory category)
        {
            if (end <= start || category == HighlightCategory.None) return;

            // Adjacent spans of the same category merge — fewer ChangeLinePart calls.
            if (result.Count > 0)
            {
                var last = result[^1];
                if (last.End == start && last.Category == category)
                {
                    result[^1] = last with { End = end };
                    return;
                }
            }

            result.Add(new HighlightSpan(start, end, category));
        }

        foreach (var capture in captures)
        {
            ct.ThrowIfCancellationRequested();

            // Same range from another pattern — the earlier one already won, drop this.
            if (capture.Start == previousStart && capture.End == previousEnd) continue;
            previousStart = capture.Start;
            previousEnd = capture.End;

            while (stack.Count > 0 && stack.Peek().End <= capture.Start)
            {
                var finished = stack.Pop();
                Emit(position, finished.End, finished.Category);
                position = Math.Max(position, finished.End);
            }

            if (stack.Count > 0) Emit(position, capture.Start, stack.Peek().Category);
            position = Math.Max(position, capture.Start);

            // Nodes of one tree always nest; should something overhang anyway, clip it.
            var end = stack.Count > 0 ? Math.Min(capture.End, stack.Peek().End) : capture.End;
            if (end > position) stack.Push(capture with { End = end });
        }

        while (stack.Count > 0)
        {
            var finished = stack.Pop();
            Emit(position, finished.End, finished.Category);
            position = Math.Max(position, finished.End);
        }

        return result;
    }

    private void CollectFolds(Node root, string text, List<FoldRegion> folds, CancellationToken ct)
    {
        if (_definition.FoldNodeTypes.Length == 0) return;

        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var node = stack.Pop();

            if (Array.IndexOf(_definition.FoldNodeTypes, node.Type) >= 0)
            {
                var start = Math.Clamp(node.StartIndex, 0, text.Length);
                var end = TrimFoldEnd(text, start, node.EndIndex);

                // Blocks shorter than two lines are not folded — they would only flicker.
                if (text.AsSpan(start, end - start).IndexOfAny('\n', '\r') >= 0)
                {
                    folds.Add(new FoldRegion(start, end, BuildPreview(node, text)));
                }
            }

            foreach (var child in node.Children) stack.Push(child);
        }
    }

    /// <summary>
    /// A markdown <c>section</c> reaches all the way to the start of the next section, so folding
    /// it would swallow the line break in front of the following heading. That heading would end
    /// up on the folded line and could no longer be folded itself. Pulling the end back to the
    /// last non-whitespace character keeps every heading on a line of its own.
    /// </summary>
    private static int TrimFoldEnd(string text, int start, int end)
    {
        var trimmed = Math.Clamp(end, start, text.Length);
        while (trimmed > start && char.IsWhiteSpace(text[trimmed - 1])) trimmed--;
        return trimmed;
    }

    /// <summary>A folded block shows a preview — <c>{…}</c> or <c># Heading …</c>.</summary>
    private static string BuildPreview(Node node, string text) => node.Type switch
    {
        "object" => "{…}",
        "array" => "[…]",
        "fenced_code_block" => "```…```",
        _ => FirstLinePreview(node, text),
    };

    /// <summary>For a markdown section the first line is precisely its heading.</summary>
    private static string FirstLinePreview(Node node, string text)
    {
        var start = node.StartIndex;
        if (start < 0 || start >= text.Length) return "…";

        var lineEnd = text.IndexOfAny(['\r', '\n'], start);
        if (lineEnd < 0) lineEnd = text.Length;

        var heading = text[start..lineEnd].Trim();
        if (heading.Length > 60) heading = heading[..60].TrimEnd();
        return heading.Length == 0 ? "…" : heading + " …";
    }

    private static string LoadQuery(string resourceName)
    {
        var assembly = typeof(SyntaxAnalyzer).Assembly;
        var fullName = $"Skopos.Highlighting.Queries.{resourceName}";

        using var stream = assembly.GetManifestResourceStream(fullName)
            ?? throw new FileNotFoundException($"Embedded query {fullName} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Dispose()
    {
        _inline?.Dispose();
        _query.Dispose();
        _language.Dispose();
    }

    private readonly record struct RawCapture(int Start, int End, HighlightCategory Category, int Priority);
}
