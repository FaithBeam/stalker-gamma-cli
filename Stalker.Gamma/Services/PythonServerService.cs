using System.Diagnostics;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.Services;

public class PythonServerService : IDisposable
{
    public PythonServerService(StalkerGammaSettings settings)
    {
        _settings = settings;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    public void Start(string host, ushort port, CancellationToken ct = default)
    {
        if (_process is not null && !_process.HasExited)
        {
            throw new PythonServerServiceException("Python server already running");
        }
        _process = new Process();
        _process.StartInfo = new ProcessStartInfo
        {
            FileName = PythonServerPath,
            Arguments = $"--host {host} --port {port}",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        _process.EnableRaisingEvents = true;
        _process.Start();

        ct.Register(Kill);
    }

    public void Dispose()
    {
        Kill();
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        Console.CancelKeyPress -= OnCancelKeyPress;
    }

    private void Kill()
    {
        var p = Interlocked.Exchange(ref _process, null);
        if (p is { HasExited: false })
            p.Kill();
    }

    private void OnProcessExit(object? sender, EventArgs e) => Kill();

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e) => Kill();

    private Process? _process;
    private readonly StalkerGammaSettings _settings;
    private string PythonServerPath => _settings.PythonServerPath;
}

public class PythonServerServiceException(string message) : Exception(message);
