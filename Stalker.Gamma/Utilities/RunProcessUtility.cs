using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace Stalker.Gamma.Utilities;

public static partial class RunProcessUtility
{
    public static async Task<int> RunProcessAsync(
        string fileName,
        IEnumerable<string> arguments,
        Action<string> onStdout,
        Action<string> onStderr,
        string? workingDirectory = null,
        Encoding? stdOutEncoding = null,
        CancellationToken ct = default
    )
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = string.Join(' ', arguments.Select(x => $"\"{x}\"")),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
            StandardOutputEncoding = stdOutEncoding,
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
                    // 7z updates the terminal with \b control characters, se we consider everything in sb as a line
                    if (buffer[i] == '\n' || buffer[i] == '\r' || buffer[i] == '\b')
                    {
                        onLine(sb.ToString());
                        sb.Clear();
                    }
                    else
                    {
                        sb.Append(buffer[i]);
                    }
                }
            }

            if (sb.Length > 0)
            {
                onLine(sb.ToString());
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}
