using Skopos.Core;
using Skopos.Core.Settings;
using Skopos.Core.SingleInstance;
using Skopos.Services;

namespace Skopos;

/// <summary>
/// Manual service wiring instead of a DI container — this is the whole "container":
/// a static holder that works around <see cref="App"/> needing a parameterless constructor
/// for AOT.
/// </summary>
internal static class AppHost
{
    private static CommandLineArgs? _args;

    public static CommandLineArgs Args => _args ??= CommandLineArgs.Parse([]);

    public static SingleInstanceGate? Gate { get; private set; }

    public static SettingsStore SettingsStore { get; } = new();

    /// <summary>
    /// Settings load lazily. Until the first load these are the defaults, which is
    /// exactly what the first window should render with.
    /// </summary>
    public static SkoposSettings Settings { get; private set; } = new();

    public static AnalyzerCache Analyzers { get; } = new();

    /// <summary>Raised once the lazy load of settings from disk completes.</summary>
    public static event Action? SettingsChanged;

    public static void Configure(CommandLineArgs args, SingleInstanceGate? gate)
    {
        _args = args;
        Gate = gate;
    }

    public static void ApplySettings(SkoposSettings settings)
    {
        Settings = settings;
        SettingsChanged?.Invoke();
    }

    public static bool SaveSettings(SkoposSettings settings)
    {
        Settings = settings;
        var saved = SettingsStore.Save(settings);
        SettingsChanged?.Invoke();
        return saved;
    }
}
