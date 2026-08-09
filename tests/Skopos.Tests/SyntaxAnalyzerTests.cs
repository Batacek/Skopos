using Skopos.Highlighting;
using Xunit;

namespace Skopos.Tests;

public class SyntaxAnalyzerTests
{
    private static SyntaxAnalyzer Create(LanguageDefinition definition)
    {
        var analyzer = SyntaxAnalyzer.TryCreate(definition);
        Assert.True(analyzer is not null,
            $"Grammar {definition.Library} did not load. Run grammars/build-grammars.ps1 or .sh.");
        return analyzer!;
    }

    private static HighlightCategory CategoryAt(SyntaxAnalysis analysis, int offset)
    {
        foreach (var span in analysis.Spans)
            if (offset >= span.Start && offset < span.End)
                return span.Category;
        return HighlightCategory.None;
    }

    // --- JSON ---------------------------------------------------------------

    [Fact]
    public void Json_ColoursKeysValuesAndPunctuation()
    {
        using var analyzer = Create(LanguageRegistry.Json);
        const string source = """{"name": "skopos", "n": 42, "ok": true, "x": null}""";

        var analysis = analyzer.Analyze(source);

        Assert.Equal(HighlightCategory.Punctuation, CategoryAt(analysis, source.IndexOf('{')));
        Assert.Equal(HighlightCategory.Property, CategoryAt(analysis, source.IndexOf("\"name\"", StringComparison.Ordinal)));
        Assert.Equal(HighlightCategory.String, CategoryAt(analysis, source.IndexOf("\"skopos\"", StringComparison.Ordinal)));
        Assert.Equal(HighlightCategory.Number, CategoryAt(analysis, source.IndexOf("42", StringComparison.Ordinal)));
        Assert.Equal(HighlightCategory.Constant, CategoryAt(analysis, source.IndexOf("true", StringComparison.Ordinal)));
        Assert.Equal(HighlightCategory.Constant, CategoryAt(analysis, source.IndexOf("null", StringComparison.Ordinal)));
    }

    [Fact]
    public void Json_KeyBeatsGenericString()
    {
        // Both patterns match the same node; the earlier one takes precedence.
        using var analyzer = Create(LanguageRegistry.Json);
        const string source = """{"key": "value"}""";

        var analysis = analyzer.Analyze(source);

        Assert.Equal(HighlightCategory.Property, CategoryAt(analysis, 2));
        Assert.Equal(HighlightCategory.String, CategoryAt(analysis, source.IndexOf("\"value\"", StringComparison.Ordinal) + 1));
    }

    [Fact]
    public void Spans_DoNotOverlapAndAreSorted()
    {
        using var analyzer = Create(LanguageRegistry.Json);
        var analysis = analyzer.Analyze("""{"a": [1, 2, {"b": "c"}], "d": null}""");

        var previousEnd = 0;
        foreach (var span in analysis.Spans)
        {
            Assert.True(span.Start >= previousEnd, "Spans overlap or are not sorted.");
            Assert.True(span.End > span.Start);
            previousEnd = span.End;
        }
    }

    [Fact]
    public void Diacritics_DoNotShiftOffsets()
    {
        // The tree-sitter binding returns indices in UTF-16 units, so they index straight into
        // the C# string. Were they UTF-8 bytes, this test would fail. The Czech data is the
        // point of the test — this is exactly the text the app has to get right.
        using var analyzer = Create(LanguageRegistry.Json);
        const string source = """{"klíč": "ěščřž", "emoji": "😀"}""";

        var analysis = analyzer.Analyze(source);

        var valueStart = source.IndexOf("\"ěščřž\"", StringComparison.Ordinal);
        Assert.Contains(analysis.Spans, s => s.Start == valueStart && s.Category == HighlightCategory.String);

        var emojiStart = source.IndexOf("\"😀\"", StringComparison.Ordinal);
        Assert.Contains(analysis.Spans, s => s.Start == emojiStart && s.Category == HighlightCategory.String);
    }

    [Fact]
    public void Json_FoldsMultiLineObjectsAndArrays()
    {
        using var analyzer = Create(LanguageRegistry.Json);
        const string source = """
            {
              "singleLine": { "a": 1 },
              "multiLine": {
                "b": 2
              },
              "array": [
                1,
                2
              ]
            }
            """;

        var analysis = analyzer.Analyze(source);

        // Outer object, inner multi-line object and multi-line array — the single-line one is out.
        Assert.Equal(3, analysis.Folds.Count);
        Assert.Contains(analysis.Folds, f => f.Preview == "{…}");
        Assert.Contains(analysis.Folds, f => f.Preview == "[…]");
    }

    [Fact]
    public void BrokenJson_DoesNotCrashAnalysis()
    {
        using var analyzer = Create(LanguageRegistry.Json);

        var analysis = analyzer.Analyze("""{"a": [1, 2""");

        Assert.NotNull(analysis);
    }

    [Fact]
    public void EmptyText_GivesEmptyResult()
    {
        using var analyzer = Create(LanguageRegistry.Json);

        var analysis = analyzer.Analyze(string.Empty);

        Assert.Empty(analysis.Spans);
        Assert.Empty(analysis.Folds);
    }

    // --- Markdown (two-phase parse) ------------------------------------------

    [Fact]
    public void Markdown_HasBothGrammars()
    {
        using var analyzer = Create(LanguageRegistry.Markdown);

        Assert.False(analyzer.IsInlinePassMissing);
    }

    [Fact]
    public void Markdown_ColoursHeadingsFromTheBlockGrammar()
    {
        using var analyzer = Create(LanguageRegistry.Markdown);
        const string source = "# Heading\n\nParagraph.\n";

        var analysis = analyzer.Analyze(source);

        Assert.Contains(analysis.Spans, s => s.Category == HighlightCategory.Heading);
    }

    [Fact]
    public void Markdown_ColoursBoldItalicsAndLinksFromTheInlineGrammar()
    {
        // This is the whole point of the two-phase parse — without the second pass
        // none of these categories would be here.
        using var analyzer = Create(LanguageRegistry.Markdown);
        const string source = "Text with **bold** and *italics* and a [link](https://example.com) and `code`.\n";

        var analysis = analyzer.Analyze(source);
        var categories = analysis.Spans.Select(s => s.Category).ToHashSet();

        Assert.Contains(HighlightCategory.Strong, categories);
        Assert.Contains(HighlightCategory.Emphasis, categories);
        Assert.Contains(HighlightCategory.Link, categories);
        Assert.Contains(HighlightCategory.String, categories);
    }

    [Fact]
    public void Markdown_DelimitersInsideBoldArePunctuation()
    {
        // The nested capture has to beat the enclosing one, or the asterisks blend into the text.
        using var analyzer = Create(LanguageRegistry.Markdown);
        const string source = "**bold**\n";

        var analysis = analyzer.Analyze(source);

        Assert.Equal(HighlightCategory.Punctuation, CategoryAt(analysis, 0));
        Assert.Equal(HighlightCategory.Strong, CategoryAt(analysis, source.IndexOf("bold", StringComparison.Ordinal)));
    }

    [Fact]
    public void Markdown_FoldsSectionsAndCodeBlocks()
    {
        using var analyzer = Create(LanguageRegistry.Markdown);
        const string source = """
            # Heading

            Paragraph.

            ## Subheading

            ```csharp
            var x = 1;
            ```
            """;

        var analysis = analyzer.Analyze(source);

        Assert.Contains(analysis.Folds, f => f.Preview.StartsWith("# Heading", StringComparison.Ordinal));
        Assert.Contains(analysis.Folds, f => f.Preview.StartsWith("## Subheading", StringComparison.Ordinal));
        Assert.Contains(analysis.Folds, f => f.Preview == "```…```");
    }

    [Fact]
    public void Markdown_SectionFoldStopsBeforeTheNextHeading()
    {
        // A section node reaches to the start of the next section. Folding that raw range would
        // swallow the line break in front of the following heading, which would then sit on the
        // folded line and could no longer be folded itself.
        using var analyzer = Create(LanguageRegistry.Markdown);
        const string source = "# First\n\nText.\n\n# Second\n\nMore text.\n";

        var analysis = analyzer.Analyze(source);

        var first = analysis.Folds.Single(f => f.Preview.StartsWith("# First", StringComparison.Ordinal));
        var secondHeading = source.IndexOf("# Second", StringComparison.Ordinal);

        // The fold ends on content, not on whitespace…
        Assert.False(char.IsWhiteSpace(source[first.End - 1]));
        // …and at least one line break survives between it and the next heading.
        Assert.Contains("\n", source[first.End..secondHeading], StringComparison.Ordinal);

        // Which is what keeps the second section foldable in its own right.
        Assert.Contains(analysis.Folds, f => f.Preview.StartsWith("# Second", StringComparison.Ordinal));
    }

    [Fact]
    public void Markdown_KnowsGfmExtensionsAndFrontmatter()
    {
        // The extensions are baked into the generated parser — if the grammar build changes,
        // this test catches it.
        using var analyzer = Create(LanguageRegistry.Markdown);
        const string source = """
            ---
            title: Example
            ---

            - [x] done

            | a | b |
            |---|---|
            | 1 | 2 |
            """;

        var analysis = analyzer.Analyze(source);
        var categories = analysis.Spans.Select(s => s.Category).ToHashSet();

        Assert.Contains(HighlightCategory.Comment, categories); // YAML frontmatter
        Assert.Contains(HighlightCategory.Constant, categories); // task list marker
        Assert.Contains(HighlightCategory.Strong, categories);   // table header
    }

    [Fact]
    public void Cancellation_Throws()
    {
        using var analyzer = Create(LanguageRegistry.Json);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze("""{"a": 1}""", cts.Token));
    }
}
