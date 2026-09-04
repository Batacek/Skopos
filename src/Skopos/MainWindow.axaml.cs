using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Skopos.Controls;
using Skopos.Core.Files;

namespace Skopos;

public partial class MainWindow : Window
{
    private readonly List<DocumentTab> _tabs = [];

    public MainWindow()
    {
        InitializeComponent();

        SettingsButton.Click += (_, _) => OpenSettings();
        EncodingButton.Click += (_, _) => ShowEncodingMenu();
        WordWrapButton.Click += (_, _) => Active?.ToggleWordWrap();

        Tabs.SelectionChanged += (_, _) => UpdateStatus();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        DragDrop.SetAllowDrop(this, true);

        AppHost.SettingsChanged += ApplyWindowSettings;
        Closed += (_, _) => OnClosed();

        UpdateEmptyState();
        UpdateStatus();
    }

    private DocumentTab? Active => (Tabs.SelectedItem as TabItem)?.Content as DocumentTab;

    // --- opening and closing tabs -------------------------------------------

    public void OpenFiles(IReadOnlyList<string> paths)
    {
        DocumentTab? last = null;
        foreach (var path in paths) last = OpenFile(path) ?? last;
        if (last is not null) SelectTab(last);
    }

    public DocumentTab? OpenFile(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception e) when (e is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return null;
        }

        // A duplicate file is not opened twice — switch to the existing tab instead.
        var existing = _tabs.FirstOrDefault(t => string.Equals(t.FilePath, fullPath, PathComparison));
        if (existing is not null)
        {
            existing.Reload(preserveScroll: true);
            SelectTab(existing);
            return existing;
        }

        var tab = new DocumentTab();
        tab.StatusChanged += OnTabStatusChanged;
        tab.CloseRequested += CloseTab;
        tab.Load(fullPath);

        var item = new TabItem { Content = tab };
        item.Classes.Add("document");
        item.Header = BuildHeader(tab);
        item.PointerReleased += OnTabPointerReleased;

        _tabs.Add(tab);
        Tabs.Items.Add(item);
        SelectTab(tab);
        UpdateEmptyState();
        return tab;
    }

    /// <summary>Tab header: file name and a close cross. Italics for a deleted file come from the style.</summary>
    private Control BuildHeader(DocumentTab tab)
    {
        var title = new TextBlock
        {
            Text = tab.FileName,
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.Classes.Add("title");

        var close = new Button { Content = "✕" };
        close.Classes.Add("tabClose");
        close.Click += (_, e) =>
        {
            e.Handled = true;
            CloseTab(tab);
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        panel.Children.Add(title);
        panel.Children.Add(close);

        ToolTip.SetTip(panel, tab.FilePath);
        return panel;
    }

    /// <summary>The middle mouse button closes a tab.</summary>
    private void OnTabPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Middle) return;
        if (sender is TabItem { Content: DocumentTab tab }) CloseTab(tab);
    }

    private void CloseTab(DocumentTab tab)
    {
        if (FindItem(tab) is not { } item) return;

        item.PointerReleased -= OnTabPointerReleased;
        tab.StatusChanged -= OnTabStatusChanged;
        tab.CloseRequested -= CloseTab;

        Tabs.Items.Remove(item);
        _tabs.Remove(tab);
        tab.Dispose();

        UpdateEmptyState();
        UpdateStatus();
    }

    private TabItem? FindItem(DocumentTab tab) =>
        Tabs.Items.OfType<TabItem>().FirstOrDefault(i => ReferenceEquals(i.Content, tab));

    private void SelectTab(DocumentTab tab)
    {
        if (FindItem(tab) is not { } item) return;

        Tabs.SelectedItem = item;
        tab.FocusEditor();
    }

    private void SelectByOffset(int offset)
    {
        if (_tabs.Count == 0) return;

        var index = Tabs.SelectedIndex + offset;
        if (index < 0) index = _tabs.Count - 1;
        if (index >= _tabs.Count) index = 0;
        Tabs.SelectedIndex = index;
    }

    private void UpdateEmptyState()
    {
        EmptyState.IsVisible = _tabs.Count == 0;
        Tabs.IsVisible = _tabs.Count > 0;
    }

    // --- status bar ----------------------------------------------------------

    private void OnTabStatusChanged(DocumentTab tab)
    {
        if (ReferenceEquals(tab, Active)) UpdateStatus();

        if (FindItem(tab) is not { } item) return;
        if (tab.IsMissing) item.Classes.Add("missing");
        else item.Classes.Remove("missing");
    }

    private void UpdateStatus()
    {
        if (Active is not { } tab)
        {
            StatusLanguage.Text = "—";
            EncodingButton.Content = "—";
            StatusLineEnding.Text = "—";
            StatusLineCount.Text = string.Empty;
            StatusCaret.Text = string.Empty;
            WordWrapButton.Content = string.Empty;
            Title = "Skopos";
            return;
        }

        StatusLanguage.Text = tab.LanguageName;

        // Flag the heuristic as uncertain — mangled accented characters are then
        // a clear hint about where to click.
        EncodingButton.Content = tab.EncodingIsReliable ? tab.EncodingName : tab.EncodingName + " ?";

        StatusLineEnding.Text = tab.LineEndingIsMixed ? tab.LineEndingName + " ⚠" : tab.LineEndingName;
        StatusLineCount.Text = FormatLineCount(tab.LineCount);
        StatusCaret.Text = $"Ln {tab.CaretLine}, Col {tab.CaretColumn}";
        WordWrapButton.Content = tab.WordWrap ? "wrap" : "no wrap";

        Title = tab.IsMissing ? $"{tab.FileName} (deleted) — Skopos" : $"{tab.FileName} — Skopos";
    }

    private static string FormatLineCount(int count) => count == 1 ? "1 line" : $"{count} lines";

    private void ShowEncodingMenu()
    {
        if (Active is not { } tab) return;

        var menu = new MenuFlyout();
        foreach (var name in TextEncodingDetector.SelectableEncodings)
        {
            var captured = name;
            var item = new MenuItem { Header = captured };
            item.Click += (_, _) => tab.SetEncoding(captured);
            menu.Items.Add(item);
        }

        menu.ShowAt(EncodingButton);
    }

    // --- drag and drop --------------------------------------------------------

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is not { } files) return;

        OpenFiles(ToLocalPaths(files));
        BringToFront();
        e.Handled = true;
    }

    private static List<string> ToLocalPaths(IEnumerable<IStorageItem> items) => items
        .Select(item => item.TryGetLocalPath())
        .Where(path => !string.IsNullOrEmpty(path))
        .Select(path => path!)
        .ToList();

    // --- keyboard shortcuts ---------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        // Cmd instead of Ctrl on macOS.
        var command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        var isCommand = e.KeyModifiers.HasFlag(command);
        var isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            Active?.ToggleWordWrap();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F3)
        {
            if (isShift) Active?.FindPrevious();
            else Active?.FindNext();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5)
        {
            Active?.Reload(preserveScroll: true);
            e.Handled = true;
            return;
        }

        if (!isCommand) return;

        switch (e.Key)
        {
            case Key.O:
                _ = OpenFileDialogAsync();
                break;

            case Key.W:
                if (Active is { } toClose) CloseTab(toClose);
                break;

            case Key.F:
                Active?.ShowSearch();
                break;

            case Key.G:
                _ = GoToLineAsync();
                break;

            case Key.Tab:
                SelectByOffset(isShift ? -1 : 1);
                break;

            case Key.OemComma:
                OpenSettings();
                break;

            case Key.OemOpenBrackets when isShift:
                Active?.FoldAll();
                break;

            case Key.OemCloseBrackets when isShift:
                Active?.UnfoldAll();
                break;

            case Key.OemPlus or Key.Add:
                Active?.ChangeFontScale(+0.1);
                break;

            case Key.OemMinus or Key.Subtract:
                Active?.ChangeFontScale(-0.1);
                break;

            case Key.D0 or Key.NumPad0:
                Active?.ResetFontScale();
                break;

            case >= Key.D1 and <= Key.D9:
                var index = e.Key - Key.D1;
                if (index < _tabs.Count) Tabs.SelectedIndex = index;
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    private async Task OpenFileDialogAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open file",
            AllowMultiple = true,
        });

        OpenFiles(ToLocalPaths(files));
    }

    private async Task GoToLineAsync()
    {
        if (Active is not { } tab) return;

        var dialog = new GoToLineWindow();
        dialog.Initialize(tab.LineCount, tab.CaretLine);

        var line = await dialog.ShowDialog<int?>(this);
        if (line is { } value) tab.GoToLine(value);
    }

    private void OpenSettings() => _ = new SettingsWindow().ShowDialog(this);

    // --- window ----------------------------------------------------------------

    /// <summary>After a message from a second instance the user must not notice nothing started.</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private void ApplyWindowSettings()
    {
        // The size is taken over only once, after settings load lazily.
        AppHost.SettingsChanged -= ApplyWindowSettings;

        var window = AppHost.Settings.Window;
        if (!window.RememberSize) return;

        Width = window.Width;
        Height = window.Height;
    }

    private void OnClosed()
    {
        AppHost.SettingsChanged -= ApplyWindowSettings;

        if (AppHost.Settings.Window.RememberSize)
        {
            var settings = AppHost.Settings;
            settings.Window.Width = Width;
            settings.Window.Height = Height;
            AppHost.SettingsStore.Save(settings);
        }

        foreach (var tab in _tabs) tab.Dispose();
        _tabs.Clear();
    }

    /// <summary>Paths are case-sensitive on Linux, not elsewhere.</summary>
    private static StringComparison PathComparison => OperatingSystem.IsLinux()
        ? StringComparison.Ordinal
        : StringComparison.OrdinalIgnoreCase;
}
