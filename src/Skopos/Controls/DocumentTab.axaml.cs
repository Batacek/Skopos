using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Search;
using Skopos.Core.Files;
using Skopos.Core.Settings;
using Skopos.Highlighting;
using Skopos.Highlighting.Rendering;
using Skopos.Services;

namespace Skopos.Controls;

/// <summary>
/// One open file: editor, folding, search and the values for the status bar.
/// No MVVM and no bindings — code-behind is the right call here.
/// </summary>
public partial class DocumentTab : UserControl, IDisposable
{
    private readonly SyntaxColorizer _colorizer = new();
    private FoldingManager? _foldingManager;
    private SearchPanel? _searchPanel;
    private FileChangeWatcher? _watcher;
    private CancellationTokenSource? _analysisCts;

    private LoadedDocument? _document;
    private LanguageDefinition? _language;
    private DetectedEncoding? _forcedEncoding;
    private bool _forceText;
    private bool _highlightingOverridden;
    private double _fontScale = 1.0;
    private bool _wordWrapOverridden;

    /// <summary>State changed enough that the status bar has to read the new values.</summary>
    public event Action<DocumentTab>? StatusChanged;

    /// <summary>The user wants this tab closed (middle mouse button, close cross).</summary>
    public event Action<DocumentTab>? CloseRequested;

    public DocumentTab()
    {
        InitializeComponent();

        _searchPanel = SearchPanel.Install(Editor);
        // Search has no regex and no replace.
        _searchPanel.UseRegex = false;
        _searchPanel.IsReplaceMode = false;

        Editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        Editor.TextArea.Caret.PositionChanged += (_, _) => StatusChanged?.Invoke(this);

        ShowAnywayButton.Click += (_, _) =>
        {
            _forceText = true;
            Reload(preserveScroll: false);
        };

        ThemeManager.PaletteChanged += ApplyPalette;
        AppHost.SettingsChanged += ApplySettings;
    }

    // --- values for the status bar -----------------------------------------

    public string FilePath { get; private set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath) is { Length: > 0 } name ? name : "(untitled)";

    public string LanguageName => _language?.DisplayName ?? "Text";
    public string EncodingName => _document?.Encoding.DisplayName ?? "—";
    public bool EncodingIsReliable => _document?.Encoding.IsReliable ?? true;
    public string LineEndingName => (_document?.LineEnding ?? LineEnding.None).ToDisplayString();
    public bool LineEndingIsMixed => _document?.LineEnding == LineEnding.Mixed;
    public int LineCount => _document?.LineCount ?? 0;
    public bool IsMissing { get; private set; }
    public bool WordWrap => Editor.WordWrap;

    public int CaretLine => Editor.TextArea.Caret.Line;
    public int CaretColumn => Editor.TextArea.Caret.Column;

    // --- loading ------------------------------------------------------------

    public void Load(string path)
    {
        FilePath = Path.GetFullPath(path);
        _language = LanguageRegistry.Detect(FilePath);

        _watcher?.Dispose();
        _watcher = new FileChangeWatcher(FilePath);
        _watcher.Changed += () => Dispatcher.UIThread.Post(() => Reload(preserveScroll: true));
        _watcher.Deleted += () => Dispatcher.UIThread.Post(MarkMissing);

        ApplySettings();
        Reload(preserveScroll: false);
    }

    public void Reload(bool preserveScroll)
    {
        var verticalOffset = preserveScroll ? Editor.VerticalOffset : 0;
        var horizontalOffset = preserveScroll ? Editor.HorizontalOffset : 0;
        var caretOffset = preserveScroll ? Editor.CaretOffset : 0;

        var options = new FileLoadOptions { ForcedEncoding = _forcedEncoding, ForceText = _forceText };
        var document = TextFileLoader.Load(FilePath, options);
        _document = document;

        IsMissing = document.Error is not null && !File.Exists(FilePath);
        ErrorOverlay.IsVisible = document.Error is not null;
        ErrorText.Text = document.Error is null
            ? string.Empty
            : IsMissing
                ? $"The file {FileName} no longer exists."
                : $"The file {FileName} could not be opened.\n{document.Error}";

        BinaryOverlay.IsVisible = document is { IsBinary: true, Text.Length: 0 };
        BinaryText.Text = $"Binary file ({TextFileLoader.FormatSize(document.SizeBytes)})";

        Editor.Document = new TextDocument(document.Text);
        Editor.IsReadOnly = true;

        // FoldingManager binds to a specific document, so swapping it out demands a new one too.
        ResetFolding();

        // The file changed under us — reload and keep the scroll position.
        if (preserveScroll)
        {
            Editor.CaretOffset = Math.Clamp(caretOffset, 0, Editor.Document.TextLength);
            Editor.ScrollToVerticalOffset(verticalOffset);
            Editor.ScrollToHorizontalOffset(horizontalOffset);
        }

        ApplyWordWrap();
        StartAnalysis(document);
        StatusChanged?.Invoke(this);
    }

    private void ResetFolding()
    {
        if (_foldingManager is not null) FoldingManager.Uninstall(_foldingManager);
        _foldingManager = FoldingManager.Install(Editor.TextArea);
    }

    private void MarkMissing()
    {
        // Keep the tab open, just mark it. The content stays as last loaded.
        IsMissing = true;
        StatusChanged?.Invoke(this);
    }

    // --- highlighting --------------------------------------------------------

    private void StartAnalysis(LoadedDocument document)
    {
        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        _analysisCts = null;

        _colorizer.Clear();
        _foldingManager?.Clear();
        HideNotice();

        if (document.Text.Length == 0 || _language is null) return;

        var settings = AppHost.Settings.Highlighting;
        if (!settings.Enabled && !_highlightingOverridden) return;

        var megabytes = document.SizeBytes / (1024.0 * 1024.0);

        if (megabytes > settings.MaxFileSizeMb && !_highlightingOverridden)
        {
            ShowNotice(
                $"Highlighting off (large file, {TextFileLoader.FormatSize(document.SizeBytes)})",
                "Turn on",
                () =>
                {
                    _highlightingOverridden = true;
                    StartAnalysis(document);
                });
            return;
        }

        var analyzer = AppHost.Analyzers.Get(_language);
        if (analyzer is null)
        {
            ShowNotice($"No grammar available for {_language.DisplayName} — shown without highlighting.", null, null);
            return;
        }

        if (analyzer.IsInlinePassMissing)
            ShowNotice("The inline markdown grammar is missing — bold, italics and links stay uncoloured.", null, null);

        // Small files parse synchronously, so colouring is done with the first frame.
        if (megabytes <= settings.AsyncThresholdMb)
        {
            ApplyAnalysis(analyzer.Analyze(document.Text));
            return;
        }

        // Larger files: window and text immediately, colouring arrives shortly after.
        var cts = new CancellationTokenSource();
        _analysisCts = cts;
        var text = document.Text;

        _ = Task.Run(() =>
        {
            try
            {
                var analysis = analyzer.Analyze(text, cts.Token);
                Dispatcher.UIThread.Post(() =>
                {
                    if (!cts.IsCancellationRequested) ApplyAnalysis(analysis);
                });
            }
            catch (OperationCanceledException)
            {
                // Another file was opened meanwhile — nobody cares about this result.
            }
        }, cts.Token);
    }

    private void ApplyAnalysis(SyntaxAnalysis analysis)
    {
        _colorizer.SetPalette(ThemeManager.Palette);
        _colorizer.SetSpans(analysis.Spans);
        Editor.TextArea.TextView.Redraw();

        if (_foldingManager is not null && Editor.Document is { } document)
            TreeSitterFoldingStrategy.Apply(_foldingManager, document, analysis.Folds);
    }

    // --- settings and appearance ---------------------------------------------

    private void ApplySettings()
    {
        var settings = AppHost.Settings;

        Editor.FontFamily = MonospaceFonts.Resolve(settings.Font.Family);
        Editor.FontSize = settings.Font.Size * _fontScale;
        Editor.ShowLineNumbers = settings.Editor.ShowLineNumbers;
        Editor.Options.IndentationSize = settings.Editor.TabWidth;
        Editor.Options.ShowSpaces = settings.Editor.ShowWhitespace;
        Editor.Options.ShowTabs = settings.Editor.ShowWhitespace;

        ApplyWordWrap();
        ApplyPalette();
    }

    private void ApplyPalette()
    {
        var palette = ThemeManager.Palette;

        Editor.Background = new SolidColorBrush(palette.Background);
        Editor.Foreground = new SolidColorBrush(palette.Foreground);
        Editor.LineNumbersForeground = new SolidColorBrush(palette.LineNumber);
        Editor.TextArea.SelectionBrush = new SolidColorBrush(palette.Selection);
        BinaryOverlay.Background = new SolidColorBrush(palette.Background);
        ErrorOverlay.Background = new SolidColorBrush(palette.Background);

        _colorizer.SetPalette(palette);
        Editor.TextArea.TextView.Redraw();
    }

    /// <summary>"auto" means by language — markdown and plain text wrap, code does not.</summary>
    private void ApplyWordWrap()
    {
        if (_wordWrapOverridden) return;

        Editor.WordWrap = AppHost.Settings.Editor.WordWrap switch
        {
            WordWrapMode.On => true,
            WordWrapMode.Off => false,
            _ => _language?.WordWrapByDefault ?? true,
        };
    }

    // --- commands from keyboard shortcuts ------------------------------------

    public void ToggleWordWrap()
    {
        _wordWrapOverridden = true;
        Editor.WordWrap = !Editor.WordWrap;
        StatusChanged?.Invoke(this);
    }

    public void ShowSearch()
    {
        _searchPanel?.Open();
        Dispatcher.UIThread.Post(() => _searchPanel?.Reactivate(), DispatcherPriority.Input);
    }

    public void FindNext() => _searchPanel?.FindNext(Editor.CaretOffset);
    public void FindPrevious() => _searchPanel?.FindPrevious();

    public void FoldAll() => SetAllFoldings(folded: true);
    public void UnfoldAll() => SetAllFoldings(folded: false);

    private void SetAllFoldings(bool folded)
    {
        if (_foldingManager is null) return;
        foreach (var folding in _foldingManager.AllFoldings) folding.IsFolded = folded;
    }

    public void ChangeFontScale(double delta)
    {
        _fontScale = Math.Clamp(_fontScale + delta, 0.5, 4.0);
        Editor.FontSize = AppHost.Settings.Font.Size * _fontScale;
    }

    public void ResetFontScale()
    {
        _fontScale = 1.0;
        Editor.FontSize = AppHost.Settings.Font.Size;
    }

    /// <summary>
    /// Manual encoding switch from the status bar. The choice holds for this file for
    /// the rest of the session — detection is not consulted again on reload.
    /// </summary>
    public void SetEncoding(string displayName)
    {
        _forcedEncoding = TextEncodingDetector.FromDisplayName(displayName);
        Reload(preserveScroll: true);
    }

    public void GoToLine(int line)
    {
        if (Editor.Document is not { } document) return;

        var target = Math.Clamp(line, 1, document.LineCount);
        Editor.ScrollToLine(target);
        Editor.CaretOffset = document.GetLineByNumber(target).Offset;
        Editor.Focus();
    }

    public void FocusEditor() => Editor.TextArea.Focus();

    // --- notice bar -----------------------------------------------------------

    private void ShowNotice(string text, string? actionLabel, Action? action)
    {
        NoticeText.Text = text;
        NoticeBar.IsVisible = true;

        NoticeAction.IsVisible = actionLabel is not null;
        NoticeAction.Content = actionLabel;

        // Without unsubscribing, the action would fire twice after a second showing.
        NoticeAction.Click -= OnNoticeAction;
        _noticeAction = action;
        if (action is not null) NoticeAction.Click += OnNoticeAction;
    }

    private Action? _noticeAction;

    private void OnNoticeAction(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HideNotice();
        _noticeAction?.Invoke();
    }

    private void HideNotice()
    {
        NoticeBar.IsVisible = false;
        NoticeAction.Click -= OnNoticeAction;
        _noticeAction = null;
    }

    public void RequestClose() => CloseRequested?.Invoke(this);

    public void Dispose()
    {
        ThemeManager.PaletteChanged -= ApplyPalette;
        AppHost.SettingsChanged -= ApplySettings;

        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        _watcher?.Dispose();

        if (_foldingManager is not null)
        {
            FoldingManager.Uninstall(_foldingManager);
            _foldingManager = null;
        }

        _searchPanel = null;
    }
}
