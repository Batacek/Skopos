using System.Text;
using Skopos.Core.Files;
using Xunit;

namespace Skopos.Tests;

public class TextFileLoaderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("skopos-tests").FullName;

    private string Write(string name, byte[] content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private string Write(string name, string content) => Write(name, Encoding.UTF8.GetBytes(content));

    [Fact]
    public void Load_ReturnsTextEncodingAndLineEndings()
    {
        var path = Write("a.json", "{\n  \"kůň\": 1\n}\n");

        var document = TextFileLoader.Load(path);

        Assert.Null(document.Error);
        Assert.Contains("kůň", document.Text);
        Assert.Equal("UTF-8", document.Encoding.DisplayName);
        Assert.Equal(LineEnding.Lf, document.LineEnding);
        Assert.Equal(3, document.LineCount);
        Assert.False(document.IsBinary);
    }

    [Fact]
    public void BinaryFile_IsNotOpenedAsText()
    {
        var path = Write("a.bin", [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x41, 0x42]);

        var document = TextFileLoader.Load(path);

        Assert.True(document.IsBinary);
        Assert.Empty(document.Text);
        Assert.Equal(8, document.SizeBytes);
    }

    [Fact]
    public void ShowAnyway_RevealsEvenABinaryFile()
    {
        var path = Write("a.bin", [0x41, 0x00, 0x42]);

        var document = TextFileLoader.Load(path, new FileLoadOptions { ForceText = true });

        Assert.True(document.IsBinary);
        Assert.NotEmpty(document.Text);
    }

    [Fact]
    public void Utf16WithBom_IsNotConsideredBinary()
    {
        // UTF-16 is full of zero bytes — without the BOM check the \0 test would wrongly succeed.
        var path = Write("a.txt", Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("hello")).ToArray());

        var document = TextFileLoader.Load(path);

        Assert.False(document.IsBinary);
        Assert.Equal("hello", document.Text);
        Assert.Equal("UTF-16 LE", document.Encoding.DisplayName);
    }

    [Fact]
    public void Bom_DoesNotLeakIntoTheText()
    {
        var path = Write("a.json", Encoding.UTF8.GetPreamble().Concat("{}"u8.ToArray()).ToArray());

        var document = TextFileLoader.Load(path);

        Assert.Equal("{}", document.Text);
    }

    [Fact]
    public void ManualEncoding_OverridesDetection()
    {
        TextEncodingDetector.EnsureCodePagesRegistered();
        var path = Write("a.txt", Encoding.GetEncoding(1250).GetBytes("žluťoučký"));

        var document = TextFileLoader.Load(path, new FileLoadOptions
        {
            ForcedEncoding = TextEncodingDetector.FromDisplayName("Windows-1250"),
        });

        Assert.Equal("žluťoučký", document.Text);
        Assert.Equal("Windows-1250", document.Encoding.DisplayName);
    }

    [Fact]
    public void MissingFile_ReturnsAnErrorInsteadOfThrowing()
    {
        var document = TextFileLoader.Load(Path.Combine(_directory, "does-not-exist.json"));

        Assert.NotNull(document.Error);
        Assert.Empty(document.Text);
    }

    [Fact]
    public void OpeningDoesNotLockTheFile()
    {
        // Locking the user's file is the fastest route to being uninstalled.
        var path = Write("a.json", "{}");

        using var ours = TextFileLoader.OpenShared(path);
        using var theirs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

        theirs.Seek(0, SeekOrigin.End);
        theirs.WriteByte((byte)' ');
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2 kB")]
    [InlineData(2411724, "2,3 MB")]
    public void FormatSize_IsReadable(long bytes, string expected)
    {
        // FormatSize follows the current culture, which is the right behaviour for a desktop
        // app even though the UI text is English. The culture is pinned here so the expected
        // decimal separator does not depend on the machine running the test.
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("cs-CZ");
        try
        {
            Assert.Equal(expected, TextFileLoader.FormatSize(bytes));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Cleaning up a temp directory is no reason to fail the test.
        }
    }
}
