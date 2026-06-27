using Skopos.Core.Settings;
using Xunit;

namespace Skopos.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("skopos-settings").FullName;

    private string PathFor(string name) => Path.Combine(_directory, name);

    [Fact]
    public void MissingFile_GivesDefaults()
    {
        var store = new SettingsStore(PathFor("does-not-exist.json"));

        var settings = store.Load();

        Assert.Equal(1, settings.Version);
        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.Equal(WordWrapMode.Auto, settings.Editor.WordWrap);
        Assert.Equal(20, settings.Highlighting.MaxFileSizeMb);
    }

    [Fact]
    public void SaveAndLoad_PreservesEverything()
    {
        var path = PathFor("settings.json");
        var store = new SettingsStore(path);

        var settings = new SkoposSettings
        {
            Theme = ThemePreference.Dark,
            Font = new FontSettings { Family = "Cascadia Code", Size = 15 },
            Editor = new EditorSettings { ShowLineNumbers = false, WordWrap = WordWrapMode.On, TabWidth = 2, ShowWhitespace = true },
            Highlighting = new HighlightingSettings { Enabled = false, MaxFileSizeMb = 8, AsyncThresholdMb = 1 },
            Window = new WindowSettings { RememberSize = false, Width = 1234, Height = 567 },
        };

        Assert.True(store.Save(settings));

        var loaded = new SettingsStore(path).Load();

        Assert.Equal(ThemePreference.Dark, loaded.Theme);
        Assert.Equal("Cascadia Code", loaded.Font.Family);
        Assert.Equal(15, loaded.Font.Size);
        Assert.False(loaded.Editor.ShowLineNumbers);
        Assert.Equal(WordWrapMode.On, loaded.Editor.WordWrap);
        Assert.Equal(2, loaded.Editor.TabWidth);
        Assert.True(loaded.Editor.ShowWhitespace);
        Assert.False(loaded.Highlighting.Enabled);
        Assert.Equal(8, loaded.Highlighting.MaxFileSizeMb);
        Assert.False(loaded.Window.RememberSize);
        Assert.Equal(1234, loaded.Window.Width);
    }

    [Fact]
    public void EnumsAreStoredAsStrings()
    {
        // The theme is stored as a string, e.g. "theme": "system" — not a number.
        var path = PathFor("settings.json");
        new SettingsStore(path).Save(new SkoposSettings { Theme = ThemePreference.Dark });

        var json = File.ReadAllText(path);

        Assert.Contains("\"theme\": \"dark\"", json);
        Assert.Contains("\"wordWrap\": \"auto\"", json);
        Assert.Contains("\"version\": 1", json);
    }

    [Fact]
    public void BrokenJson_DoesNotTakeTheAppDown()
    {
        var path = PathFor("settings.json");
        File.WriteAllText(path, "{ this is definitely not JSON ");

        var settings = new SettingsStore(path).Load();

        Assert.Equal(ThemePreference.System, settings.Theme);
    }

    [Fact]
    public void NonsenseValues_AreClamped()
    {
        var path = PathFor("settings.json");
        File.WriteAllText(path, """
            { "version": 1, "font": { "size": 9000 }, "editor": { "tabWidth": 99 },
              "highlighting": { "maxFileSizeMb": 5, "asyncThresholdMb": 500 } }
            """);

        var settings = new SettingsStore(path).Load();

        Assert.Equal(72, settings.Font.Size);
        Assert.Equal(16, settings.Editor.TabWidth);
        Assert.Equal(5, settings.Highlighting.MaxFileSizeMb);
        // The thresholds must not cross — async never above max.
        Assert.Equal(5, settings.Highlighting.AsyncThresholdMb);
    }

    [Fact]
    public void CommentsAndTrailingCommas_AreAccepted()
    {
        var path = PathFor("settings.json");
        File.WriteAllText(path, """
            {
              // hand-edited
              "version": 1,
              "theme": "light",
            }
            """);

        Assert.Equal(ThemePreference.Light, new SettingsStore(path).Load().Theme);
    }

    [Fact]
    public void Clone_IsDeep()
    {
        var original = new SkoposSettings { Font = { Size = 13 } };
        var copy = original.Clone();

        copy.Font.Size = 20;

        Assert.Equal(13, original.Font.Size);
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
