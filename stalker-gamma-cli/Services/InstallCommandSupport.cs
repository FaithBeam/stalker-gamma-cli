using System.Reactive.Disposables;
using System.Reactive.Linq;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Models;
using Stalker.Gamma.Services;

namespace stalker_gamma_cli.Services;

/// <summary>
/// What the commands that download and install have in common: full-install, update apply and
/// anomaly install
/// </summary>
public class InstallCommandSupport(
    ILogger logger,
    CliSettings cliSettings,
    StalkerGammaSettings stalkerGammaSettings,
    UtilitiesReady utilitiesReady,
    PowerShellCmdBuilder powerShellCmdBuilder,
    ProgressLoggingService progressLoggingService
)
{
    /// <summary>
    /// Validates the dependencies and active profile
    /// </summary>
    /// <param name="usesDownloadThreads">Whether the command downloads addons in parallel with the profile's downloadThreads</param>
    /// <param name="downloadThreads">The --download-threads override, the profile's is only validated without it</param>
    /// <returns>The active profile</returns>
    /// <exception cref="CommandValidationException">The command can't run</exception>
    public CliProfile Validate(bool usesDownloadThreads = true, int? downloadThreads = null)
    {
        ValidateDependencies.Validate(utilitiesReady);
        var profile = ValidateActiveProfile.Validate(cliSettings.ActiveProfile);
        if (usesDownloadThreads && downloadThreads is null)
        {
            ValidateActiveProfile.ValidateDownloadThreads(profile);
        }
        return profile;
    }

    /// <summary>
    /// Configures the installer from the profile and command line options
    /// </summary>
    public void ApplySettings(
        CliProfile profile,
        int? downloadThreads = null,
        string? stalkerGammaServerUrl = null,
        bool useAnomalyMirror = false,
        bool addFoldersToWinDefenderExclusion = false,
        bool enableLongPaths = false
    )
    {
        stalkerGammaSettings.DownloadThreads = downloadThreads ?? profile.DownloadThreads;
        stalkerGammaSettings.ModpackMakerList = profile.ModPackMakerUrl;
        stalkerGammaSettings.ModListUrl = profile.ModListUrl;
        stalkerGammaSettings.GammaSetupRepo = profile.GammaSetupRepoUrl;
        stalkerGammaSettings.GammaSetupRepoBranch = profile.GammaSetupRepoBranch;
        stalkerGammaSettings.StalkerGammaRepo = profile.StalkerGammaRepoUrl;
        stalkerGammaSettings.StalkerGammaRepoBranch = profile.StalkerGammaRepoBranch;
        stalkerGammaSettings.GammaLargeFilesRepo = profile.GammaLargeFilesRepoUrl;
        stalkerGammaSettings.GammaLargeFilesRepoBranch = profile.GammaLargeFilesRepoBranch;
        stalkerGammaSettings.TeivazAnomalyGunslingerRepo = profile.TeivazAnomalyGunslingerRepoUrl;
        stalkerGammaSettings.TeivazAnomalyGunslingerRepoBranch =
            profile.TeivazAnomalyGunslingerRepoBranch;
        // ModDB requests only go through the server when this is set
        stalkerGammaSettings.StalkerGammaServerUrl = stalkerGammaServerUrl;
        stalkerGammaSettings.UseAnomalyMirror = useAnomalyMirror;

        if (OperatingSystem.IsWindows())
        {
            if (addFoldersToWinDefenderExclusion)
            {
                powerShellCmdBuilder.WithWindowsDefenderExclusions(
                    Path.GetFullPath(profile.Gamma),
                    Path.GetFullPath(profile.Anomaly),
                    Path.GetFullPath(profile.Cache)
                );
            }
            if (enableLongPaths)
            {
                powerShellCmdBuilder.WithEnableLongPaths();
            }
        }
    }

    /// <summary>
    /// Logs warnings, writes progress to the console, and collects the files touched for the log
    /// file until disposed
    /// </summary>
    public IDisposable ReportProgress(
        IGammaProgress progress,
        bool verbose,
        bool json,
        long progressUpdateIntervalMs
    )
    {
        progress.Warning += OnWarning;
        var progressChanged = Observable
            .FromEventPattern<GammaProgress.GammaInstallProgressEventArgs>(
                handler => progress.ProgressChanged += handler,
                handler => progress.ProgressChanged -= handler
            )
            .Select(x => x.EventArgs);
        return new CompositeDisposable(
            progressChanged.Subscribe(progressLoggingService.OnProgressChangedWriteToFile),
            progressChanged.SubscribeSampledPerOperationOnDedicatedThread(
                TimeSpan.FromMilliseconds(progressUpdateIntervalMs),
                GetProgressWriter(verbose, json)
            ),
            Disposable.Create(() => progress.Warning -= OnWarning)
        );
    }

    public Action<GammaProgress.GammaInstallProgressEventArgs> GetProgressWriter(
        bool verbose,
        bool json
    ) =>
        json ? JsonOutputUtility.WriteProgress
        : verbose ? WriteProgressVerbose
        : WriteProgressInformational;

    /// <summary>
    /// Runs the command, logs its failure, and writes the json result message with --json
    /// </summary>
    /// <param name="command">The command name in the result message, e.g. full-install</param>
    /// <param name="failureMessage">Logged when it fails, e.g. Install failed!</param>
    /// <param name="onFailure">Runs after a failure is logged, e.g. to save what to resume</param>
    /// <param name="progressReporting">
    /// From <see cref="ReportProgress"/>. It's disposed before the result message so no progress
    /// is written after it.
    /// </param>
    public async Task<int> RunAsync(
        string command,
        string failureMessage,
        bool json,
        Func<Task> action,
        Action? onFailure = null,
        IDisposable? progressReporting = null
    )
    {
        string? error = null;
        Exception? failure = null;
        try
        {
            await action();
        }
        catch (CommandValidationException e)
        {
            error = JsonOutputUtility.LogError(logger, e.Message);
        }
        catch (Exception e)
        {
            failure = e;
        }
        finally
        {
            progressReporting?.Dispose();
            // before the failure, so the log file lists what was being worked on above the error
            progressLoggingService.WriteToLogFile();
        }

        if (failure is not null)
        {
            logger.Error(
                failure,
                "{FailureMessage} {ExceptionMessage}",
                failureMessage,
                failure.Message
            );
            onFailure?.Invoke();
            error = failure.Message;
        }

        var statusCode = error is null ? 0 : 1;
        if (json)
        {
            JsonOutputUtility.WriteResult(command, statusCode, error);
        }
        return statusCode;
    }

    private void OnWarning(object? sender, GammaProgress.GammaInstallWarningEventArgs e) =>
        logger.Warning(e.Exception, "{Message}", e.Message);

    private void WriteProgressInformational(GammaProgress.GammaInstallProgressEventArgs e) =>
        logger.Information(
            Informational,
            DateTimeOffset.Now.ToString("HH:mm:ss"),
            e.Name[..Math.Min(e.Name.Length, 35)].PadRight(40),
            e.ProgressType.GetHumanReadableString().PadRight(10),
            e.GetHumanReadableProgress().PadRight(10),
            $"[{e.Complete}/{e.Total}]"
        );

    private void WriteProgressVerbose(GammaProgress.GammaInstallProgressEventArgs e) =>
        logger.Information(
            Verbose,
            e.Name[..Math.Min(e.Name.Length, 35)].PadRight(40),
            e.ProgressType.GetHumanReadableString().PadRight(10),
            e.GetHumanReadableProgress().PadRight(10),
            $"[{e.Complete}/{e.Total}]",
            e.Url
        );

    private const string Informational =
        "\e[97m[{DateTime}]\e[0m "
        + "\e[96m{AddonName}\e[0m "
        + "\e[97m|\e[0m "
        + "\e[96m{Operation}\e[0m "
        + "\e[97m|\e[0m "
        + "\e[96m{Percent}\e[0m "
        + "\e[97m|\e[0m "
        + "\e[96m{CompleteTotal}\e[0m";

    private const string Verbose =
        "{AddonName} | {Operation} | {Percent} | {CompleteTotal} | {Url}";
}
