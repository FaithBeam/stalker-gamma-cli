using System.ComponentModel.DataAnnotations;
using System.Reactive.Linq;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Services;
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
    IGammaInstaller gammaInstaller,
    OfflineGammaInstaller offlineGammaInstaller,
    PowerShellCmdBuilder powerShellCmdBuilder,
    UtilitiesReady utilitiesReady,
    ProgressLoggingService progressLoggingService
)
{
    /// <summary>
    /// This will install/update Anomaly and all GAMMA addons. This will take ~150GB.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <param name="skipGithubDownloads">Disable downloading github addons. They will still download if these archives do not exist.</param>
    /// <param name="skipExtractOnHashMatch">Skip extracting archives when their MD5 hashes match</param>
    /// <param name="addFoldersToWinDefenderExclusion">(Windows) Add the anomaly, gamma, and cache folders to the Windows Defender Exclusion list</param>
    /// <param name="enableLongPaths">(Windows) Enable long paths</param>
    /// <param name="verbose">More verbose logging</param>
    /// <param name="minimal">Delete cache files after extracting. Could be useful for space constrained devices but increases the chance of installation failure and will make updates much slower. This will take about ~100GB.</param>
    /// <param name="offline">Perform an offline install from cache. This will not download anything if you combine this with --mod-pack-maker-path and --mod-list-path</param>
    /// <param name="preserveUserSettings">Preserve user settings (user.ltx)</param>
    /// <param name="preserveMcmSettings">Preserve MCM settings</param>
    /// <param name="modPackMakerPath">Path to modpack_maker_list.txt. Offline install.</param>
    /// <param name="modListPath">Path to modlist.txt. Offline install.</param>
    /// <param name="downloadThreads">Override downloadThreads defined in your profile</param>
    /// <param name="debug"></param>
    /// <param name="progressUpdateIntervalMs">How frequently to write progress to the console in milliseconds</param>
    public async Task FullInstall(
        CancellationToken cancellationToken,
        bool skipGithubDownloads = false,
        bool skipExtractOnHashMatch = false,
        bool addFoldersToWinDefenderExclusion = false,
        bool enableLongPaths = false,
        bool verbose = false,
        bool minimal = false,
        bool offline = false,
        bool preserveUserSettings = false,
        bool preserveMcmSettings = false,
        string? modPackMakerPath = null,
        string? modListPath = null,
        [Range(1, 20)] int? downloadThreads = null,
        [Hidden] bool debug = false,
        [Hidden] long progressUpdateIntervalMs = 250
    )
    {
        LogAndExitOnDependencyError.Check(_utilitiesReady, _logger);

        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);

        ValidateOfflineRequirements(offline, modPackMakerPath, modListPath);

        InitializeSettings(
            downloadThreads,
            _cliSettings.ActiveProfile!.GammaSetupRepoUrl,
            _cliSettings.ActiveProfile.GammaSetupRepoBranch,
            _cliSettings.ActiveProfile.StalkerGammaRepoUrl,
            _cliSettings.ActiveProfile.StalkerGammaRepoBranch,
            _cliSettings.ActiveProfile.GammaLargeFilesRepoUrl,
            _cliSettings.ActiveProfile.GammaLargeFilesRepoBranch,
            _cliSettings.ActiveProfile.TeivazAnomalyGunslingerRepoUrl,
            _cliSettings.ActiveProfile.TeivazAnomalyGunslingerRepoBranch,
            out var anomaly,
            out var gamma,
            out var cache,
            out var mo2Profile
        );

        ConfigurePowerShellSettings(
            addFoldersToWinDefenderExclusion,
            enableLongPaths,
            gamma,
            anomaly,
            cache
        );

        var installer = offline ? _offlineGammaInstaller : _gammaInstaller;

        SetUpLogging(
            installer,
            verbose,
            debug,
            progressUpdateIntervalMs,
            out var gammaWriteFileDisposable,
            out var gammaProgressDisposable,
            out var gammaDbgDispo
        );

        try
        {
            var args = GammaInstallerArgs
                .Create(anomaly, gamma, cache)
                .WithCancellationToken(cancellationToken)
                .WithDownloadGithubArchives(!skipGithubDownloads)
                .WithSkipExtractOnHashMatch(skipExtractOnHashMatch)
                .WithMo2Profile(mo2Profile)
                .WithMinimal(minimal)
                .WithModPackMakerPath(modPackMakerPath)
                .WithModListPath(modListPath)
                .WithPreserveUserLtx(preserveUserSettings)
                .WithPreserveMcmSettings(preserveMcmSettings)
                .Build();
            args.GroupedAddonRecords = await installer.BuildGroupedAddonRecordsAsync(args);
            args.AnomalyRecord = installer.BuildAnomalyRecord(args);
            installer.BuildSpecialRepoRecords(args);
            await installer.InstallAsync(args);
            _logger.Information("Install finished");
        }
        catch (Exception e)
        {
            _progressLoggingService.WriteToLogFile();
            _logger.Error(e, "Install failed! {ExceptionMessage}", e.Message);
        }
        finally
        {
            _progressLoggingService.WriteToLogFile();
            gammaDbgDispo?.Dispose();
            gammaProgressDisposable.Dispose();
            gammaWriteFileDisposable.Dispose();
        }
    }

    private static void ValidateOfflineRequirements(
        bool offline,
        string? modPackMakerPath,
        string? modListPath
    )
    {
        if (
            offline
            && (
                string.IsNullOrWhiteSpace(modPackMakerPath)
                || string.IsNullOrWhiteSpace(modListPath)
            )
        )
        {
            throw new ArgumentException(
                "--offline requires --mod-pack-maker-path and --mod-list-path"
            );
        }
    }

    private void InitializeSettings(
        int? downloadThreads,
        string gammaSetupRepoUrl,
        string gammaSetupRepoBranch,
        string stalkerGammaRepoUrl,
        string stalkerGammaRepoBranch,
        string gammaLargeFilesRepoUrl,
        string gammaLargeFilesRepoBranch,
        string teivazAnomalyGunslingerRepoUrl,
        string teivazAnomalyGunslingerRepoBranch,
        out string anomaly,
        out string gamma,
        out string cache,
        out string mo2Profile
    )
    {
        anomaly = _cliSettings.ActiveProfile!.Anomaly;
        gamma = _cliSettings.ActiveProfile!.Gamma;
        cache = _cliSettings.ActiveProfile!.Cache;
        mo2Profile = _cliSettings.ActiveProfile!.Mo2Profile;
        var modpackMakerUrl = _cliSettings.ActiveProfile!.ModPackMakerUrl;
        var modListUrl = _cliSettings.ActiveProfile!.ModListUrl;
        _stalkerGammaSettings.DownloadThreads =
            downloadThreads ?? _cliSettings.ActiveProfile!.DownloadThreads;
        _stalkerGammaSettings.ModpackMakerList = modpackMakerUrl;
        _stalkerGammaSettings.ModListUrl = modListUrl;
        _stalkerGammaSettings.GammaSetupRepo = gammaSetupRepoUrl;
        _stalkerGammaSettings.GammaSetupRepoBranch = gammaSetupRepoBranch;
        _stalkerGammaSettings.StalkerGammaRepo = stalkerGammaRepoUrl;
        _stalkerGammaSettings.StalkerGammaRepoBranch = stalkerGammaRepoBranch;
        _stalkerGammaSettings.GammaLargeFilesRepo = gammaLargeFilesRepoUrl;
        _stalkerGammaSettings.GammaLargeFilesRepoBranch = gammaLargeFilesRepoBranch;
        _stalkerGammaSettings.TeivazAnomalyGunslingerRepo = teivazAnomalyGunslingerRepoUrl;
        _stalkerGammaSettings.TeivazAnomalyGunslingerRepoBranch = teivazAnomalyGunslingerRepoBranch;
    }

    private void ConfigurePowerShellSettings(
        bool addFoldersToWinDefenderExclusion,
        bool enableLongPaths,
        string gamma,
        string anomaly,
        string cache
    )
    {
        if (OperatingSystem.IsWindows())
        {
            if (addFoldersToWinDefenderExclusion)
            {
                _powerShellCmdBuilder.WithWindowsDefenderExclusions(
                    Path.GetFullPath(gamma),
                    Path.GetFullPath(anomaly),
                    Path.GetFullPath(cache)
                );
            }
            if (enableLongPaths)
            {
                _powerShellCmdBuilder.WithEnableLongPaths();
            }
        }
    }

    private void SetUpLogging(
        IGammaInstaller installer,
        bool verbose,
        bool debug,
        long progressUpdateIntervalMs,
        out IDisposable gammaWriteFileDisposable,
        out IDisposable gammaProgressDisposable,
        out IDisposable? gammaDbgDisposable
    )
    {
        gammaDbgDisposable = null;
        if (debug)
        {
            var gammaDbgObs = Observable
                .FromEventPattern<GammaProgress.GammaInstallDebugProgressEventArgs>(
                    handler => installer.Progress.DebugProgressChanged += handler,
                    handler => installer.Progress.DebugProgressChanged -= handler
                )
                .Select(x => x.EventArgs);
            gammaDbgDisposable = gammaDbgObs.Subscribe(OnDebugProgressChanged);
        }

        var gammaWriteFileObs = Observable
            .FromEventPattern<GammaProgress.GammaInstallProgressEventArgs>(
                handler => installer.Progress.ProgressChanged += handler,
                handler => installer.Progress.ProgressChanged -= handler
            )
            .Select(x => x.EventArgs);
        gammaWriteFileDisposable = gammaWriteFileObs.Subscribe(
            _progressLoggingService.OnProgressChangedWriteToFile
        );

        var gammaProgressObservable = Observable
            .FromEventPattern<GammaProgress.GammaInstallProgressEventArgs>(
                handler => installer.Progress.ProgressChanged += handler,
                handler => installer.Progress.ProgressChanged -= handler
            )
            .Select(x => x.EventArgs);
        gammaProgressDisposable = gammaProgressObservable
            .Sample(TimeSpan.FromMilliseconds(progressUpdateIntervalMs))
            .Subscribe(verbose ? OnProgressChangedVerbose : OnProgressChangedInformational);
    }

    private void OnDebugProgressChanged(GammaProgress.GammaInstallDebugProgressEventArgs e) =>
        File.AppendAllText("stalker-gamma-cli.log", $"{e.Text}{Environment.NewLine}");

    private void OnProgressChangedInformational(GammaProgress.GammaInstallProgressEventArgs e) =>
        _logger.Information(
            Informational,
            DateTimeOffset.Now.ToString("HH:mm:ss"),
            e.Name[..Math.Min(e.Name.Length, 35)].PadRight(40),
            e.ProgressType.GetHumanReadableString().PadRight(10),
            $"{e.Progress:P2}".PadRight(8),
            $"[{e.Complete}/{e.Total}]"
        );

    private void OnProgressChangedVerbose(GammaProgress.GammaInstallProgressEventArgs e) =>
        _logger.Information(
            Verbose,
            e.Name[..Math.Min(e.Name.Length, 35)].PadRight(40),
            e.ProgressType.GetHumanReadableString().PadRight(10),
            $"{e.Progress:P2}".PadRight(8),
            $"[{e.Complete}/{e.Total}]",
            e.Url
        );

    private readonly ILogger _logger = logger;
    private readonly UtilitiesReady _utilitiesReady = utilitiesReady;
    private readonly CliSettings _cliSettings = cliSettings;
    private readonly StalkerGammaSettings _stalkerGammaSettings = stalkerGammaSettings;
    private readonly IGammaInstaller _gammaInstaller = gammaInstaller;
    private readonly OfflineGammaInstaller _offlineGammaInstaller = offlineGammaInstaller;
    private readonly PowerShellCmdBuilder _powerShellCmdBuilder = powerShellCmdBuilder;
    private readonly ProgressLoggingService _progressLoggingService = progressLoggingService;

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
