using System.Text.Json.Serialization;

namespace Skopos.Core.Settings;

/// <summary>Settings schema. Properties are settable because of deserialization.</summary>
public sealed class SkoposSettings
{
    /// <summary>For future migrations. Adding it retroactively is a chore, so it is here from day one.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("theme")]
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    [JsonPropertyName("font")]
    public FontSettings Font { get; set; } = new();

    [JsonPropertyName("editor")]
    public EditorSettings Editor { get; set; } = new();

    [JsonPropertyName("highlighting")]
    public HighlightingSettings Highlighting { get; set; } = new();

    [JsonPropertyName("window")]
    public WindowSettings Window { get; set; } = new();

    public SkoposSettings Clone() => new()
    {
        Version = Version,
        Theme = Theme,
        Font = new FontSettings { Family = Font.Family, Size = Font.Size },
        Editor = new EditorSettings
        {
            ShowLineNumbers = Editor.ShowLineNumbers,
            WordWrap = Editor.WordWrap,
            TabWidth = Editor.TabWidth,
            ShowWhitespace = Editor.ShowWhitespace,
        },
        Highlighting = new HighlightingSettings
        {
            Enabled = Highlighting.Enabled,
            MaxFileSizeMb = Highlighting.MaxFileSizeMb,
            AsyncThresholdMb = Highlighting.AsyncThresholdMb,
        },
        Window = new WindowSettings
        {
            RememberSize = Window.RememberSize,
            Width = Window.Width,
            Height = Window.Height,
        },
    };
}

[JsonConverter(typeof(JsonStringEnumConverter<ThemePreference>))]
public enum ThemePreference
{
    [JsonStringEnumMemberName("system")] System,
    [JsonStringEnumMemberName("light")] Light,
    [JsonStringEnumMemberName("dark")] Dark,
}

/// <summary>"auto" means by language — code stays unwrapped, text and markdown wrap.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WordWrapMode>))]
public enum WordWrapMode
{
    [JsonStringEnumMemberName("auto")] Auto,
    [JsonStringEnumMemberName("on")] On,
    [JsonStringEnumMemberName("off")] Off,
}

public sealed class FontSettings
{
    /// <summary>
    /// Until we bundle a font of our own, we ride the chain of system
    /// monospace fonts. An empty value means "pick per platform".
    /// </summary>
    [JsonPropertyName("family")]
    public string Family { get; set; } = "";

    [JsonPropertyName("size")]
    public double Size { get; set; } = 13;
}

public sealed class EditorSettings
{
    [JsonPropertyName("showLineNumbers")]
    public bool ShowLineNumbers { get; set; } = true;

    [JsonPropertyName("wordWrap")]
    public WordWrapMode WordWrap { get; set; } = WordWrapMode.Auto;

    [JsonPropertyName("tabWidth")]
    public int TabWidth { get; set; } = 4;

    [JsonPropertyName("showWhitespace")]
    public bool ShowWhitespace { get; set; }
}

public sealed class HighlightingSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Above this, highlighting is turned off and a bar offers to switch it back on.</summary>
    [JsonPropertyName("maxFileSizeMb")]
    public double MaxFileSizeMb { get; set; } = 20;

    /// <summary>Above this, parsing moves to a background thread so the window stays responsive.</summary>
    [JsonPropertyName("asyncThresholdMb")]
    public double AsyncThresholdMb { get; set; } = 2;
}

public sealed class WindowSettings
{
    [JsonPropertyName("rememberSize")]
    public bool RememberSize { get; set; } = true;

    [JsonPropertyName("width")]
    public double Width { get; set; } = 1000;

    [JsonPropertyName("height")]
    public double Height { get; set; } = 700;
}
