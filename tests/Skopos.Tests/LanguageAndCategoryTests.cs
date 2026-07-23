using Skopos.Core;
using Skopos.Highlighting;
using Xunit;

namespace Skopos.Tests;

public class HighlightCategoryTests
{
    [Theory]
    [InlineData("string", HighlightCategory.String)]
    [InlineData("property", HighlightCategory.Property)]
    [InlineData("punctuation", HighlightCategory.Punctuation)]
    public void PlainName_Matches(string capture, HighlightCategory expected) =>
        Assert.Equal(expected, HighlightCategories.FromCaptureName(capture));

    [Theory]
    [InlineData("constant.builtin", HighlightCategory.Constant)]
    [InlineData("punctuation.delimiter", HighlightCategory.Punctuation)]
    [InlineData("string.special.symbol", HighlightCategory.String)]
    public void DottedName_IsShortenedFromTheRight(string capture, HighlightCategory expected) =>
        Assert.Equal(expected, HighlightCategories.FromCaptureName(capture));

    [Theory]
    [InlineData("text.title", HighlightCategory.Heading)]
    [InlineData("text.emphasis", HighlightCategory.Emphasis)]
    [InlineData("text.strong", HighlightCategory.Strong)]
    [InlineData("text.uri", HighlightCategory.Link)]
    [InlineData("text.reference", HighlightCategory.Link)]
    [InlineData("text.literal", HighlightCategory.None)]
    public void NeovimCaptures_AreUsableWithoutRewriting(string capture, HighlightCategory expected) =>
        Assert.Equal(expected, HighlightCategories.FromCaptureName(capture));

    [Theory]
    [InlineData("none")]
    [InlineData("completely.unknown.category")]
    [InlineData("")]
    public void UnknownName_ColoursNothing(string capture) =>
        Assert.Equal(HighlightCategory.None, HighlightCategories.FromCaptureName(capture));
}

public class LanguageRegistryTests
{
    [Theory]
    [InlineData("a.json", "json")]
    [InlineData("A.JSON", "json")]
    [InlineData("package.webmanifest", "json")]
    [InlineData("readme.md", "markdown")]
    [InlineData("readme.MARKDOWN", "markdown")]
    public void Detect_ByExtension(string path, string expectedId) =>
        Assert.Equal(expectedId, LanguageRegistry.Detect(path)?.Id);

    [Theory]
    [InlineData("readme.txt")]
    [InlineData("Makefile")]
    [InlineData("a.cs")]
    public void UnknownExtension_IsPlainText(string path) =>
        Assert.Null(LanguageRegistry.Detect(path));

    [Fact]
    public void WordWrap_FollowsTheLanguage()
    {
        // Code does not wrap, markdown does.
        Assert.False(LanguageRegistry.Json.WordWrapByDefault);
        Assert.True(LanguageRegistry.Markdown.WordWrapByDefault);
    }

    [Fact]
    public void Markdown_HasAnInlineGrammar() =>
        Assert.Equal("markdown-inline", LanguageRegistry.Markdown.InlineGrammar?.Id);
}

public class CommandLineTests
{
    [Fact]
    public void Options_AreToldApartFromPaths()
    {
        var args = CommandLineArgs.Parse(["--timing", "a.json", "--new-instance", "b.md"]);

        Assert.True(args.Timing);
        Assert.True(args.NewInstance);
        Assert.False(args.Help);
        Assert.Equal(2, args.Files.Count);
    }

    [Fact]
    public void Paths_AreNormalisedToAbsolute()
    {
        // The running instance has a different working directory, so a relative path
        // would mean nothing over there.
        var args = CommandLineArgs.Parse(["relative.json"]);

        Assert.True(Path.IsPathRooted(args.Files[0]));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help(string flag) => Assert.True(CommandLineArgs.Parse([flag]).Help);

    [Fact]
    public void UnknownOption_IsNotTreatedAsAFile()
    {
        var args = CommandLineArgs.Parse(["--what-is-this"]);

        Assert.Empty(args.Files);
    }
}
