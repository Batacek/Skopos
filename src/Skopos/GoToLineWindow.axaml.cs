using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Skopos;

/// <summary>Go-to-line dialog (Ctrl+G). Returns a line number, or <c>null</c> when cancelled.</summary>
public partial class GoToLineWindow : Window
{
    private int _lineCount = 1;

    // A parameterless constructor is mandatory for AOT, so the parameters
    // arrive later through Initialize.
    public GoToLineWindow()
    {
        InitializeComponent();

        OkButton.Click += (_, _) => Accept();
        CancelButton.Click += (_, _) => Close(null);
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Close(null);
            e.Handled = true;
        };

        Opened += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            LineInput.Focus();
            LineInput.SelectAll();
        });
    }

    public void Initialize(int lineCount, int currentLine)
    {
        _lineCount = Math.Max(lineCount, 1);
        Prompt.Text = $"Line (1–{_lineCount}):";
        LineInput.Text = currentLine.ToString();
    }

    private void Accept()
    {
        if (!int.TryParse(LineInput.Text, out var line))
        {
            Close(null);
            return;
        }

        Close(Math.Clamp(line, 1, _lineCount));
    }
}
