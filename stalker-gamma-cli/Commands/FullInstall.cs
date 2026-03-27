using System.ComponentModel.DataAnnotations;
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
    /// This will install/update Anomaly and all GAMMA addons. This will take ~150GB.
    /// </summary>
    /// <param name="skipGithubDownloads">Disable downloading github addons. They will still download if these archives do not exist.</param>
    /// <param name="skipExtractOnHashMatch">Skip extracting archives when their MD5 hashes match</param>
    /// <param name="addFoldersToWinDefenderExclusion">(Windows) Add the anomaly, gamma, and cache folders to the Windows Defender Exclusion list</param>
    /// <param name="enableLongPaths">(Windows) Enable long paths</param>
    /// <param name="verbose">More verbose logging</param>
    /// <param name="minimal">Delete cache files after extracting. Could be useful for space constrained devices but increases the chance of installation failure and will make updates much slower. This will take about ~100GB.</param>
    /// <param name="offline">Perform an offline install from cache. This will not download anything if you combine this with --mod-pack-maker-path and --mod-list-path</param>
    /// <param name="modPackMakerPath">Path to modpack_maker_list.txt. Offline install.</param>
    /// <param name="modListPath">Path to modlist.txt. Offline install.</param>
    /// <param name="downloadThreads">Override downloadThreads defined in your profile</param>
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
        bool minimal = false,
        bool offline = false,
        string? modPackMakerPath = null,
        string? modListPath = null,
        [Range(1, 6)] int? downloadThreads = null,
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
        LogAndExitOnDependencyError.Check(_utilitiesReady, _logger);

        ValidateActiveProfile.Validate(_logger, cliSettings.ActiveProfile);

        ValidateOfflineRequirements(offline, modPackMakerPath, modListPath);

        InitializeSettings(
            downloadThreads,
            gammaSetupRepoUrl,
            stalkerGammaRepoUrl,
            gammaLargeFilesRepoUrl,
            teivazAnomalyGunslingerRepoUrl,
            stalkerAnomalyModdbUrl,
            stalkerAnomalyArchiveMd5,
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

        SetUpLogging(
            verbose,
            debug,
            progressUpdateIntervalMs,
            out var gammaWriteFileDisposable,
            out var gammaProgressDisposable,
            out var gammaDbgDispo
        );

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
                    Minimal = minimal,
                    Offline = offline,
                    ModPackMakerPath = modPackMakerPath,
                    ModListPath = modListPath,
                }
            );
            _logger.Information("Install finished");
        }
        catch (Exception e)
        {
            WriteToLogFile();
            _logger.Error(e, "Install failed");
            throw;
        }
        finally
        {
            WriteToLogFile();
            gammaDbgDispo?.Dispose();
            gammaProgressDisposable.Dispose();
            gammaWriteFileDisposable.Dispose();
        }
    }

    private void WriteToLogFile()
    {
        if (_alreadyWrittenToLogFile)
        {
            return;
        }
        lock (_progressEventHashSetLock)
        {
            var maxOperationLen = _progressEventHashSet.Max(x => x.Operation.Length);
            var maxArchiveNameLen = _progressEventHashSet.Max(x => x.ArchiveName.Length);
            var maxDownloadPathLen = _progressEventHashSet.Max(x => x.DownloadPath.Length);
            var maxExtractPathLen = _progressEventHashSet.Max(x => x.ExtractPath.Length);
            var maxUrlLen = _progressEventHashSet.Max(x => x.Url.Length);
            foreach (var e in _progressEventHashSet)
            {
                _logger.Verbose(
                    "Operation: {Operation} | Archive Name: {ArchiveName} | Download Path: {DownloadPath} | Extract Path: {ExtractPath} | Url: {Url}",
                    e.Operation.PadRight(maxOperationLen),
                    e.ArchiveName.PadRight(maxArchiveNameLen),
                    e.DownloadPath.PadRight(maxDownloadPathLen),
                    e.ExtractPath.PadRight(maxExtractPathLen),
                    e.Url.PadRight(maxUrlLen)
                );
            }
        }
        _alreadyWrittenToLogFile = true;
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
        string stalkerGammaRepoUrl,
        string gammaLargeFilesRepoUrl,
        string teivazAnomalyGunslingerRepoUrl,
        string stalkerAnomalyModdbUrl,
        string stalkerAnomalyArchiveMd5,
        out string anomaly,
        out string gamma,
        out string cache,
        out string mo2Profile
    )
    {
        anomaly = cliSettings.ActiveProfile!.Anomaly;
        gamma = cliSettings.ActiveProfile!.Gamma;
        cache = cliSettings.ActiveProfile!.Cache;
        mo2Profile = cliSettings.ActiveProfile!.Mo2Profile;
        var modpackMakerUrl = cliSettings.ActiveProfile!.ModPackMakerUrl;
        var modListUrl = cliSettings.ActiveProfile!.ModListUrl;
        stalkerGammaSettings.DownloadThreads =
            downloadThreads ?? cliSettings.ActiveProfile!.DownloadThreads;
        stalkerGammaSettings.ModpackMakerList = modpackMakerUrl;
        stalkerGammaSettings.ModListUrl = modListUrl;
        stalkerGammaSettings.GammaSetupRepo = gammaSetupRepoUrl;
        stalkerGammaSettings.StalkerGammaRepo = stalkerGammaRepoUrl;
        stalkerGammaSettings.GammaLargeFilesRepo = gammaLargeFilesRepoUrl;
        stalkerGammaSettings.TeivazAnomalyGunslingerRepo = teivazAnomalyGunslingerRepoUrl;
        stalkerGammaSettings.StalkerAnomalyModdbUrl = stalkerAnomalyModdbUrl;
        stalkerGammaSettings.StalkerAnomalyArchiveMd5 = stalkerAnomalyArchiveMd5;
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
    }

    private void SetUpLogging(
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
                    handler => gammaInstaller.Progress.DebugProgressChanged += handler,
                    handler => gammaInstaller.Progress.DebugProgressChanged -= handler
                )
                .Select(x => x.EventArgs);
            gammaDbgDisposable = gammaDbgObs.Subscribe(OnDebugProgressChanged);
        }

        var gammaWriteFileObs = Observable
            .FromEventPattern<GammaProgress.GammaInstallProgressEventArgs>(
                handler => gammaInstaller.Progress.ProgressChanged += handler,
                handler => gammaInstaller.Progress.ProgressChanged -= handler
            )
            .Select(x => x.EventArgs);
        gammaWriteFileDisposable = gammaWriteFileObs.Subscribe(OnProgressChangedWriteToFile);

        var gammaProgressObservable = Observable
            .FromEventPattern<GammaProgress.GammaInstallProgressEventArgs>(
                handler => gammaInstaller.Progress.ProgressChanged += handler,
                handler => gammaInstaller.Progress.ProgressChanged -= handler
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

    private void OnProgressChangedWriteToFile(GammaProgress.GammaInstallProgressEventArgs e)
    {
        lock (_progressEventHashSetLock)
        {
            _progressEventHashSet.Add(
                new LogFileRecord
                {
                    Operation = e.ProgressType,
                    ArchiveName = e.Name,
                    Url = e.Url,
                    DownloadPath = e.DownloadPath,
                    ExtractPath = e.ExtractPath,
                }
            );
        }
    }

    private bool _alreadyWrittenToLogFile;
    private readonly Lock _progressEventHashSetLock = new();
    private readonly HashSet<LogFileRecord> _progressEventHashSet = [];
    private readonly ILogger _logger = logger;
    private readonly UtilitiesReady _utilitiesReady = utilitiesReady;
    private const string Informational = "{AddonName} | {Operation} | {Percent} | {CompleteTotal}";
    private const string Verbose =
        "{AddonName} | {Operation} | {Percent} | {CompleteTotal} | {Url}";
}
