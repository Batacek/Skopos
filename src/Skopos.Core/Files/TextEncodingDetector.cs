using System.Text;
using UtfUnknown;

namespace Skopos.Core.Files;

/// <summary>How we arrived at the encoding. For the user this is the difference between "certain" and "guessing".</summary>
public enum EncodingSource
{
    /// <summary>BOM — unambiguous.</summary>
    Bom,
    /// <summary>The first 64 kB decoded as valid UTF-8.</summary>
    Utf8Validation,
    /// <summary>Statistical heuristic. It gets Czech wrong now and then.</summary>
    Heuristic,
    /// <summary>Nothing matched, so we fall back to UTF-8 with replacement characters.</summary>
    Fallback,
    /// <summary>The user switched the encoding by hand in the status bar.</summary>
    Manual,
}

public sealed record DetectedEncoding(Encoding Encoding, string DisplayName, bool HasBom, EncodingSource Source)
{
    /// <summary>The heuristic is the only source that cannot be trusted — the UI marks it as uncertain.</summary>
    public bool IsReliable => Source is not EncodingSource.Heuristic;
}

/// <summary>
/// Encoding detection in this order: BOM → UTF-8 validation → heuristic → fallback.
/// </summary>
public static class TextEncodingDetector
{
    /// <summary>How many bytes are enough to decide. More only slows startup down.</summary>
    public const int SampleSize = 64 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding LenientUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    private static bool _codePagesRegistered;

    /// <summary>
    /// The encodings offered in the manual switcher. The 8-bit ones matter for Czech users —
    /// the heuristic cannot reliably tell them apart.
    /// </summary>
    public static readonly string[] SelectableEncodings =
    [
        "UTF-8", "UTF-8 BOM", "UTF-16 LE", "UTF-16 BE",
        "Windows-1250", "ISO-8859-2", "CP852", "Windows-1252", "ISO-8859-1",
    ];

    /// <summary>
    /// Registers the code page provider (Windows-1250, CP852, …). These are not available
    /// on .NET Core without this call. Idempotent.
    /// </summary>
    public static void EnsureCodePagesRegistered()
    {
        if (_codePagesRegistered) return;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _codePagesRegistered = true;
    }

    /// <summary>Returns the <see cref="Encoding"/> for a name from <see cref="SelectableEncodings"/>.</summary>
    public static DetectedEncoding FromDisplayName(string name)
    {
        EnsureCodePagesRegistered();
        return name switch
        {
            "UTF-8" => new DetectedEncoding(LenientUtf8, "UTF-8", false, EncodingSource.Manual),
            "UTF-8 BOM" => new DetectedEncoding(LenientUtf8, "UTF-8 BOM", true, EncodingSource.Manual),
            "UTF-16 LE" => new DetectedEncoding(Encoding.Unicode, "UTF-16 LE", false, EncodingSource.Manual),
            "UTF-16 BE" => new DetectedEncoding(Encoding.BigEndianUnicode, "UTF-16 BE", false, EncodingSource.Manual),
            "Windows-1250" => Manual(1250, "Windows-1250"),
            "ISO-8859-2" => Manual(28592, "ISO-8859-2"),
            "CP852" => Manual(852, "CP852"),
            "Windows-1252" => Manual(1252, "Windows-1252"),
            "ISO-8859-1" => Manual(28591, "ISO-8859-1"),
            _ => new DetectedEncoding(LenientUtf8, "UTF-8", false, EncodingSource.Manual),
        };

        static DetectedEncoding Manual(int codePage, string display)
        {
            try
            {
                return new DetectedEncoding(Encoding.GetEncoding(codePage), display, false, EncodingSource.Manual);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException)
            {
                return new DetectedEncoding(LenientUtf8, "UTF-8", false, EncodingSource.Fallback);
            }
        }
    }

    /// <summary>Length of the BOM at the start of the buffer, or 0.</summary>
    public static int GetBomLength(ReadOnlySpan<byte> bytes) => DetectBom(bytes)?.length ?? 0;

    public static DetectedEncoding Detect(ReadOnlySpan<byte> bytes)
    {
        EnsureCodePagesRegistered();

        // 1) BOM — unambiguous, no further guessing.
        if (DetectBom(bytes) is { } bom)
            return new DetectedEncoding(bom.encoding, bom.name, true, EncodingSource.Bom);

        var sample = bytes.Length > SampleSize ? bytes[..SampleSize] : bytes;

        // Pure ASCII is valid UTF-8 and valid in every 8-bit encoding at the same time.
        // Handing it to the heuristic would only make it guess.
        if (IsAscii(sample))
            return new DetectedEncoding(LenientUtf8, "UTF-8", false, EncodingSource.Utf8Validation);

        // 2) UTF-8 validation. The sample may be cut in the middle of a multi-byte
        //    sequence, so trim the end back to a character boundary.
        if (IsValidUtf8(TrimToCharBoundary(sample)))
            return new DetectedEncoding(LenientUtf8, "UTF-8", false, EncodingSource.Utf8Validation);

        // 3) Heuristic for 8-bit encodings.
        if (DetectByHeuristic(sample) is { } heuristic)
            return heuristic;

        // 4) Fallback — UTF-8 with replacement characters, so at least something shows up.
        return new DetectedEncoding(LenientUtf8, "UTF-8", false, EncodingSource.Fallback);
    }

    private static (Encoding encoding, string name, int length)? DetectBom(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xFE && b[2] == 0x00 && b[3] == 0x00)
            return (new UTF32Encoding(bigEndian: false, byteOrderMark: true), "UTF-32 LE", 4);
        if (b.Length >= 4 && b[0] == 0x00 && b[1] == 0x00 && b[2] == 0xFE && b[3] == 0xFF)
            return (new UTF32Encoding(bigEndian: true, byteOrderMark: true), "UTF-32 BE", 4);
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
            return (LenientUtf8, "UTF-8 BOM", 3);
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
            return (Encoding.Unicode, "UTF-16 LE", 2);
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
            return (Encoding.BigEndianUnicode, "UTF-16 BE", 2);
        return null;
    }

    private static bool IsAscii(ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
            if (b >= 0x80) return false;
        return true;
    }

    /// <summary>
    /// Cuts an incomplete UTF-8 sequence off the end of the sample, so validation does not
    /// fail merely because we happened to read exactly 64 kB.
    /// </summary>
    private static ReadOnlySpan<byte> TrimToCharBoundary(ReadOnlySpan<byte> sample)
    {
        // At most 3 bytes back — no UTF-8 sequence is longer.
        var limit = Math.Min(3, sample.Length);
        for (var back = 1; back <= limit; back++)
        {
            var b = sample[^back];
            if ((b & 0b1100_0000) == 0b1000_0000) continue; // continuation byte
            var expected = b switch
            {
                >= 0xF0 => 4,
                >= 0xE0 => 3,
                >= 0xC0 => 2,
                _ => 1,
            };
            return expected > back ? sample[..^back] : sample;
        }
        return sample;
    }

    private static bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            StrictUtf8.GetCharCount(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static DetectedEncoding? DetectByHeuristic(ReadOnlySpan<byte> sample)
    {
        try
        {
            var result = CharsetDetector.DetectFromBytes(sample.ToArray());
            var best = result.Detected;
            if (best?.Encoding is null) return null;

            return new DetectedEncoding(best.Encoding, NormalizeName(best.Encoding), false, EncodingSource.Heuristic);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string NormalizeName(Encoding encoding) => encoding.CodePage switch
    {
        65001 => "UTF-8",
        1200 => "UTF-16 LE",
        1201 => "UTF-16 BE",
        1250 => "Windows-1250",
        1252 => "Windows-1252",
        852 => "CP852",
        28592 => "ISO-8859-2",
        28591 => "ISO-8859-1",
        _ => encoding.WebName.ToUpperInvariant(),
    };
}
