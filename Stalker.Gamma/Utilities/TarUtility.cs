using System.Text;
using CliWrap;
using CliWrap.Builders;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.Utilities;

public class TarUtility(StalkerGammaSettings settings)
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

    private async Task<StdOutStdErrOutput> ExecuteTarCmdAsync(
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
            await Cli.Wrap(settings.PathToTar)
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
                throw new TarUtilityException(
                    $"""
                    Error executing tar
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

public class TarUtilityException : Exception
{
    public TarUtilityException(string message)
        : base(message) { }

    public TarUtilityException(string message, Exception innerException)
        : base(message, innerException) { }
}
