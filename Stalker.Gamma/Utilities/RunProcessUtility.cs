using System.Diagnostics;

namespace Stalker.Gamma.Utilities;

public static partial class RunProcessUtility
{
    public static async Task<int> RunProcessAsync(
        string fileName,
        IEnumerable<string> arguments,
        Action<string> onStdout,
        Action<string> onStderr,
        string? workingDirectory = null,
        CancellationToken ct = default
    )
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = string.Join(' ', arguments),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };
        process.EnableRaisingEvents = true;

        process.Start();

        // Read both streams concurrently to avoid deadlocks
        var stdoutTask = ReadStreamAsync(process.StandardOutput, onStdout, ct);
        var stderrTask = ReadStreamAsync(process.StandardError, onStderr, ct);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        return process.ExitCode;
    }

    private static async Task ReadStreamAsync(
        StreamReader reader,
        Action<string> onLine,
        CancellationToken ct
    )
    {
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            ct.ThrowIfCancellationRequested();
            onLine(line);
        }
    }
}
