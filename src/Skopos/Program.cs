using Avalonia;
using Skopos.Core;
using Skopos.Core.Diagnostics;
using Skopos.Core.SingleInstance;

namespace Skopos;

internal static class Program
{
    // Nothing from Avalonia, and nothing depending on the SynchronizationContext, may run before AppMain.
    [STAThread]
    public static void Main(string[] args)
    {
        var cli = CommandLineArgs.Parse(args);
        StartupTiming.Start(cli.Timing);
        StartupTiming.Mark("Main()");

        if (cli.Help)
        {
            Console.WriteLine(CommandLineArgs.HelpText);
            return;
        }

        // First try to hand the files to a running instance. If that works we are done —
        // the user simply sees the window come forward with a new tab.
        if (!cli.NewInstance && cli.Files.Count > 0 && SingleInstanceGate.TrySendToRunningInstance(cli.Files))
            return;

        SingleInstanceGate? gate = null;
        if (!cli.NewInstance)
        {
            gate = SingleInstanceGate.TryAcquire();

            // Race between two processes starting at once: someone else created the pipe in the
            // meantime, so try sending a second time and let theirs be the one that runs.
            if (gate is null && cli.Files.Count > 0 && SingleInstanceGate.TrySendToRunningInstance(cli.Files))
                return;
        }

        StartupTiming.Mark("single instance");
        AppHost.Configure(cli, gate);

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            gate?.Dispose();
        }
    }

    // Avalonia configuration — do not delete, the visual designer uses it too.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
}
