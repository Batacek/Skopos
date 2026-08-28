using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Skopos.Core.Diagnostics;
using Skopos.Services;

namespace Skopos;

public partial class App : Application
{
    private MainWindow? _window;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        StartupTiming.Mark("Avalonia init");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ThemeManager.Attach(this);

            _window = new MainWindow();
            desktop.MainWindow = _window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.Exit += (_, _) => AppHost.Analyzers.Dispose();

            // Never block the first paint. The files from the command line are the
            // reason we started, so open them right away; everything else (settings, pipe,
            // file watching) waits until after the first frame.
            _window.OpenFiles(AppHost.Args.Files);
            StartupTiming.Mark("files from argv");

            _window.Opened += OnFirstShown;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnFirstShown(object? sender, EventArgs e)
    {
        if (_window is null) return;
        _window.Opened -= OnFirstShown;

        StartupTiming.Mark("first paint");
        StartupTiming.Report();

        // Only now — nothing may touch the disk more than strictly necessary
        // before the first window.
        Dispatcher.UIThread.Post(async void () =>
        {
            var settings = await AppHost.SettingsStore.LoadAsync().ConfigureAwait(true);
            AppHost.ApplySettings(settings);
            ThemeManager.Apply(this, settings.Theme);

            StartListening();
        }, DispatcherPriority.Background);
    }

    /// <summary>Message received → new tab and the window comes forward.</summary>
    private void StartListening()
    {
        if (AppHost.Gate is not { } gate) return;

        gate.FilesReceived += paths => Dispatcher.UIThread.Post(() =>
        {
            if (_window is null) return;
            _window.OpenFiles(paths);
            _window.BringToFront();
        });

        gate.StartListening();
    }
}
