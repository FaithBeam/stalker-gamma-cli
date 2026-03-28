using System.Reactive.Linq;
using System.Text.Json;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Services;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Extensions;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Models;
using Stalker.Gamma.Utilities;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("update")]
public class UpdateCmds(
    ILogger logger,
    CliSettings cliSettings,
    StalkerGammaSettings stalkerGammaSettings,
    IGetStalkerModsFromApi getStalkerModsFromApi,
    IModListRecordFactory modListRecordFactory,
    GammaInstaller gammaInstaller,
    GitUtility gitUtility,
    UtilitiesReady utilitiesReady,
    GetRemoteGitRepoCommit getRemoteGitRepoCommit,
    ProgressLoggingService progressLoggingService
)
{
    /// <summary>
    /// Check for updates
    /// </summary>
    [Command("check")]
    public async Task CheckUpdates(CancellationToken cancellationToken = default)
    {
        LogAndExitOnDependencyError.Check(utilitiesReady, _logger);
        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
        stalkerGammaSettings.ModpackMakerList = _cliSettings.ActiveProfile!.ModPackMakerUrl;

        var gammaDownloadsPath = Path.Join(_cliSettings.ActiveProfile!.Gamma, "downloads");
        List<string> repos =
        [
            "gamma_setup",
            "gamma_large_files_v2",
            "Stalker_GAMMA",
            "teivaz_anomaly_gunslinger",
        ];
        const string repoOwner = "Grokitach";

        var localRepos = repos
            .Select(x => new { Name = x, Path = Path.Join(gammaDownloadsPath, $"{x}.git") })
            .Where(repoDir => Directory.Exists(repoDir.Path))
            .ToList();
        var localRepoModPackMakerRecs = localRepos
            .Select(repoDir =>
            {
                var sha = gitUtility.GetLatestCommitHash(repoDir.Path);
                return new ModPackMakerRecord
                {
                    DlLink = $"https://github.com/{repoOwner}/{repoDir.Name}",
                    AddonName = repoDir.Name,
                    Md5ModDb = sha,
                    ZipName = sha[..7],
                };
            })
            .ToList();
        var remoteRepoModPackMakerRecs = localRepos
            .ToAsyncEnumerable()
            .Select(async repoDir =>
            {
                var sha =
                    await getRemoteGitRepoCommit.ExecuteAsync(
                        repoOwner,
                        repoDir.Name,
                        cancellationToken
                    ) ?? "N/A";
                return new ModPackMakerRecord
                {
                    DlLink = $"https://github.com/{repoOwner}/{repoDir.Name}",
                    AddonName = repoDir.Name,
                    Md5ModDb = sha,
                    ZipName = sha[..7],
                };
            })
            .WithCancellation(cancellationToken);
        var localGitRepoDiffs = await localRepoModPackMakerRecs.DiffAsync(
            remoteRepoModPackMakerRecs
        );

        var localModPackMakerPath = Path.Join(
            _cliSettings.ActiveProfile!.Gamma,
            "profiles",
            _cliSettings.ActiveProfile.Mo2Profile,
            "modpack_maker_list.json"
        );

        var localRecords = File.Exists(localModPackMakerPath)
            ? JsonSerializer.Deserialize(
                await File.ReadAllTextAsync(localModPackMakerPath, cancellationToken),
                jsonTypeInfo: ModPackMakerCtx.Default.ListModPackMakerRecord
            ) ?? []
            : [];
        var onlineRecordsTxt = await _getStalkerModsFromApi.GetModsAsync(cancellationToken);
        var onlineRecords = _modListRecordFactory.Create(onlineRecordsTxt);
        var diffs = localRecords.Diff(onlineRecords).Concat(localGitRepoDiffs).ToList();
        if (diffs.Count > 0)
        {
            var olds = diffs
                .Where(x =>
                    x.OldListRecord is not null
                    && !string.IsNullOrWhiteSpace(x.OldListRecord.AddonName)
                )
                .Select(x => x.OldListRecord!);
            var news = diffs
                .Where(x =>
                    x.NewListRecord is not null
                    && !string.IsNullOrWhiteSpace(x.NewListRecord.AddonName)
                )
                .Select(x => x.NewListRecord!);
            var joined = olds.Concat(news).ToList();
            var padRightAddonName = joined.MaxBy(x => x.AddonName!.Length)!.AddonName!.Length + 5;
            var padRightOldZipName =
                diffs.MaxBy(x => x.OldListRecord?.ZipName?.Length)?.OldListRecord?.ZipName?.Length
                ?? 3;
            var padRightStatus = nameof(DiffType.Modified).Length;

            _logger.Information("Updates available: {NumberUpdates}", diffs.Count);

            foreach (var diff in diffs)
            {
                if (diff.DiffType == DiffType.Modified)
                {
                    _logger.Information(
                        "{Status}: {AddonName} {OldZipName} -> {NewZipName}",
                        diff.DiffType.ToString().PadRight(padRightStatus),
                        diff.OldListRecord!.AddonName!.PadRight(padRightAddonName),
                        diff.OldListRecord.ZipName!.PadRight(padRightOldZipName),
                        diff.NewListRecord!.ZipName
                    );
                }
                else
                {
                    _logger.Information(
                        "{Status}: {AddonName} {OldZipName} -> {NewZipName}",
                        diff.DiffType.ToString().PadRight(padRightStatus),
                        diff.DiffType switch
                        {
                            DiffType.Added =>
                                $"{diff.NewListRecord?.AddonName ?? diff.NewListRecord?.DlLink ?? "N/A"}".PadRight(
                                    padRightAddonName
                                ),
                            DiffType.Removed => diff.OldListRecord?.AddonName?.PadRight(
                                padRightAddonName
                            ),
                            _ => throw new ArgumentOutOfRangeException(),
                        },
                        $"{diff.OldListRecord?.ZipName ?? "N/A"}".PadRight(padRightOldZipName),
                        $"{diff.NewListRecord?.ZipName ?? "N/A"}"
                    );
                }
            }

            _logger.Information("To apply updates, run `stalker-gamma update apply`");
        }
        else
        {
            _logger.Information("No updates found");
        }
    }

    /// <summary>
    /// Apply any updates
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <param name="verbose"></param>
    /// <param name="minimal"></param>
    /// <param name="mo2Version"></param>
    /// <param name="progressUpdateIntervalMs"></param>
    public async Task Apply(
        CancellationToken cancellationToken,
        bool verbose = false,
        bool minimal = false,
        [Hidden] string? mo2Version = null,
        [Hidden] long progressUpdateIntervalMs = 250
    )
    {
        LogAndExitOnDependencyError.Check(utilitiesReady, _logger);

        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);

        InitializeSettings(out var anomaly, out var gamma, out var cache, out var mo2Profile);

        SetUpLogging(
            verbose,
            progressUpdateIntervalMs,
            out var gammaProgressDisposable,
            out var gammaWriteFileDisposable
        );

        try
        {
            await gammaInstaller.UpdateAsync(
                new InstallUpdatesArgs
                {
                    Gamma = gamma,
                    Anomaly = anomaly,
                    Cache = cache,
                    CancellationToken = cancellationToken,
                    Mo2Profile = mo2Profile,
                    Mo2Version = mo2Version,
                    Minimal = minimal,
                }
            );
            _logger.Information("Update finished");
        }
        catch (Exception e)
        {
            progressLoggingService.WriteToLogFile();
            _logger.Error(e, "Update failed");
            throw;
        }
        finally
        {
            progressLoggingService.WriteToLogFile();
            gammaWriteFileDisposable.Dispose();
            gammaProgressDisposable.Dispose();
        }
    }

    private void InitializeSettings(
        out string anomaly,
        out string gamma,
        out string cache,
        out string mo2Profile
    )
    {
        stalkerGammaSettings.ModpackMakerList = _cliSettings.ActiveProfile!.ModPackMakerUrl;
        anomaly = _cliSettings.ActiveProfile!.Anomaly;
        gamma = _cliSettings.ActiveProfile!.Gamma;
        cache = _cliSettings.ActiveProfile!.Cache;
        mo2Profile = _cliSettings.ActiveProfile!.Mo2Profile;
        var modpackMakerUrl = _cliSettings.ActiveProfile!.ModPackMakerUrl;
        var modListUrl = _cliSettings.ActiveProfile!.ModListUrl;
        stalkerGammaSettings.DownloadThreads = _cliSettings.ActiveProfile!.DownloadThreads;
        stalkerGammaSettings.ModpackMakerList = modpackMakerUrl;
        stalkerGammaSettings.ModListUrl = modListUrl;
    }

    private void SetUpLogging(
        bool verbose,
        long progressUpdateIntervalMs,
        out IDisposable gammaProgressDisposable,
        out IDisposable gammaWriteFileDisposable
    )
    {
        var gammaWriteFileObs = Observable
            .FromEventPattern<GammaProgress.GammaInstallProgressEventArgs>(
                handler => gammaInstaller.Progress.ProgressChanged += handler,
                handler => gammaInstaller.Progress.ProgressChanged -= handler
            )
            .Select(x => x.EventArgs);
        gammaWriteFileDisposable = gammaWriteFileObs.Subscribe(
            progressLoggingService.OnProgressChangedWriteToFile
        );

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

    private const string Informational = "{AddonName} | {Operation} | {Percent} | {CompleteTotal}";
    private const string Verbose =
        "{AddonName} | {Operation} | {Percent} | {CompleteTotal} | {Url}";

    private readonly ILogger _logger = logger;
    private readonly CliSettings _cliSettings = cliSettings;
    private readonly IGetStalkerModsFromApi _getStalkerModsFromApi = getStalkerModsFromApi;
    private readonly IModListRecordFactory _modListRecordFactory = modListRecordFactory;
}
