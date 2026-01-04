using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Builders;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.Utilities;

public partial class SevenZipUtility(StalkerGammaSettings settings)
{
    public async Task<StdOutStdErrOutput> ExtractAsync(
        string archivePath,
        string destinationFolder,
        Action<double>? onProgress = null,
        Action<string>? txtProgress = null,
        string? workingDirectory = null,
        CancellationToken? cancellationToken = null
    )
    {
        if (!Directory.Exists(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
        }

        return await ExecuteSevenZipCmdAsync(
            ["x", "-y", "-bsp1", archivePath, $"-o{destinationFolder}"],
            onProgress,
            txtProgress,
            workingDirectory: workingDirectory,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="paths">The paths to add to the archive</param>
    /// <param name="destination">The output path</param>
    /// <param name="compressor"></param>
    /// <param name="compressionLevel"></param>
    /// <param name="exclusions">Folders/items to exclude</param>
    /// <param name="workDirectory"></param>
    /// <param name="txtProgress"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<StdOutStdErrOutput> Archive(
        string[] paths,
        string destination,
        string compressor,
        string compressionLevel,
        string[]? exclusions = null,
        string? workDirectory = null,
        Action<string>? txtProgress = null,
        CancellationToken? cancellationToken = null
    )
    {
        var args = new[]
        {
            "a",
            "-bsp",
            $"{destination}",
            $"{string.Join(" ", paths.Select(x => $"{x}"))}",
            $"-m0={(compressor == "zstd" ? "bcj" : compressor)}",
            $"{(compressor == "zstd" ? "-m1=zstd " : "")}",
            $"-mx{compressionLevel}",
            $"{(exclusions?.Length == 0 ? "" : string.Join(" ", exclusions!.Select(x => $"-xr!{x}")))}",
        };

        return await ExecuteSevenZipCmdAsync(
            args,
            workingDirectory: workDirectory,
            txtProgress: txtProgress,
            cancellationToken: cancellationToken
        );
    }

    private async Task<StdOutStdErrOutput> ExecuteSevenZipCmdAsync(
        string[] args,
        Action<double>? onProgress = null,
        Action<string>? txtProgress = null,
        string? workingDirectory = null,
        CancellationToken? cancellationToken = null
    )
    {
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        try
        {
            await Cli.Wrap(PathTo7Z)
                .WithArguments(argBuilder => AppendArgument(args, argBuilder))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
                .WithStandardOutputPipe(
                    PipeTarget.Merge(
                        PipeTarget.ToStringBuilder(stdOut),
                        PipeTarget.Create(
                            async (origin, ct) =>
                            {
                                // Buffer must be bigger than the process's stdout/stderr buffer
                                const int bufferSize = 1024;

                                using var reader = new StreamReader(
                                    origin,
                                    Console.OutputEncoding,
                                    false,
                                    bufferSize,
                                    true
                                );
                                var buffer = new char[bufferSize];

                                int charsRead;
                                while (
                                    (charsRead = await reader.ReadAsync(buffer, 0, buffer.Length))
                                    > 0
                                )
                                {
                                    ct.ThrowIfCancellationRequested();

                                    var data = new string(buffer, 0, charsRead);

                                    // Do something with data here
                                    if (onProgress is null)
                                    {
                                        return;
                                    }
                                    var matches = ProgressRx().Matches(data).ToList();
                                    if (matches.Count > 0)
                                    {
                                        foreach (var m in matches)
                                        {
                                            onProgress(double.Parse(m.Groups[1].Value) / 100);
                                        }
                                    }
                                }
                            }
                        )
                    )
                )
                .WithWorkingDirectory(workingDirectory ?? "")
                .ExecuteAsync(cancellationToken ?? CancellationToken.None);
        }
        catch (Exception e)
        {
            throw new SevenZipUtilityException(
                $"""
                Error executing {PathTo7Z}
                {string.Join(' ', args)}
                StdOut: {stdOut}
                StdErr: {stdErr}
                Exception: {e}
                """,
                e
            );
        }

        // make sure to report 100% progress because 7zip many times stops reporting at 99%
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

    private string PathTo7Z => settings.PathTo7Z;

    [GeneratedRegex(@"(\d+(\.\d+)?)\s*%", RegexOptions.Compiled)]
    private partial Regex ProgressRx();
}

public class SevenZipUtilityException(string msg, Exception innerException)
    : Exception(msg, innerException);
