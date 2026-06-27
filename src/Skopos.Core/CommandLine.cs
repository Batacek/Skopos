namespace Skopos.Core;

/// <summary>Parsed command line. Skopos has few options — it is a viewer, not a tool.</summary>
public sealed record CommandLineArgs
{
    public required IReadOnlyList<string> Files { get; init; }

    /// <summary>Print the startup breakdown to stderr.</summary>
    public bool Timing { get; init; }

    /// <summary>Do not hand the files to a running instance (useful while debugging).</summary>
    public bool NewInstance { get; init; }

    public bool Help { get; init; }

    public static CommandLineArgs Parse(string[] args)
    {
        var files = new List<string>();
        var timing = false;
        var newInstance = false;
        var help = false;

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--timing":
                    timing = true;
                    break;
                case "--new-instance":
                    newInstance = true;
                    break;
                case "--help" or "-h" or "/?":
                    help = true;
                    break;
                default:
                    if (arg.StartsWith('-')) break;
                    // Normalise paths right away — the running instance has a different working
                    // directory, so what reaches it has to be absolute.
                    files.Add(TryGetFullPath(arg));
                    break;
            }
        }

        return new CommandLineArgs { Files = files, Timing = timing, NewInstance = newInstance, Help = help };
    }

    private static string TryGetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception e) when (e is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return path;
        }
    }

    public const string HelpText = """
        Skopos — a fast code viewer with syntax highlighting.

        Usage:
          skopos [options] [file…]

        Options:
          --timing         print the startup breakdown to stderr
          --new-instance   do not hand files to a running instance
          -h, --help       this help
        """;
}
