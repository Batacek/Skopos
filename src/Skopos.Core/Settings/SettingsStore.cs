using System.Text.Json;

namespace Skopos.Core.Settings;

/// <summary>
/// Locating and loading settings. Reads are **lazy** — defaults apply immediately
/// and user values only once loaded, so the first window is never blocked.
/// </summary>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";
    private const string PortableMarker = "portable.txt";

    private readonly string _path;

    public SettingsStore(string? explicitPath = null)
    {
        _path = explicitPath ?? ResolvePath();
    }

    /// <summary>Path to the settings file, portable or system-wide.</summary>
    public string Path => _path;

    /// <summary>Are we running in portable mode?</summary>
    public bool IsPortable { get; private set; }

    /// <summary>
    /// If <c>settings.json</c> or an empty <c>portable.txt</c> sits next to the executable,
    /// we read and write there and never touch the system folders.
    /// </summary>
    private string ResolvePath()
    {
        var exeDir = AppContext.BaseDirectory;
        var portableSettings = System.IO.Path.Combine(exeDir, FileName);
        var portableMarker = System.IO.Path.Combine(exeDir, PortableMarker);

        if (File.Exists(portableSettings) || File.Exists(portableMarker))
        {
            IsPortable = true;
            return portableSettings;
        }

        // Never hard-code the %APPDATA% path — it lives elsewhere on Linux and macOS.
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(appData, "Skopos", FileName);
    }

    public SkoposSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new SkoposSettings();

            using var stream = File.Open(_path, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
            });

            var loaded = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.SkoposSettings);
            return loaded is null ? new SkoposSettings() : Migrate(loaded);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Broken settings must not take down a file viewer. Fall back to defaults.
            return new SkoposSettings();
        }
    }

    public Task<SkoposSettings> LoadAsync(CancellationToken ct = default) => Task.Run(Load, ct);

    public bool Save(SkoposSettings settings)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            // Write through a temp file so a crash mid-write cannot leave half a JSON behind.
            var temp = _path + ".tmp";
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.SkoposSettings);

            File.Move(temp, _path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Only one schema version so far — the hook exists so it is there when needed.</summary>
    private static SkoposSettings Migrate(SkoposSettings settings)
    {
        if (settings.Version < 1) settings.Version = 1;

        // Nonsense values from a hand-edited file must not break the UI.
        settings.Font.Size = Math.Clamp(settings.Font.Size, 6, 72);
        settings.Editor.TabWidth = Math.Clamp(settings.Editor.TabWidth, 1, 16);
        settings.Highlighting.MaxFileSizeMb = Math.Clamp(settings.Highlighting.MaxFileSizeMb, 0.1, 1024);
        settings.Highlighting.AsyncThresholdMb = Math.Clamp(settings.Highlighting.AsyncThresholdMb, 0.1, settings.Highlighting.MaxFileSizeMb);
        settings.Window.Width = Math.Clamp(settings.Window.Width, 320, 10000);
        settings.Window.Height = Math.Clamp(settings.Window.Height, 240, 10000);
        return settings;
    }
}
