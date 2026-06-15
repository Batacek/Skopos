using System.Text;
using Skopos.Core.Files;
using Xunit;

namespace Skopos.Tests;

public class TextEncodingDetectorTests
{
    [Fact]
    public void Utf8Bom_IsRecognisedUnambiguously()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat("hello"u8.ToArray()).ToArray();

        var detected = TextEncodingDetector.Detect(bytes);

        Assert.Equal("UTF-8 BOM", detected.DisplayName);
        Assert.True(detected.HasBom);
        Assert.Equal(EncodingSource.Bom, detected.Source);
        Assert.Equal(3, TextEncodingDetector.GetBomLength(bytes));
    }

    [Theory]
    [InlineData("UTF-16 LE", new byte[] { 0xFF, 0xFE })]
    [InlineData("UTF-16 BE", new byte[] { 0xFE, 0xFF })]
    [InlineData("UTF-32 LE", new byte[] { 0xFF, 0xFE, 0x00, 0x00 })]
    [InlineData("UTF-32 BE", new byte[] { 0x00, 0x00, 0xFE, 0xFF })]
    public void EveryBom_HasItsOwnLabel(string expected, byte[] bom)
    {
        var detected = TextEncodingDetector.Detect(bom);

        Assert.Equal(expected, detected.DisplayName);
        Assert.Equal(EncodingSource.Bom, detected.Source);
    }

    [Fact]
    public void PureAscii_IsUtf8WithoutGuessing()
    {
        var detected = TextEncodingDetector.Detect("{ \"a\": 1 }"u8);

        Assert.Equal("UTF-8", detected.DisplayName);
        Assert.Equal(EncodingSource.Utf8Validation, detected.Source);
        Assert.True(detected.IsReliable);
    }

    [Fact]
    public void ValidUtf8WithDiacritics_PassesValidation()
    {
        // The Czech pangram is the fixture on purpose — these are the bytes the app has to
        // round-trip without mangling.
        var bytes = Encoding.UTF8.GetBytes("příliš žluťoučký kůň úpěl ďábelské ódy");

        var detected = TextEncodingDetector.Detect(bytes);

        Assert.Equal("UTF-8", detected.DisplayName);
        Assert.Equal(EncodingSource.Utf8Validation, detected.Source);
        Assert.Equal("příliš žluťoučký kůň úpěl ďábelské ódy", detected.Encoding.GetString(bytes));
    }

    [Fact]
    public void CzechTextInWindows1250_DoesNotPassAsUtf8()
    {
        TextEncodingDetector.EnsureCodePagesRegistered();
        var windows1250 = Encoding.GetEncoding(1250);
        var bytes = windows1250.GetBytes("Příliš žluťoučký kůň úpěl ďábelské ódy. " +
                                         "This sentence pads the sample so the heuristic has something to work with.");

        var detected = TextEncodingDetector.Detect(bytes);

        // The heuristic cannot reliably tell Windows-1250, ISO-8859-2 and CP852 apart,
        // so we do not pin a specific encoding. What matters is that it is not UTF-8 and that
        // the result is flagged as uncertain, so the user knows where to click.
        Assert.NotEqual(EncodingSource.Utf8Validation, detected.Source);
        Assert.Equal(EncodingSource.Heuristic, detected.Source);
        Assert.False(detected.IsReliable);
    }

    [Fact]
    public void IncompleteSequenceAtSampleEnd_DoesNotBreakDetection()
    {
        // 64 kB of ASCII and a multi-byte character right past the boundary — trimming to a
        // character boundary must stop valid UTF-8 from being judged broken.
        var text = new string('a', TextEncodingDetector.SampleSize - 1) + "ř";
        var bytes = Encoding.UTF8.GetBytes(text);

        var detected = TextEncodingDetector.Detect(bytes);

        Assert.Equal(EncodingSource.Utf8Validation, detected.Source);
    }

    [Fact]
    public void ManualChoice_ReturnsTheRequestedEncoding()
    {
        var detected = TextEncodingDetector.FromDisplayName("Windows-1250");

        Assert.Equal("Windows-1250", detected.DisplayName);
        Assert.Equal(1250, detected.Encoding.CodePage);
        Assert.Equal(EncodingSource.Manual, detected.Source);
        Assert.True(detected.IsReliable);
    }

    [Fact]
    public void EveryOfferedEncoding_CanBeCreated()
    {
        foreach (var name in TextEncodingDetector.SelectableEncodings)
        {
            var detected = TextEncodingDetector.FromDisplayName(name);
            Assert.Equal(name, detected.DisplayName);
        }
    }
}
