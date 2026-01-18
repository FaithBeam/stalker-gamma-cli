using System.Reactive.Linq;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Models;
using Stalker.Gamma.Services;

namespace stalker_gamma_cli.Commands;

[RegisterCommands]
public class FullInstallCmd(
    ILogger logger,
    CliSettings cliSettings,
    StalkerGammaSettings stalkerGammaSettings,
    GammaInstaller gammaInstaller,
    PowerShellCmdBuilder powerShellCmdBuilder,
    UtilitiesReady utilitiesReady
)
{
    /// <summary>
    /// This will install/update Anomaly and all GAMMA addons.
    /// </summary>
    /// <param name="skipGithubDownloads">Disable downloading github addons. They will still download if these archives do not exist.</param>
    /// <param name="skipExtractOnHashMatch">Skip extracting archives when their MD5 hashes match</param>
    /// <param name="addFoldersToWinDefenderExclusion">(Windows) Add the anomaly, gamma, and cache folders to the Windows Defender Exclusion list</param>
    /// <param name="enableLongPaths">(Windows) Enable long paths</param>
    /// <param name="verbose">More verbose logging</param>
    /// <param name="debug"></param>
    /// <param name="mo2Version">The version of Mod Organizer 2 to download</param>
    /// <param name="progressUpdateIntervalMs">How frequently to write progress to the console in milliseconds</param>
    /// <param name="gammaSetupRepoUrl">Escape hatch for git repo gamma_setup</param>
    /// <param name="stalkerGammaRepoUrl">Escape hatch for git repo Stalker_GAMMA</param>
    /// <param name="gammaLargeFilesRepoUrl">Escape hatch for git repo gamma_large_files_v2</param>
    /// <param name="teivazAnomalyGunslingerRepoUrl">Escape hatch for git repo teivaz_anomaly_gunslinger</param>
    /// <param name="stalkerAnomalyModdbUrl">Escape hatch for Stalker Anomaly</param>
    /// <param name="stalkerAnomalyArchiveMd5">The hash of the archive downloaded from --stalker-anomaly-moddb-url</param>
    public async Task FullInstall(
        // ReSharper disable once InvalidXmlDocComment
        CancellationToken cancellationToken,
        bool skipGithubDownloads = false,
        bool skipExtractOnHashMatch = false,
        bool addFoldersToWinDefenderExclusion = false,
        bool enableLongPaths = false,
        bool verbose = false,
        [Hidden] bool debug = false,
        [Hidden] string? mo2Version = null,
        [Hidden] long progressUpdateIntervalMs = 250,
        [Hidden] string gammaSetupRepoUrl = "https://github.com/Grokitach/gamma_setup",
        [Hidden] string stalkerGammaRepoUrl = "https://github.com/Grokitach/Stalker_GAMMA",
        [Hidden]
            string gammaLargeFilesRepoUrl = "https://github.com/Grokitach/gamma_large_files_v2",
        [Hidden]
            string teivazAnomalyGunslingerRepoUrl =
            "https://github.com/Grokitach/teivaz_anomaly_gunslinger",
        [Hidden] string stalkerAnomalyModdbUrl = "https://www.moddb.com/downloads/start/277404",
        [Hidden] string stalkerAnomalyArchiveMd5 = "d6bce51a4e6d98f9610ef0aa967ba964"
    )
    {
        if (!utilitiesReady.IsReady)
        {
            _logger.Error(
                """
                Dependency not found:
                {Message}
                """,
                utilitiesReady.NotReadyReason
            );
            Environment.Exit(1);
        }

        ValidateActiveProfile.Validate(_logger, cliSettings.ActiveProfile);
        var anomaly = cliSettings.ActiveProfile!.Anomaly;
        var gamma = cliSettings.ActiveProfile!.Gamma;
        var cache = cliSettings.ActiveProfile!.Cache;
        var mo2Profile = cliSettings.ActiveProfile!.Mo2Profile;
        var modpackMakerUrl = cliSettings.ActiveProfile!.ModPackMakerUrl;
        var modListUrl = cliSettings.ActiveProfile!.ModListUrl;
        stalkerGammaSettings.DownloadThreads = cliSettings.ActiveProfile!.DownloadThreads;
        stalkerGammaSettings.ModpackMakerList = modpackMakerUrl;
        stalkerGammaSettings.ModListUrl = modListUrl;
        stalkerGammaSettings.GammaSetupRepo = gammaSetupRepoUrl;
        stalkerGammaSettings.StalkerGammaRepo = stalkerGammaRepoUrl;
        stalkerGammaSettings.GammaLargeFilesRepo = gammaLargeFilesRepoUrl;
        stalkerGammaSettings.TeivazAnomalyGunslingerRepo = teivazAnomalyGunslingerRepoUrl;
        stalkerGammaSettings.StalkerAnomalyModdbUrl = stalkerAnomalyModdbUrl;
        stalkerGammaSettings.StalkerAnomalyArchiveMd5 = stalkerAnomalyArchiveMd5;

        if (OperatingSystem.IsWindows())
        {
            if (addFoldersToWinDefenderExclusion)
            {
                powerShellCmdBuilder.WithWindowsDefenderExclusions(
                    Path.GetFullPath(gamma),
                    Path.GetFullPath(anomaly),
                    Path.GetFullPath(cache)
                );
            }
            if (enableLongPaths)
            {
                powerShellCmdBuilder.WithEnableLongPaths();
            }
        }

        IDisposable? gammaDbgDispo = null;
        if (debug)
        {
            var gammaDbgObs = Observable
                .FromEventPattern<GammaProgress.GammaInstallDebugProgressEventArgs>(
                    handler => gammaInstaller.Progress.DebugProgressChanged += handler,
                    handler => gammaInstaller.Progress.DebugProgressChanged -= handler
                )
                .Select(x => x.EventArgs);
            gammaDbgDispo = gammaDbgObs.Subscribe(OnDebugProgressChanged);
        }

        var gammaProgressObservable = Observable
            .FromEventPattern<GammaProgress.GammaInstallProgressEventArgs>(
                handler => gammaInstaller.Progress.ProgressChanged += handler,
                handler => gammaInstaller.Progress.ProgressChanged -= handler
            )
            .Select(x => x.EventArgs);
        var gammaProgressDisposable = gammaProgressObservable
            .Sample(TimeSpan.FromMilliseconds(progressUpdateIntervalMs))
            .Subscribe(verbose ? OnProgressChangedVerbose : OnProgressChangedInformational);
        try
        {
            await gammaInstaller.FullInstallAsync(
                new GammaInstallerArgs
                {
                    Anomaly = anomaly,
                    Gamma = gamma,
                    Cache = cache,
                    Mo2Version = mo2Version,
                    CancellationToken = cancellationToken,
                    DownloadGithubArchives = !skipGithubDownloads,
                    SkipExtractOnHashMatch = skipExtractOnHashMatch,
                    Mo2Profile = mo2Profile,
                }
            );
            _logger.Information("Install finished");
        }
        finally
        {
            gammaDbgDispo?.Dispose();
            gammaProgressDisposable.Dispose();
        }
    }

    private void OnDebugProgressChanged(GammaProgress.GammaInstallDebugProgressEventArgs e)
    {
        File.AppendAllText("stalker-gamma-cli.log", $"{e.Text}{Environment.NewLine}");
    }

    private void OnProgressChangedInformational(GammaProgress.GammaInstallProgressEventArgs e) =>
        _logger.Information(
            Informational,
            e.Name[..Math.Min(e.Name.Length, 35)].PadRight(40),
            e.ProgressType.PadRight(10),
            $"{e.Progress:P2}".PadRight(8),
            $"[{e.Complete}/{e.Total}]"
        );

    private void OnProgressChangedVerbose(GammaProgress.GammaInstallProgressEventArgs e) =>
        _logger.Information(
            Verbose,
            e.Name[..Math.Min(e.Name.Length, 35)].PadRight(40),
            e.ProgressType.PadRight(10),
            $"{e.Progress:P2}".PadRight(8),
            $"[{e.Complete}/{e.Total}]",
            e.Url
        );

    private readonly ILogger _logger = logger;
    private const string Informational = "{AddonName} | {Operation} | {Percent} | {CompleteTotal}";
    private const string Verbose =
        "{AddonName} | {Operation} | {Percent} | {CompleteTotal} | {Url}";
}
