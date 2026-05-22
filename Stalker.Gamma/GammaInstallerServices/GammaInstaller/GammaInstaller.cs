using System.Collections.Concurrent;
using System.Text.Json;
using Stalker.Gamma.Extensions;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices.SpecialRepos;
using Stalker.Gamma.Models;
using Stalker.Gamma.ModOrganizer;
using Stalker.Gamma.ModOrganizer.DownloadModOrganizer;
using Stalker.Gamma.Proxies;
using Stalker.Gamma.Services;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices.GammaInstaller;

public class GammaInstaller(
    StalkerGammaSettings settings,
    GammaProgress gammaProgress,
    IDownloadModOrganizerService downloadModOrganizerService,
    IGetStalkerModsFromApi getStalkerModsFromApi,
    IDownloadableRecordFactory downloadableRecordFactory,
    IModListRecordFactory modListRecordFactory,
    ISeparatorsFactory separatorsFactory,
    IHttpClientFactory hcf,
    PowerShellCmdBuilder powerShellCmdBuilder,
    IGetStalkerModsFromLocal getStalkerModsFromLocal,
    PreserveUserLtxSettingsService preserveUserLtxSettingsService,
    PreserveMcmSettings preserveMcmSettings,
    PythonServerService pythonServerService,
    PythonApiProxy pythonApiProxy
) : IGammaInstaller, IDisposable
{
    public IGammaProgress Progress { get; } = gammaProgress;
    protected StalkerGammaSettings Settings { get; } = settings;
    protected IDownloadModOrganizerService DownloadModOrganizerService { get; } =
        downloadModOrganizerService;
    protected IModListRecordFactory ModListRecordFactory { get; } = modListRecordFactory;
    protected ISeparatorsFactory SeparatorsFactory { get; } = separatorsFactory;
    protected PowerShellCmdBuilder PowerShellCmdBuilder { get; } = powerShellCmdBuilder;
    protected PreserveUserLtxSettingsService PreserveUserLtxSettingsService { get; } =
        preserveUserLtxSettingsService;
    protected PreserveMcmSettings PreserveMcmSettings { get; } = preserveMcmSettings;
    private readonly HttpClient _hc = hcf.CreateClient();

    public async Task<IList<IDownloadableRecord>> BuildGroupedAddonRecordsAsync(
        GammaInstallerArgs args
    )
    {
        var modpackMakerTxt = await GetModpackMakerTxt(args);
        var modpackMakerRecords = ModListRecordFactory.Create(modpackMakerTxt);
        var addonRecords = modpackMakerRecords
            .Select(rec =>
            {
                if (
                    !downloadableRecordFactory.TryCreate(
                        args.Gamma,
                        rec,
                        out var dlRec,
                        useCurl: !args.UseExperimentalPythonServer
                    )
                )
                {
                    return null;
                }
                if (dlRec is GithubRecord ghr)
                {
                    ghr.Download = args.DownloadGithubArchives;
                    return ghr;
                }
                return dlRec;
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
        return downloadableRecordFactory
            .CreateGroupedDownloadableRecords(addonRecords)
            .Select(dlRec =>
                args.SkipExtractOnHashMatch
                    ? downloadableRecordFactory.CreateSkipExtractWhenNotDownloadedRecord(dlRec)
                    : dlRec
            )
            .Shuffle() // randomize the order addons are downloaded
            .ToList();
    }

    public void BuildSpecialRepoRecords(GammaInstallerArgs args)
    {
        args.GammaLargeFilesRecord = downloadableRecordFactory.CreateGammaLargeFilesRecord(
            args.Gamma,
            Settings.GammaLargeFilesRepo,
            Settings.GammaLargeFilesRepoBranch
        );
        args.TeivazAnomalyGunslingerRecord =
            downloadableRecordFactory.CreateTeivazAnomalyGunslingerRecord(
                args.Gamma,
                Settings.TeivazAnomalyGunslingerRepo,
                Settings.TeivazAnomalyGunslingerRepoBranch
            );
        args.GammaSetupRecord = downloadableRecordFactory.CreateGammaSetupRecord(
            args.Gamma,
            Settings.GammaSetupRepo,
            Settings.GammaSetupRepoBranch
        );
        args.StalkerGammaRecord = downloadableRecordFactory.CreateStalkerGammaRecord(
            args.Gamma,
            args.Anomaly,
            Settings.StalkerGammaRepo,
            Settings.StalkerGammaRepoBranch
        );
    }

    public virtual async Task InstallAsync(GammaInstallerArgs args)
    {
        if (
            args is
            { UseExperimentalPythonServer: true, ExperimentalPythonServerSettings: not null }
        )
        {
            pythonServerService.Start(
                args.ExperimentalPythonServerSettings.Host,
                args.ExperimentalPythonServerSettings.Port,
                args.CancellationToken
            );

            while (!await pythonApiProxy.Ready())
            {
                await Task.Delay(TimeSpan.FromSeconds(1), args.CancellationToken);
            }
        }

        args.Mo2Version = "v2.5.2";
        args.Cache = Path.IsPathRooted(args.Cache) ? args.Cache : Path.GetFullPath(args.Cache);
        args.Gamma = Path.IsPathRooted(args.Gamma) ? args.Gamma : Path.GetFullPath(args.Gamma);
        args.Anomaly = Path.IsPathRooted(args.Anomaly)
            ? args.Anomaly
            : Path.GetFullPath(args.Anomaly);

        var anomalyBinPath = Path.Join(args.Anomaly, "bin");
        var gammaModsPath = Path.Join(args.Gamma, "mods");
        var gammaDownloadsPath = Path.Join(args.Gamma, "downloads");

        Directory.CreateDirectory(args.Anomaly);
        Directory.CreateDirectory(args.Gamma);
        Directory.CreateDirectory(args.Cache);
        Directory.CreateDirectory(gammaModsPath);
        CreateSymbolicLinkUtility.Create(gammaDownloadsPath, args.Cache, PowerShellCmdBuilder);
        if (OperatingSystem.IsWindows())
        {
            await PowerShellCmdBuilder.Build().ExecuteAsync(args.CancellationToken);
        }

        if (args.PreserveUserLtx)
        {
            await PreserveUserLtxSettingsService.ReadUserLtxAsync(
                args.Anomaly,
                args.CancellationToken
            );
        }

        if (args.PreserveMcmSettings)
        {
            await PreserveMcmSettings.ReadAxrOptionsAsync(args.Gamma, args.CancellationToken);
        }

        var modpackMakerTxt = await GetModpackMakerTxt(args);
        var modpackMakerRecords = ModListRecordFactory.Create(modpackMakerTxt);
        var separators = SeparatorsFactory.Create(modpackMakerRecords);

        var internalProgress = Progress as GammaProgress;
        internalProgress!.TotalMods =
            new List<IDownloadableRecord>(args.GroupedAddonRecords)
            {
                args.GammaLargeFilesRecord!,
                args.TeivazAnomalyGunslingerRecord!,
                args.GammaSetupRecord!,
                args.StalkerGammaRecord!,
            }.Count + (args.AnomalyRecord is not null ? 1 : 0);

        foreach (var separator in separators)
        {
            await separator.WriteAsync(args.Gamma);
        }

        IList<IDownloadableRecord> mainBatchRecords = args.AnomalyRecord is not null
            ? [args.AnomalyRecord, .. args.GroupedAddonRecords]
            : [.. args.GroupedAddonRecords];

        ConcurrentBag<IDownloadableRecord> brokenAddons = [];

        var mainBatch = ProcessAddonsAsync(
            mainBatchRecords,
            brokenAddons,
            args.Minimal,
            cancellationToken: args.CancellationToken
        );
        var teivazDlTask = Task.Run(
            async () =>
            {
                await args.TeivazAnomalyGunslingerRecord!.DownloadAsync(args.CancellationToken);
                await (
                    (TeivazAnomalyGunslingerRepo)args.TeivazAnomalyGunslingerRecord!
                ).ExpandFilesAsync(args.CancellationToken);
            },
            args.CancellationToken
        );
        var gammaLargeFilesDlTask = Task.Run(
            async () =>
            {
                await args.GammaLargeFilesRecord!.DownloadAsync(args.CancellationToken);
                await ((GammaLargeFilesRepo)args.GammaLargeFilesRecord!).ExpandFilesAsync(
                    args.CancellationToken
                );
            },
            args.CancellationToken
        );
        var gammaSetupDownloadTask = Task.Run(
            async () =>
            {
                await args.GammaSetupRecord!.DownloadAsync(args.CancellationToken);
                await ((GammaSetupRepo)args.GammaSetupRecord!).ExpandFilesAsync(
                    args.CancellationToken
                );
            },
            args.CancellationToken
        );
        var stalkerGammaDownloadTask = Task.Run(
            async () =>
            {
                await args.StalkerGammaRecord!.DownloadAsync(args.CancellationToken);
                await ((StalkerGammaRepo)args.StalkerGammaRecord!).ExpandFilesAsync(
                    args.CancellationToken
                );
            },
            args.CancellationToken
        );

        await Task.WhenAll(
            mainBatch,
            teivazDlTask,
            gammaLargeFilesDlTask,
            gammaSetupDownloadTask,
            stalkerGammaDownloadTask
        );

        foreach (var brokenAddon in brokenAddons)
        {
            await brokenAddon.DownloadAsync(args.CancellationToken);
            await brokenAddon.ExtractAsync(args.CancellationToken);
        }

        await args.GammaSetupRecord!.ExtractAsync(args.CancellationToken);
        await args.StalkerGammaRecord!.ExtractAsync(args.CancellationToken);
        await args.GammaLargeFilesRecord!.ExtractAsync(args.CancellationToken);
        await args.TeivazAnomalyGunslingerRecord!.ExtractAsync(args.CancellationToken);
        if (args.Minimal)
        {
            args.GammaSetupRecord!.DeleteArchive();
            args.StalkerGammaRecord!.DeleteArchive();
            args.GammaLargeFilesRecord!.DeleteArchive();
            args.TeivazAnomalyGunslingerRecord!.DeleteArchive();
        }

        DeleteReshadeDlls.Delete(anomalyBinPath);
        DeleteShaderCache.Delete(args.Anomaly);

        if (args.PreserveUserLtx)
        {
            await PreserveUserLtxSettingsService.WriteUserLtxAsync(args.CancellationToken);
        }
        await UserLtxForceBorderless.ForceBorderless(args.Anomaly);

        if (args.PreserveMcmSettings)
        {
            await PreserveMcmSettings.WriteAxrOptionsAsync(args.CancellationToken);
        }

        await DownloadModOrganizerService.DownloadAsync(
            cachePath: args.Cache,
            extractPath: args.Gamma,
            version: args.Mo2Version,
            cancellationToken: args.CancellationToken
        );
        await DownloadModOrganizerService.ExtractAsync(
            version: args.Mo2Version,
            cachePath: args.Cache,
            extractPath: args.Gamma,
            cancellationToken: args.CancellationToken
        );

        if (args.Minimal)
        {
            DownloadModOrganizerService.DeleteArchive(args.Cache);
        }

        await InstallModOrganizerGammaProfile.InstallAsync(
            Path.Join(gammaDownloadsPath, args.StalkerGammaRecord!.Name),
            args.Gamma,
            args.Mo2Profile
        );
        await WriteModOrganizerIni.WriteAsync(
            args.Gamma,
            args.Anomaly,
            args.Mo2Version,
            separators.Select(x => x.FolderName).ToList(),
            args.Mo2Profile
        );
        await DisableNexusModHandlerLink.DisableAsync(args.Gamma);

        var mo2ProfilePath = Path.Join(args.Gamma, "profiles", args.Mo2Profile);
        Directory.CreateDirectory(mo2ProfilePath);
        if (
            !string.IsNullOrWhiteSpace(args.ModListPath)
            || !string.IsNullOrWhiteSpace(Settings.ModListUrl)
        )
        {
            var modList =
                !string.IsNullOrWhiteSpace(args.ModListPath)
                    ? await File.ReadAllTextAsync(
                        args.ModListPath,
                        cancellationToken: args.CancellationToken
                    )
                : !string.IsNullOrWhiteSpace(Settings.ModListUrl)
                    ? await _hc.GetStringAsync(Settings.ModListUrl)
                : throw new InvalidOperationException("Mod list path or url is empty");
            Directory.CreateDirectory(mo2ProfilePath);
            await File.WriteAllTextAsync(Path.Join(mo2ProfilePath, "modlist.txt"), modList);
        }

        await File.WriteAllTextAsync(
            Path.Join(mo2ProfilePath, "modpack_maker_list.txt"),
            modpackMakerTxt
        );
        await File.WriteAllTextAsync(
            Path.Join(mo2ProfilePath, "modpack_maker_list.json"),
            JsonSerializer.Serialize(
                modpackMakerRecords,
                jsonTypeInfo: ModPackMakerCtx.Default.ListModPackMakerRecord
            )
        );

        internalProgress.Reset();
    }

    protected async Task<string> GetModpackMakerTxt(GammaInstallerArgs args)
    {
        return string.IsNullOrWhiteSpace(args.ModPackMakerPath)
                ? await getStalkerModsFromApi.GetModsAsync(args.CancellationToken)
            : File.Exists(args.ModPackMakerPath)
                ? await File.ReadAllTextAsync(args.ModPackMakerPath)
            : throw new FileNotFoundException(
                $"{nameof(args.ModPackMakerPath)} file not found: {args.ModPackMakerPath}"
            );
    }

    public class DiffedAddonRecords
    {
        public required List<ModPackMakerRecord> OnlineRecords { get; set; }
        public required List<ModPackMakerRecord> AddedOrModifiedRecords { get; set; }
        public required List<ModPackMakerRecord> LocalRecords { get; set; }
    }

    public async Task<DiffedAddonRecords> DiffAddonRecordsAsync(GammaInstallerArgs args)
    {
        var modpackMakerTxt = await getStalkerModsFromApi.GetModsAsync(args.CancellationToken);
        var onlineModPackMakerRecords = ModListRecordFactory.Create(modpackMakerTxt);
        var localRecords = await getStalkerModsFromLocal.GetMods(args.Gamma, args.Mo2Profile);
        var addedOrModifiedRecords = localRecords
            .Diff(onlineModPackMakerRecords)
            .Where(x => x.DiffType is DiffType.Added or DiffType.Modified)
            .Select(x => x.NewListRecord!)
            .ToList();
        return new DiffedAddonRecords
        {
            LocalRecords = localRecords,
            OnlineRecords = onlineModPackMakerRecords,
            AddedOrModifiedRecords = addedOrModifiedRecords,
        };
    }

    public async Task<IList<IDownloadableRecord>> BuildUpdateGroupedAddonRecordsAsync(
        GammaInstallerArgs args
    )
    {
        var diffedAddonRecords = await DiffAddonRecordsAsync(args);
        var addonRecords = diffedAddonRecords
            .AddedOrModifiedRecords.Select(rec =>
                downloadableRecordFactory.TryCreate(args.Gamma, rec, out var dlRec) ? dlRec : null
            )
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
        return downloadableRecordFactory.CreateGroupedDownloadableRecords(addonRecords).ToList();
    }

    public IDownloadableRecord BuildAnomalyRecord(GammaInstallerArgs args)
    {
        var anomalyRecord = downloadableRecordFactory.CreateAnomalyRecord(
            Path.Join(args.Gamma, "downloads"),
            args.Anomaly,
            useCurl: !args.UseExperimentalPythonServer
        );
        return args.SkipExtractOnHashMatch
            ? downloadableRecordFactory.CreateSkipExtractWhenNotDownloadedRecord(anomalyRecord)
            : anomalyRecord;
    }

    protected virtual async Task ProcessAddonsAsync(
        IList<IDownloadableRecord> addons,
        ConcurrentBag<IDownloadableRecord> brokenAddons,
        bool minimal = false,
        CancellationToken cancellationToken = default
    ) =>
        await Parallel.ForEachAsync(
            addons,
            new ParallelOptions { MaxDegreeOfParallelism = Settings.DownloadThreads },
            async (grs, _) =>
            {
                try
                {
                    await grs.DownloadAsync(cancellationToken);
                    await grs.ExtractAsync(cancellationToken);
                    if (minimal)
                    {
                        grs.DeleteArchive();
                    }
                }
                catch (Exception)
                {
                    brokenAddons.Add(grs);
                }
            }
        );

    public void Dispose()
    {
        _hc.Dispose();
        pythonServerService.Dispose();
    }
}
