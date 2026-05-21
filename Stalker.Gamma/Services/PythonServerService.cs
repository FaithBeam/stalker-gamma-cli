using System.Diagnostics;
using System.Reactive.Subjects;
using Stalker.Gamma.Models;
using Stalker.Gamma.Proxies;

namespace Stalker.Gamma.Services;

public class PythonServerService(StalkerGammaSettings settings, PythonApiProxy pythonApiProxy)
    : IDisposable
{
    public Subject<bool> ReadySubject { get; } = new();

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_process is not null && !_process.HasExited)
        {
            throw new PythonServerServiceException("Python server already running");
        }
        _process = new Process();
        _process.StartInfo = new ProcessStartInfo
        {
            FileName = PythonServerPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        _process.EnableRaisingEvents = true;
        _process.Start();

        ct.Register(() => _process?.Kill());

        while (!await _pythonApiProxy.Ready())
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
        ReadySubject.OnNext(true);
        ReadySubject.OnCompleted();
    }

    public void Dispose()
    {
        _process?.Kill();
    }

    private Process? _process;
    private string PythonServerPath => settings.PythonServerPath;
    private readonly PythonApiProxy _pythonApiProxy = pythonApiProxy;
}

public class PythonServerServiceException(string message) : Exception(message);
