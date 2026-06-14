using Skopos.Core.Files;
using Xunit;

namespace Skopos.Tests;

public class LineEndingTests
{
    [Theory]
    [InlineData("no line ending", LineEnding.None)]
    [InlineData("a\nb", LineEnding.Lf)]
    [InlineData("a\r\nb", LineEnding.CrLf)]
    [InlineData("a\rb", LineEnding.Cr)]
    [InlineData("a\r\nb\nc", LineEnding.Mixed)]
    [InlineData("a\rb\nc", LineEnding.Mixed)]
    public void Detect_RecognisesEveryCombination(string text, LineEnding expected) =>
        Assert.Equal(expected, LineEndingExtensions.Detect(text));

    [Fact]
    public void CrLf_DoesNotCountAsCrPlusLf()
    {
        // A naive implementation would see both CR and LF in "a\r\nb" and report mixed endings.
        Assert.Equal(LineEnding.CrLf, LineEndingExtensions.Detect("a\r\nb\r\nc\r\n"));
    }

    [Theory]
    [InlineData(LineEnding.Lf, "LF")]
    [InlineData(LineEnding.CrLf, "CRLF")]
    [InlineData(LineEnding.Mixed, "mixed")]
    public void ToDisplayString_IsForTheStatusBar(LineEnding value, string expected) =>
        Assert.Equal(expected, value.ToDisplayString());
}
