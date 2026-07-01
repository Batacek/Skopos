namespace Skopos.Core.Files;

/// <summary>
/// Watches a single open file. Since we never edit, a change just means reloading;
/// a deletion is only flagged in the tab header.
/// </summary>
public sealed class FileChangeWatcher : IDisposable
{
    /// <summary>Editors write in chunks — without debouncing one save would raise five events.</summary>
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(150);

    private readonly FileSystemWatcher? _watcher;
    private readonly Timer _debounce;
    private readonly Lock _gate = new();
    private bool _disposed;

    public event Action? Changed;
    public event Action? Deleted;

    public FileChangeWatcher(string path)
    {
        _debounce = new Timer(_ => Fire(), null, Timeout.Infinite, Timeout.Infinite);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(name)) return;

        try
        {
            _watcher = new FileSystemWatcher(directory, name)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += (_, _) => Schedule();
            _watcher.Created += (_, _) => Schedule();
            // Renamed to our name = the file came back; away from our name = it is gone.
            _watcher.Renamed += (_, e) =>
            {
                if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) Schedule();
                else RaiseDeleted();
            };
            _watcher.Deleted += (_, _) => RaiseDeleted();
            // The watcher can fail (network path, overflowing buffer). Not worth dying over — just stop watching.
            _watcher.Error += (_, _) => Disable();
        }
        catch (Exception e) when (e is ArgumentException or FileNotFoundException or IOException)
        {
            _watcher = null;
        }
    }

    private void Schedule()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _debounce.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void Fire()
    {
        lock (_gate)
        {
            if (_disposed) return;
        }
        Changed?.Invoke();
    }

    private void RaiseDeleted() => Deleted?.Invoke();

    private void Disable()
    {
        if (_watcher is not null) _watcher.EnableRaisingEvents = false;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        _watcher?.Dispose();
        _debounce.Dispose();
    }
}
