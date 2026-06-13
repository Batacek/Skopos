using System.Text;

namespace Skopos.Core.Files;

public sealed record LoadedDocument
{
    public required string Path { get; init; }
    public required long SizeBytes { get; init; }

    /// <summary>File contents. Empty for binary files until the user asks otherwise.</summary>
    public required string Text { get; init; }

    public required DetectedEncoding Encoding { get; init; }
    public required LineEnding LineEnding { get; init; }

    /// <summary>There was a zero byte in the first 8 kB.</summary>
    public required bool IsBinary { get; init; }

    /// <summary>The file did not exist at load time, or could not be read.</summary>
    public string? Error { get; init; }

    public int LineCount { get; init; }
}

public sealed record FileLoadOptions
{
    /// <summary>Manual encoding choice from the status bar. Overrides detection.</summary>
    public DetectedEncoding? ForcedEncoding { get; init; }

    /// <summary>The user clicked "Show anyway" on a binary file.</summary>
    public bool ForceText { get; init; }

    public static readonly FileLoadOptions Default = new();
}

/// <summary>
/// File loading. Two things are unforgivable here: locking the user's file and
/// mangling their diacritics — this class handles both.
/// </summary>
public static class TextFileLoader
{
    /// <summary>How many bytes from the start we check for a zero byte.</summary>
    public const int BinarySniffSize = 8 * 1024;

    /// <summary>
    /// <c>FileShare.ReadWrite | FileShare.Delete</c> is critical. Locking the file would
    /// block a build in someone else's project — the fastest route to being uninstalled.
    /// </summary>
    public static FileStream OpenShared(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    public static LoadedDocument Load(string path, FileLoadOptions? options = null)
    {
        options ??= FileLoadOptions.Default;

        try
        {
            using var stream = OpenShared(path);
            var size = stream.Length;
            var bytes = ReadAllBytes(stream, size);

            var isBinary = LooksBinary(bytes);
            if (isBinary && !options.ForceText)
            {
                return new LoadedDocument
                {
                    Path = path,
                    SizeBytes = size,
                    Text = string.Empty,
                    Encoding = new DetectedEncoding(Encoding.UTF8, "—", false, EncodingSource.Fallback),
                    LineEnding = LineEnding.None,
                    IsBinary = true,
                };
            }

            var encoding = options.ForcedEncoding ?? TextEncodingDetector.Detect(bytes);
            var text = Decode(bytes, encoding);

            return new LoadedDocument
            {
                Path = path,
                SizeBytes = size,
                Text = text,
                Encoding = encoding,
                LineEnding = LineEndingExtensions.Detect(text),
                IsBinary = isBinary,
                LineCount = CountLines(text),
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new LoadedDocument
            {
                Path = path,
                SizeBytes = 0,
                Text = string.Empty,
                Encoding = new DetectedEncoding(Encoding.UTF8, "—", false, EncodingSource.Fallback),
                LineEnding = LineEnding.None,
                IsBinary = false,
                Error = e.Message,
            };
        }
    }

    public static Task<LoadedDocument> LoadAsync(string path, FileLoadOptions? options = null, CancellationToken ct = default) =>
        Task.Run(() => Load(path, options), ct);

    private static byte[] ReadAllBytes(FileStream stream, long size)
    {
        // Another process may be writing the file, so Length is only an estimate — read while it gives.
        if (size is > 0 and <= int.MaxValue)
        {
            var buffer = new byte[size];
            var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            return read == buffer.Length ? buffer : buffer[..read];
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>A zero byte in the first 8 kB means this is not text.</summary>
    public static bool LooksBinary(ReadOnlySpan<byte> bytes)
    {
        // UTF-16/UTF-32 with a BOM legitimately contain zeros — those we already recognised by the BOM.
        var bomLength = TextEncodingDetector.GetBomLength(bytes);
        if (bomLength >= 2) return false;

        var head = bytes.Length > BinarySniffSize ? bytes[..BinarySniffSize] : bytes;
        return head.IndexOf((byte)0) >= 0;
    }

    private static string Decode(ReadOnlySpan<byte> bytes, DetectedEncoding encoding)
    {
        var bom = TextEncodingDetector.GetBomLength(bytes);
        var payload = bom > 0 && bom <= bytes.Length ? bytes[bom..] : bytes;
        return encoding.Encoding.GetString(payload);
    }

    private static int CountLines(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return 0;

        var lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') lines++;
            else if (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')) lines++;
        }

        // A trailing line ending does not open a new line with content.
        var last = text[^1];
        if (last is '\n' or '\r') lines--;
        return Math.Max(lines, 1);
    }

    /// <summary>"2.3 MB" for the binary-file notice and the status bar.</summary>
    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} kB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.#} GB",
    };
}
