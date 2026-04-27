using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.Utilities;

public partial class CurlUtility(StalkerGammaSettings settings)
{
    public async Task<StdOutStdErrOutput> GetHeadersAsync(
        string url,
        CancellationToken cancellationToken = default
    ) => await ExecuteCurlCmdAsync(["-I", url], cancellationToken: cancellationToken);

    public async Task<StdOutStdErrOutput> DownloadFileAsync(
        string url,
        string pathToDownloads,
        string fileName,
        Action<double>? onProgress = null,
        string? workingDir = null,
        CancellationToken cancellationToken = default
    ) =>
        await ExecuteCurlCmdAsync(
            ["--progress-bar", "--clobber", "-o", Path.Join(pathToDownloads, fileName), url],
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
        List<string> args,
        Action<double>? onProgress = null,
        string? workingDir = null,
        CancellationToken cancellationToken = default
    )
    {
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();
        args.AddRange("--cacert", Path.Join(AppContext.BaseDirectory, "resources", "cacert.pem"));
        args.AddImpersonation();
        var exitCode = await RunProcessUtility.RunProcessAsync(
            PathToCurlImpersonate,
            args,
            onStdout: line =>
            {
                if (line.Contains("It appears you are a bot"))
                {
                    throw new ModDbBotDetectedException(
                        "ModDb temporarily blocked you. Try again in 1 hour."
                    );
                }
                stdOut.AppendLine(line);
            },
            onStderr: line =>
            {
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
                stdErr.AppendLine(line);
            },
            workingDir,
            ct: cancellationToken
        );
        if (exitCode != 0)
        {
            throw new CurlServiceException(
                $"""
                Error executing curl command
                {string.Join(' ', args)}
                StdOut: {stdOut}
                StdErr: {stdErr}
                Exit Code: {exitCode}
                """
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

public class CurlServiceException : Exception
{
    public CurlServiceException(string message)
        : base(message) { }

    public CurlServiceException(string message, Exception innerException)
        : base(message, innerException) { }
}

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

    internal static List<string> AddImpersonation(this List<string> argBuilder)
    {
        argBuilder.AddRange("--compressed", "--impersonate", Impersonation);
        return argBuilder;
    }
}
