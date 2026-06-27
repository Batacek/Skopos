using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace Skopos.Core.SingleInstance;

/// <summary>
/// Hands files opened from a second launch over to the already running instance. This is the
/// single largest startup win: without it every double-click is a new process (~150 ms);
/// with it, just a message to the running instance (~30 ms).
/// </summary>
/// <remarks>
/// .NET maps named pipes to <c>\\.\pipe\…</c> on Windows and to a domain socket in the temp
/// directory on Unix, so one implementation covers both platforms.
/// </remarks>
public sealed class SingleInstanceGate : IDisposable
{
    /// <summary>How long we wait to reach a running instance before giving up and starting ourselves.</summary>
    private const int ConnectTimeoutMs = 300;

    private readonly string _pipeName;
    private readonly CancellationTokenSource _shutdown = new();
    private NamedPipeServerStream? _server;

    /// <summary>Paths arrived from a second instance. Raised on the pipe thread — the UI must marshal itself.</summary>
    public event Action<string[]>? FilesReceived;

    private SingleInstanceGate(string pipeName) => _pipeName = pipeName;

    /// <summary>The pipe name is per user, so two users on one machine stay out of each other's way.</summary>
    public static string GetPipeName()
    {
        var id = "default";
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                id = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
            }
            else
            {
                id = Environment.GetEnvironmentVariable("UID") ?? Environment.UserName;
            }
        }
        catch (Exception e) when (e is PlatformNotSupportedException or UnauthorizedAccessException)
        {
            id = Environment.UserName;
        }

        return $"skopos-{id}";
    }

    /// <summary>
    /// Sends the paths to a running instance. Returns <c>true</c> if it took them — then
    /// <c>Environment.Exit(0)</c> is enough and the user never notices nothing started.
    /// </summary>
    public static bool TrySendToRunningInstance(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return false;

        try
        {
            using var client = new NamedPipeClientStream(".", GetPipeName(), PipeDirection.Out);
            client.Connect(ConnectTimeoutMs);

            var payload = Encoding.UTF8.GetBytes(string.Join('\n', paths));
            client.Write(payload);
            client.Flush();
            return true;
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to become the first instance. Returns <c>null</c> if someone already holds the
    /// pipe — which also settles the race between two processes starting at once.
    /// </summary>
    public static SingleInstanceGate? TryAcquire()
    {
        var gate = new SingleInstanceGate(GetPipeName());
        try
        {
            gate._server = CreateServer(gate._pipeName);
            return gate;
        }
        catch (IOException)
        {
            // Someone else created the pipe in the meantime — we are not first.
            gate.Dispose();
            return null;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Life without single-instance is fine, only slower to start. Not a reason to stop the app.
            gate.Dispose();
            return null;
        }
    }

    private static NamedPipeServerStream CreateServer(string name) => new(
        name,
        PipeDirection.In,
        NamedPipeServerStream.MaxAllowedServerInstances,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    /// <summary>Starts the accept loop in the background. Call only after the first window paint.</summary>
    public void StartListening() => _ = Task.Run(ListenLoopAsync);

    private async Task ListenLoopAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                var server = _server ?? CreateServer(_pipeName);
                _server = server;

                await server.WaitForConnectionAsync(_shutdown.Token).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                var payload = await reader.ReadToEndAsync(_shutdown.Token).ConfigureAwait(false);

                var paths = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (paths.Length > 0) FilesReceived?.Invoke(paths);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                // The client dropped mid-write. The pipe is unusable afterwards, so make a new one.
            }
            finally
            {
                if (!_shutdown.IsCancellationRequested)
                {
                    _server?.Dispose();
                    _server = null;
                }
            }
        }
    }

    public void Dispose()
    {
        if (!_shutdown.IsCancellationRequested) _shutdown.Cancel();
        _server?.Dispose();
        _server = null;
        _shutdown.Dispose();
    }
}
