using System.Text;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.Utilities;

public class UnzipUtility(StalkerGammaSettings settings)
{
    public async Task ExtractAsync(
        string archivePath,
        string extractDirectory,
        Action<double>? onProgress,
        CancellationToken ct
    ) =>
        await ExecuteUnzipCmdAsync(
            ["-o", $"\"{archivePath}\"", "-d", $"\"{extractDirectory}\""],
            onProgress: onProgress,
            cancellationToken: ct
        );

    public bool Ready =>
        File.Exists(settings.PathToUnzip)
        || EnvChecker.IsInPath(OperatingSystem.IsWindows() ? "unzip.exe" : "unzip");

    private async Task<StdOutStdErrOutput> ExecuteUnzipCmdAsync(
        string[] args,
        string? workingDirectory = null,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        var exitCode = await RunProcessUtility.RunProcessAsync(
            settings.PathToUnzip,
            args,
            onStdout: line => stdOut.AppendLine(line),
            onStderr: line => stdErr.AppendLine(line),
            workingDirectory,
            ct: cancellationToken
        );
        if (exitCode != 0)
        {
            if (!stdErr.ToString().Contains("appears to use backslashes as path separators"))
            {
                throw new UnzipUtilityException(
                    $"""
                    Error executing unzip
                    {string.Join(' ', args)}
                    StdOut: {stdOut}
                    StdErr: {stdErr}
                    Exit Code: {exitCode}
                    """
                );
            }
        }

        onProgress?.Invoke(1);

        return new StdOutStdErrOutput(stdOut.ToString(), stdErr.ToString());
    }
}

public class UnzipUtilityException(string message) : Exception(message);
