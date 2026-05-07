using System.Buffers;
using System.Diagnostics;
using System.Reactive.Subjects;
using System.Text;
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

        await Task.Run(
            async () =>
            {
                while (!await _pythonApiProxy.Ready())
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
                ReadySubject.OnNext(true);
                ReadySubject.OnCompleted();
            },
            ct
        );

        var stdOutTask = ReadStreamAsync(_process.StandardOutput, ct);
        var stdErrTask = ReadStreamAsync(_process.StandardError, ct);

        await Task.WhenAll(stdOutTask, stdErrTask);
    }

    public void Dispose()
    {
        _process?.Kill();
    }

    private static async Task ReadStreamAsync(StreamReader reader, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = ArrayPool<char>.Shared.Rent(4096);
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                for (var i = 0; i < read; i++)
                {
                    if (buffer[i] == '\n' || buffer[i] == '\r')
                    {
                        sb.Clear();
                    }
                    else
                    {
                        sb.Append(buffer[i]);
                    }
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    private Process? _process;
    private string PythonServerPath => settings.PythonServerPath;
    private readonly PythonApiProxy _pythonApiProxy = pythonApiProxy;
}

public class PythonServerServiceException(string message) : Exception(message);
