using System.Text;
using CliWrap;
using CliWrap.Builders;
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
        await ExecuteSevenZipCmdAsync(
            ["-o", archivePath, "-d", extractDirectory],
            onProgress: onProgress,
            cancellationToken: ct
        );

    private async Task<StdOutStdErrOutput> ExecuteSevenZipCmdAsync(
        string[] args,
        string? workingDirectory = null,
        CancellationToken? cancellationToken = null,
        Action<double>? onProgress = null
    )
    {
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        try
        {
            await Cli.Wrap(settings.PathToUnzip)
                .WithArguments(argBuilder => AppendArgument(args, argBuilder))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .WithWorkingDirectory(workingDirectory ?? "")
                .ExecuteAsync(cancellationToken ?? CancellationToken.None);
        }
        catch (Exception e)
        {
            if (!stdErr.ToString().Contains("appears to use backslashes as path separators"))
            {
                throw new UnzipUtilityException(
                    $"""
                    Error executing unzip
                    {string.Join(' ', args)}
                    StdOut: {stdOut}
                    StdErr: {stdErr}
                    Exception: {e}
                    """,
                    e
                );
            }
        }

        onProgress?.Invoke(1);

        return new StdOutStdErrOutput(stdOut.ToString(), stdErr.ToString());
    }

    private void AppendArgument(string[] args, ArgumentsBuilder argBuilder)
    {
        foreach (var arg in args)
        {
            argBuilder.Add(arg);
        }
    }
}

public class UnzipUtilityException(string message, Exception innerException)
    : Exception(message, innerException);
