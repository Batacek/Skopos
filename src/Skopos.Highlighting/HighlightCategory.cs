namespace Skopos.Highlighting;

/// <summary>
/// The fixed category set. Every <c>@capture</c> from every grammar maps onto these —
/// colours then live only in the theme, never in the grammars.
/// </summary>
public enum HighlightCategory
{
    None = 0,
    Keyword,
    String,
    Number,
    Comment,
    Function,
    Type,
    Variable,
    Property,
    Constant,
    Operator,
    Punctuation,
    Heading,
    Emphasis,
    Strong,
    Link,
}

public static class HighlightCategories
{
    /// <summary>
    /// Maps a capture name from a <c>.scm</c> file onto a category.
    /// </summary>
    /// <remarks>
    /// Dotted names are first shortened from the right, so <c>@constant.builtin</c> falls back
    /// to <c>constant</c> — the usual tree-sitter convention. When that finds nothing, we try
    /// from the left: Neovim and Helix write <c>@text.title</c> or <c>@text.uri</c>, where the
    /// meaning sits in the last part. Supporting both directions lets their <c>.scm</c> files
    /// be used unchanged.
    /// </remarks>
    public static HighlightCategory FromCaptureName(ReadOnlySpan<char> capture)
    {
        var prefix = capture;
        while (!prefix.IsEmpty)
        {
            var category = Exact(prefix);
            if (category != HighlightCategory.None) return category;

            var dot = prefix.LastIndexOf('.');
            if (dot <= 0) break;
            prefix = prefix[..dot];
        }

        var suffix = capture;
        while (true)
        {
            var dot = suffix.IndexOf('.');
            if (dot < 0 || dot + 1 >= suffix.Length) return HighlightCategory.None;

            suffix = suffix[(dot + 1)..];
            var category = Exact(suffix);
            if (category != HighlightCategory.None) return category;
        }
    }

    private static HighlightCategory Exact(ReadOnlySpan<char> name) => name switch
    {
        "keyword" => HighlightCategory.Keyword,
        "string" => HighlightCategory.String,
        "number" or "float" or "integer" => HighlightCategory.Number,
        "comment" => HighlightCategory.Comment,
        "function" or "method" => HighlightCategory.Function,
        "type" or "class" or "constructor" => HighlightCategory.Type,
        "variable" or "parameter" => HighlightCategory.Variable,
        "property" or "field" or "attribute" => HighlightCategory.Property,
        "constant" or "boolean" => HighlightCategory.Constant,
        "operator" => HighlightCategory.Operator,
        "punctuation" => HighlightCategory.Punctuation,
        "heading" or "title" => HighlightCategory.Heading,
        "emphasis" or "italic" => HighlightCategory.Emphasis,
        "strong" or "bold" => HighlightCategory.Strong,
        "link" or "uri" or "url" or "reference" => HighlightCategory.Link,
        _ => HighlightCategory.None,
    };
}
