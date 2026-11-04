using System.ComponentModel.DataAnnotations;
using System.Reactive.Linq;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Services;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.GammaInstallerServices.GammaInstaller;
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
    /// <param name="skipGithubDownloads">Disable downloading GitHub addons. They will still download if these archives do not exist.</param>
    /// <param name="skipExtractOnHashMatch">Skip extracting archives when their MD5 hashes match</param>
    /// <param name="addFoldersToWinDefenderExclusion">(Windows) Add the anomaly, gamma, and cache folders to the Windows Defender Exclusion list</param>
    /// <param name="enableLongPaths">(Windows) Enable long paths</param>
    /// <param name="verbose">More verbose logging</param>
    /// <param name="minimal">Delete cache files after extracting. Could be useful for space-constrained devices but increases the chance of installation failure and will make updates much slower. This will take about ~100GB.</param>
    /// <param name="preserveUserSettings">Preserve user settings (user.ltx)</param>
    /// <param name="preserveMcmSettings">Preserve MCM settings</param>
    /// <param name="stalkerGammaServerUrl">URL to the experimental stalker gamma cli server when connecting to moddb for addons</param>
    /// <param name="useAnomalyMirror">Download Anomaly from the faith-beam.com mirror first, which extracts faster, and fall back to ModDB if it fails</param>
    /// <param name="modPackMakerPath">Path to a modpack_maker_list.txt to install instead of downloading the latest one. Overrides the one in --offline-archive-path.</param>
    /// <param name="modListPath">Path to a modlist.txt to install instead of downloading the latest one. Overrides the one in --offline-archive-path.</param>
    /// <param name="offlineArchivePath">Path to an offline cache tar archive. It's extracted to your cache folder, then an offline install is performed with the modpack_maker_list.txt and modlist.txt inside it. Nothing is downloaded.</param>
    /// <param name="downloadThreads">Override downloadThreads defined in your profile</param>
    /// <param name="resume">Resume a previously failed full-install from resume.txt in your gamma folder</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    /// <param name="debug"></param>
    /// <param name="progressUpdateIntervalMs">How frequently to write progress to the console in milliseconds</param>
    public async Task<int> FullInstall(
        CancellationToken cancellationToken,
        bool skipGithubDownloads = false,
        bool skipExtractOnHashMatch = false,
        bool addFoldersToWinDefenderExclusion = false,
        bool enableLongPaths = false,
        bool verbose = false,
        bool minimal = false,
        bool preserveUserSettings = false,
        bool preserveMcmSettings = false,
        string? stalkerGammaServerUrl = null,
        bool useAnomalyMirror = false,
        string? modPackMakerPath = null,
        string? modListPath = null,
        string? offlineArchivePath = null,
        [Range(1, 20)] int? downloadThreads = null,
        bool resume = false,
        bool json = false,
        [Hidden] bool debug = false,
        [Hidden] long progressUpdateIntervalMs = 250
    )
    {
        var statusCode = 0;
        string? error = null;
        LogAndExitOnDependencyError.Check(_utilitiesReady, _logger);

        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);

        var offline = !string.IsNullOrWhiteSpace(offlineArchivePath);
        ValidateOfflineArchivePath(offlineArchivePath);

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
            stalkerGammaServerUrl,
            out var anomaly,
            out var gamma,
            out var cache,
            out var mo2Profile
        );
        _stalkerGammaSettings.UseAnomalyMirror = useAnomalyMirror;

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
            json,
            debug,
            progressUpdateIntervalMs,
            out var gammaWriteFileDisposable,
            out var gammaProgressDisposable,
            out var gammaDbgDispo
        );

        ResumeState? resumeState = null;
        DirectoryInfo? offlineListsDir = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(offlineArchivePath))
            {
                offlineListsDir = Directory.CreateTempSubdirectory("stalker-gamma-offline-");
                (modPackMakerPath, modListPath) = await ExtractOfflineArchiveAsync(
                    offlineArchivePath,
                    cache,
                    offlineListsDir.FullName,
                    modPackMakerPath,
                    modListPath,
                    GetProgressWriter(verbose, json),
                    progressUpdateIntervalMs,
                    cancellationToken
                );
            }

            resumeState = await ResumeUtility.LoadAsync(
                _logger,
                resume,
                gamma,
                ResumeCommand,
                cancellationToken
            );
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
                .WithUseStalkerGammaServerUrl(stalkerGammaServerUrl)
                .WithResumeState(resumeState)
                .Build();
            args.GroupedAddonRecords = await installer.BuildGroupedAddonRecordsAsync(args);
            args.AnomalyRecord = installer.BuildAnomalyRecord(args);
            installer.BuildSpecialRepoRecords(args);
            await installer.InstallAsync(args);
            ResumeUtility.Delete(gamma);
            _logger.Information("Install finished");
        }
        catch (Exception e)
        {
            _progressLoggingService.WriteToLogFile();
            _logger.Error(e, "Install failed! {ExceptionMessage}", e.Message);
            if (resumeState is not null)
            {
                ResumeUtility.Write(_logger, resumeState, gamma);
            }
            statusCode = 1;
            error = e.Message;
        }
        finally
        {
            DeleteOfflineListsDir(offlineListsDir);
            _progressLoggingService.WriteToLogFile();
            gammaDbgDispo?.Dispose();
            gammaProgressDisposable.Dispose();
            gammaWriteFileDisposable.Dispose();
        }

        if (json)
        {
            JsonOutputUtility.WriteResult(ResumeCommand, statusCode, error);
        }

        return statusCode;
    }

    private static void ValidateOfflineArchivePath(string? offlineArchivePath)
    {
        if (!string.IsNullOrWhiteSpace(offlineArchivePath) && !File.Exists(offlineArchivePath))
        {
            throw new ArgumentException(
                $"--offline-archive-path {offlineArchivePath} does not exist"
            );
        }
    }

    /// <summary>
    /// Extract the offline archive's cache into the cache folder and its lists into listsDir.
    /// --mod-pack-maker-path and --mod-list-path take priority over the lists in the archive.
    /// </summary>
    /// <returns>Paths to modpack_maker_list.txt and modlist.txt to install with</returns>
    private async Task<(string ModPackMakerPath, string ModListPath)> ExtractOfflineArchiveAsync(
        string offlineArchivePath,
        string cache,
        string listsDir,
        string? modPackMakerPath,
        string? modListPath,
        Action<GammaProgress.GammaInstallProgressEventArgs> writeProgress,
        long progressUpdateIntervalMs,
        CancellationToken cancellationToken
    )
    {
        var archiveFullPath = Path.GetFullPath(offlineArchivePath);
        var cacheFullPath = Path.GetFullPath(cache);
        _logger.Information(
            "Extracting offline archive {OfflineArchive} to {Cache}",
            archiveFullPath,
            cacheFullPath
        );
        var archiveName = Path.GetFileName(archiveFullPath);
        var progress = ProgressThrottleUtility.Throttle<double>(
            pct =>
                writeProgress(
                    new GammaProgress.GammaInstallProgressEventArgs
                    {
                        Name = archiveName,
                        ProgressType = GammaProgressType.Extract,
                        Progress = pct,
                        Url = "",
                        ArchiveName = archiveName,
                        DownloadPath = archiveFullPath,
                        ExtractPath = cacheFullPath,
                        Complete = pct >= 1 ? 1 : 0,
                        Total = 1,
                    }
                ),
            TimeSpan.FromMilliseconds(progressUpdateIntervalMs)
        );

        var result = await OfflineArchiveUtility.ExtractAsync(
            archiveFullPath,
            cacheFullPath,
            listsDir,
            progress,
            cancellationToken
        );
        _logger.Information(
            "Extracted {Extracted} files from the offline archive, skipped {Skipped} files that were already extracted",
            result.Extracted,
            result.Skipped
        );

        modPackMakerPath = string.IsNullOrWhiteSpace(modPackMakerPath)
            ? result.ModPackMakerListPath
                ?? throw new OfflineArchiveException(
                    $"{archiveFullPath} does not contain {OfflineArchiveUtility.ModPackMakerListFileName}. Pass it with --mod-pack-maker-path"
                )
            : modPackMakerPath;
        modListPath = string.IsNullOrWhiteSpace(modListPath)
            ? result.ModListPath
                ?? throw new OfflineArchiveException(
                    $"{archiveFullPath} does not contain {OfflineArchiveUtility.ModListFileName}. Pass it with --mod-list-path"
                )
            : modListPath;
        return (modPackMakerPath, modListPath);
    }

    private void DeleteOfflineListsDir(DirectoryInfo? dir)
    {
        if (dir is null)
        {
            return;
        }

        try
        {
            dir.Delete(true);
        }
        catch (Exception e)
        {
            _logger.Warning(e, "Unable to delete {OfflineListsDir}", dir.FullName);
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
        string? stalkerGammaServerUrl,
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
        _stalkerGammaSettings.StalkerGammaServerUrl = stalkerGammaServerUrl;
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
        bool json,
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
            .SubscribeSampledOnDedicatedThread(
                TimeSpan.FromMilliseconds(progressUpdateIntervalMs),
                GetProgressWriter(verbose, json)
            );
    }

    private Action<GammaProgress.GammaInstallProgressEventArgs> GetProgressWriter(
        bool verbose,
        bool json
    ) =>
        json ? JsonOutputUtility.WriteProgress
        : verbose ? OnProgressChangedVerbose
        : OnProgressChangedInformational;

    private void OnDebugProgressChanged(GammaProgress.GammaInstallDebugProgressEventArgs e) =>
        File.AppendAllText("stalker-gamma-cli.log", $"{e.Text}{Environment.NewLine}");

    private void OnProgressChangedInformational(GammaProgress.GammaInstallProgressEventArgs e) =>
        _logger.Information(
            Informational,
            DateTimeOffset.Now.ToString("HH:mm:ss"),
            e.Name[..Math.Min(e.Name.Length, 35)].PadRight(40),
            e.ProgressType.GetHumanReadableString().PadRight(10),
            e.GetHumanReadableProgress().PadRight(10),
            $"[{e.Complete}/{e.Total}]"
        );

    private void OnProgressChangedVerbose(GammaProgress.GammaInstallProgressEventArgs e) =>
        _logger.Information(
            Verbose,
            e.Name[..Math.Min(e.Name.Length, 35)].PadRight(40),
            e.ProgressType.GetHumanReadableString().PadRight(10),
            e.GetHumanReadableProgress().PadRight(10),
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

    private const string ResumeCommand = "full-install";

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
