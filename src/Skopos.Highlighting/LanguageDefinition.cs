namespace Skopos.Highlighting;

/// <summary>Description of one grammar: what to load, what to colour with, and what to fold.</summary>
public sealed record LanguageDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>Extensions including the dot, lower case.</summary>
    public required string[] Extensions { get; init; }

    /// <summary>Native library name without extension, e.g. <c>tree-sitter-json</c>.</summary>
    public required string Library { get; init; }

    /// <summary>Exported symbol, e.g. <c>tree_sitter_json</c>.</summary>
    public required string Symbol { get; init; }

    /// <summary>Name of the embedded <c>.scm</c> file holding the highlighting query.</summary>
    public required string QueryResource { get; init; }

    /// <summary>Node types that turn into a foldable range.</summary>
    public required string[] FoldNodeTypes { get; init; }

    /// <summary>Code does not wrap, text and markdown do.</summary>
    public bool WordWrapByDefault { get; init; }

    /// <summary>
    /// Markdown is split into a block and an inline grammar. When this is set,
    /// a second pass runs through <c>ts_parser_set_included_ranges</c>.
    /// </summary>
    public LanguageDefinition? InlineGrammar { get; init; }

    /// <summary>Block-grammar node type whose ranges are handed to the inline pass.</summary>
    public string InlineNodeType { get; init; } = "inline";
}

/// <summary>Language detection by extension.</summary>
public static class LanguageRegistry
{
    public static readonly LanguageDefinition Json = new()
    {
        Id = "json",
        DisplayName = "JSON",
        Extensions = [".json", ".jsonc", ".json5", ".webmanifest", ".jsonl"],
        Library = "tree-sitter-json",
        Symbol = "tree_sitter_json",
        QueryResource = "json.scm",
        // Objects and arrays fold; single-line ones drop out at the length check.
        FoldNodeTypes = ["object", "array"],
        WordWrapByDefault = false,
    };

    public static readonly LanguageDefinition MarkdownInline = new()
    {
        Id = "markdown-inline",
        DisplayName = "Markdown (inline)",
        Extensions = [],
        Library = "tree-sitter-markdown-inline",
        Symbol = "tree_sitter_markdown_inline",
        QueryResource = "markdown-inline.scm",
        FoldNodeTypes = [],
    };

    public static readonly LanguageDefinition Markdown = new()
    {
        Id = "markdown",
        DisplayName = "Markdown",
        Extensions = [".md", ".markdown", ".mdown", ".mkd"],
        Library = "tree-sitter-markdown",
        Symbol = "tree_sitter_markdown",
        QueryResource = "markdown.scm",
        // Sections (a heading together with its content) and fenced code blocks.
        FoldNodeTypes = ["section", "fenced_code_block"],
        WordWrapByDefault = true,
        InlineGrammar = MarkdownInline,
    };

    public static readonly LanguageDefinition[] All = [Json, Markdown];

    /// <summary>Returns the language for a file, or <c>null</c> for plain text.</summary>
    public static LanguageDefinition? Detect(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension)) return null;

        foreach (var language in All)
            foreach (var candidate in language.Extensions)
                if (extension.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                    return language;

        return null;
    }
}
