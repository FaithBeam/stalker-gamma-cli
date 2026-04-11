using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Builders;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.Utilities;

public partial class CurlUtility(StalkerGammaSettings settings)
{
    public async Task<StdOutStdErrOutput> DownloadFileAsync(
        string url,
        string pathToDownloads,
        string fileName,
        Action<double>? onProgress = null,
        string? workingDir = null,
        CancellationToken cancellationToken = default
    ) =>
        await ExecuteCurlCmdAsync(
            ["--progress-bar", "--clobber", "-Lo", Path.Join(pathToDownloads, fileName), url],
            onProgress: onProgress,
            workingDir: workingDir,
            cancellationToken: cancellationToken
        );

    public async Task<string> GetStringAsync(
        string url,
        CancellationToken cancellationToken = default
    ) =>
        (
            await ExecuteCurlCmdAsync(
                ["--no-progress-meter", url],
                cancellationToken: cancellationToken
            )
        ).StdOut;

    private async Task<StdOutStdErrOutput> ExecuteCurlCmdAsync(
        string[] args,
        Action<double>? onProgress = null,
        Action<string>? txtProgress = null,
        string? workingDir = null,
        CancellationToken cancellationToken = default
    )
    {
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        try
        {
            await Cli.Wrap(PathToCurlImpersonate)
                .WithArguments(argBuilder =>
                    argBuilder
                        .Add(args)
                        .Add("--cacert")
                        .Add(Path.Join(AppContext.BaseDirectory, "resources", "cacert.pem"))
                        .AddImpersonation()
                )
                .WithStandardOutputPipe(
                    PipeTarget.Merge(
                        PipeTarget.ToStringBuilder(stdOut),
                        PipeTarget.ToDelegate(line =>
                        {
                            if (line.Contains("It appears you are a bot"))
                            {
                                throw new ModDbBotDetectedException(
                                    "ModDb temporarily blocked you. Try again in 1 hour."
                                );
                            }
                        })
                    )
                )
                .WithStandardErrorPipe(
                    PipeTarget.Merge(
                        PipeTarget.ToStringBuilder(stdErr),
                        PipeTarget.ToDelegate(line =>
                        {
                            txtProgress?.Invoke(line);
                            var match = ProgressRx().Match(line);
                            if (
                                onProgress is not null
                                && match.Success
                                && double.TryParse(
                                    ProgressRx().Match(line).Groups[1].Value,
                                    provider: CultureInfo.InvariantCulture,
                                    out var parsed
                                )
                            )
                            {
                                onProgress(parsed / 100);
                            }
                        })
                    )
                )
                .WithWorkingDirectory(workingDir ?? "")
                .ExecuteAsync(cancellationToken);
        }
        catch (Exception e) when (e is not ModDbBotDetectedException)
        {
            throw new CurlServiceException(
                $"""
                Error executing curl command
                {string.Join(' ', args)}
                StdOut: {stdOut}
                StdErr: {stdErr}
                Exception: {e}
                """,
                e
            );
        }

        return new StdOutStdErrOutput(stdOut.ToString(), stdErr.ToString());
    }

    private string PathToCurlImpersonate => settings.PathToCurl;

    /// <summary>
    /// Whether curl service found curl-impersonate-win.exe and can execute.
    /// </summary>
    public bool Ready =>
        File.Exists(PathToCurlImpersonate)
        || EnvChecker.IsInPath(OperatingSystem.IsWindows() ? "curl.exe" : "curl-impersonate");

    [GeneratedRegex(@"(\d+([\.,]\d+)?)\s*%", RegexOptions.Compiled)]
    private partial Regex ProgressRx();
}

public class ModDbBotDetectedException(string msg) : Exception(msg);

public class CurlServiceException(string message, Exception innerException)
    : Exception(message, innerException);

internal static class ArgumentsBuilderExtensions
{
    private static readonly HashSet<string> Impersonations =
    [
        "chrome145",
        "chrome142",
        "firefox147",
        "safari2601",
    ];

    // choose an impersonation randomly to be used for this session's requests to moddb
    private static readonly string Impersonation = Impersonations.ElementAt(
        Random.Shared.Next(Impersonations.Count)
    );

    internal static ArgumentsBuilder AddImpersonation(this ArgumentsBuilder argBuilder) =>
        argBuilder.Add("--compressed").Add("--impersonate").Add(Impersonation);
}
