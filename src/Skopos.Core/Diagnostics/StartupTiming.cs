using System.Diagnostics;
using System.Globalization;

namespace Skopos.Core.Diagnostics;

/// <summary>
/// Measures startup from the first line of <c>Main()</c> to the first window paint.
/// Logged to stderr behind <c>--timing</c> so CI can compare it against a baseline.
/// </summary>
public static class StartupTiming
{
    private static readonly Stopwatch Clock = new();
    private static readonly List<(string Label, double Ms)> Marks = [];

    public static bool Enabled { get; private set; }

    /// <summary>The very first call in <c>Main()</c>.</summary>
    public static void Start(bool enabled)
    {
        Enabled = enabled;
        if (!enabled) return;
        Clock.Start();
    }

    public static void Mark(string label)
    {
        if (!Enabled) return;
        lock (Marks) Marks.Add((label, Clock.Elapsed.TotalMilliseconds));
    }

    /// <summary>Prints the phase table. Called after the first window paint.</summary>
    public static void Report()
    {
        if (!Enabled) return;

        lock (Marks)
        {
            var previous = 0.0;
            Console.Error.WriteLine("skopos --timing");
            foreach (var (label, ms) in Marks)
            {
                var delta = ms - previous;
                previous = ms;
                Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"  {label,-32} {delta,7:F1} ms   (total {ms,7:F1} ms)"));
            }
        }
    }
}
