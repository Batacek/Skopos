namespace Skopos.Core.Files;

/// <summary>Line endings detected in a file.</summary>
public enum LineEnding
{
    /// <summary>The file contains no line ending at all.</summary>
    None,
    Lf,
    CrLf,
    Cr,
    /// <summary>The file mixes several kinds — the status bar flags this with an icon.</summary>
    Mixed,
}

public static class LineEndingExtensions
{
    public static string ToDisplayString(this LineEnding value) => value switch
    {
        LineEnding.Lf => "LF",
        LineEnding.CrLf => "CRLF",
        LineEnding.Cr => "CR",
        LineEnding.Mixed => "mixed",
        _ => "—",
    };

    /// <summary>Walks the text once and records which line endings it contains.</summary>
    public static LineEnding Detect(ReadOnlySpan<char> text)
    {
        var lf = false;
        var crlf = false;
        var cr = false;

        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '\r' when i + 1 < text.Length && text[i + 1] == '\n':
                    crlf = true;
                    i++;
                    break;
                case '\r':
                    cr = true;
                    break;
                case '\n':
                    lf = true;
                    break;
            }
        }

        return (lf, crlf, cr) switch
        {
            (false, false, false) => LineEnding.None,
            (true, false, false) => LineEnding.Lf,
            (false, true, false) => LineEnding.CrLf,
            (false, false, true) => LineEnding.Cr,
            _ => LineEnding.Mixed,
        };
    }
}
