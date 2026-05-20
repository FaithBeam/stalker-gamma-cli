using System.Text;
using Stalker.Gamma.Models;
using Stalker.Gamma.Services.Models;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Services;

public class TarService(StalkerGammaSettings settings)
{
    public async Task ExtractAsync(
        string archivePath,
        string extractDirectory,
        Action<double>? onProgress,
        CancellationToken cancellationToken
    )
    {
        Directory.CreateDirectory(extractDirectory);
        await ExecuteTarCmdAsync(
            ["-xzvf", archivePath, "-C", extractDirectory],
            onProgress: onProgress,
            cancellationToken: cancellationToken
        );
    }

    public bool Ready =>
        File.Exists(settings.PathToTar)
        || EnvChecker.IsInPath(OperatingSystem.IsWindows() ? "tar.exe" : "tar");

    private async Task<StdOutStdErrOutput> ExecuteTarCmdAsync(
        string[] args,
        string? workingDirectory = null,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        var exitCode = await RunProcessUtility.RunProcessAsync(
            settings.PathToTar,
            args,
            onStdout: line => stdOut.AppendLine(line),
            onStderr: line => stdErr.AppendLine(line),
            workingDirectory ?? "",
            ct: cancellationToken
        );
        if (exitCode != 0)
        {
            if (!stdErr.ToString().Contains("appears to use backslashes as path separators"))
            {
                throw new TarUtilityException(
                    $"""
                    Error executing tar
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

public class TarUtilityException : Exception
{
    public TarUtilityException(string message)
        : base(message) { }
}
