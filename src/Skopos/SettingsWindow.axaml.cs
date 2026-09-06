using Avalonia;
using Avalonia.Controls;
using Skopos.Core.Settings;
using Skopos.Services;

namespace Skopos;

/// <summary>The one settings window, opened by the gear or Ctrl+,.</summary>
public partial class SettingsWindow : Window
{
    private const string SystemFontLabel = "(per platform)";

    private static readonly (string Label, ThemePreference Value)[] Themes =
    [
        ("Follow system", ThemePreference.System),
        ("Light", ThemePreference.Light),
        ("Dark", ThemePreference.Dark),
    ];

    private static readonly (string Label, WordWrapMode Value)[] WrapModes =
    [
        ("By language", WordWrapMode.Auto),
        ("Always", WordWrapMode.On),
        ("Never", WordWrapMode.Off),
    ];

    public SettingsWindow()
    {
        InitializeComponent();

        ThemeBox.ItemsSource = Themes.Select(t => t.Label).ToList();
        WordWrapBox.ItemsSource = WrapModes.Select(w => w.Label).ToList();

        // Font enumeration is slow, hence here and not at startup.
        var fonts = new List<string> { SystemFontLabel };
        fonts.AddRange(MonospaceFonts.Enumerate());
        FontBox.ItemsSource = fonts;

        Load(AppHost.Settings);

        SaveButton.Click += (_, _) => Save();
        CancelButton.Click += (_, _) => Close();

        var store = AppHost.SettingsStore;
        LocationText.Text = store.IsPortable
            ? $"Portable mode — settings are stored in {store.Path}"
            : $"Settings: {store.Path}";
    }

    private void Load(SkoposSettings settings)
    {
        ThemeBox.SelectedIndex = Array.FindIndex(Themes, t => t.Value == settings.Theme);
        WordWrapBox.SelectedIndex = Array.FindIndex(WrapModes, w => w.Value == settings.Editor.WordWrap);

        FontBox.SelectedItem = string.IsNullOrWhiteSpace(settings.Font.Family)
            ? SystemFontLabel
            : settings.Font.Family;
        if (FontBox.SelectedItem is null) FontBox.SelectedIndex = 0;

        FontSizeBox.Value = (decimal)settings.Font.Size;
        LineNumbersBox.IsChecked = settings.Editor.ShowLineNumbers;
        WhitespaceBox.IsChecked = settings.Editor.ShowWhitespace;
        TabWidthBox.Value = settings.Editor.TabWidth;

        HighlightingBox.IsChecked = settings.Highlighting.Enabled;
        MaxSizeBox.Value = (decimal)settings.Highlighting.MaxFileSizeMb;
        AsyncSizeBox.Value = (decimal)settings.Highlighting.AsyncThresholdMb;

        RememberSizeBox.IsChecked = settings.Window.RememberSize;
    }

    private void Save()
    {
        var settings = AppHost.Settings.Clone();

        if (ThemeBox.SelectedIndex >= 0) settings.Theme = Themes[ThemeBox.SelectedIndex].Value;
        if (WordWrapBox.SelectedIndex >= 0) settings.Editor.WordWrap = WrapModes[WordWrapBox.SelectedIndex].Value;

        var font = FontBox.SelectedItem as string;
        settings.Font.Family = font is null or SystemFontLabel ? string.Empty : font;
        settings.Font.Size = (double)(FontSizeBox.Value ?? 13m);

        settings.Editor.ShowLineNumbers = LineNumbersBox.IsChecked ?? true;
        settings.Editor.ShowWhitespace = WhitespaceBox.IsChecked ?? false;
        settings.Editor.TabWidth = (int)(TabWidthBox.Value ?? 4m);

        settings.Highlighting.Enabled = HighlightingBox.IsChecked ?? true;
        settings.Highlighting.MaxFileSizeMb = (double)(MaxSizeBox.Value ?? 20m);
        settings.Highlighting.AsyncThresholdMb = Math.Min(
            (double)(AsyncSizeBox.Value ?? 2m),
            settings.Highlighting.MaxFileSizeMb);

        settings.Window.RememberSize = RememberSizeBox.IsChecked ?? true;

        AppHost.SaveSettings(settings);
        if (Application.Current is { } application) ThemeManager.Apply(application, settings.Theme);

        Close();
    }
}
