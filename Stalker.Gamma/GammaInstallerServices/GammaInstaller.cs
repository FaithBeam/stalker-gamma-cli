using System.Collections.Concurrent;
using System.Text.Json;
using Stalker.Gamma.Extensions;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices.SpecialRepos;
using Stalker.Gamma.Models;
using Stalker.Gamma.ModOrganizer;
using Stalker.Gamma.ModOrganizer.DownloadModOrganizer;
using Stalker.Gamma.Services;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices;

public class GammaInstallerArgs
{
    public required string Anomaly { get; set; }
    public required string Gamma { get; set; }
    public required string Cache { get; set; }
    public string? Mo2Version { get; set; }
    public bool DownloadGithubArchives { get; set; } = true;
    public bool SkipExtractOnHashMatch { get; set; }
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
    public string Mo2Profile { get; set; } = "G.A.M.M.A";
    public bool Minimal { get; set; }
    public bool Offline { get; set; }
    public bool PreserveUserLtx { get; set; }
    public bool PreserveMcmSettings { get; set; }
    public string? ModPackMakerPath { get; set; }
    public string? ModListPath { get; set; }
    public IList<IDownloadableRecord> GroupedAddonRecords { get; set; } = [];
    public IDownloadableRecord? AnomalyRecord { get; set; }
    public IDownloadableRecord? GammaLargeFilesRecord { get; set; }
    public IDownloadableRecord? TeivazAnomalyGunslingerRecord { get; set; }
    public IDownloadableRecord? GammaSetupRecord { get; set; }
    public IDownloadableRecord? StalkerGammaRecord { get; set; }

    public static GammaInstallerArgsBuilder Create(string anomaly, string gamma, string cache) =>
        new(anomaly, gamma, cache);
}

public class GammaInstallerArgsBuilder(string anomaly, string gamma, string cache)
{
    private bool _downloadGithubArchives = true;
    private bool _skipExtractOnHashMatch;
    private IList<IDownloadableRecord> _groupedAddonRecords = [];
    private IDownloadableRecord? _anomalyRecord;
    private CancellationToken _cancellationToken = CancellationToken.None;
    private string _mo2Profile = "G.A.M.M.A";
    private bool _minimal;
    private bool _offline;
    private bool _preserveUserLtx;
    private bool _preserveMcmSettings;
    private string? _modPackMakerPath;
    private string? _modListPath;

    public GammaInstallerArgsBuilder WithCancellationToken(CancellationToken ct)
    {
        _cancellationToken = ct;
        return this;
    }

    public GammaInstallerArgsBuilder WithDownloadGithubArchives(bool value = true)
    {
        _downloadGithubArchives = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithSkipExtractOnHashMatch(bool value = true)
    {
        _skipExtractOnHashMatch = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithMo2Profile(string profile)
    {
        _mo2Profile = profile;
        return this;
    }

    public GammaInstallerArgsBuilder WithMinimal(bool value = true)
    {
        _minimal = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithOffline(bool value = true)
    {
        _offline = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithPreserveUserLtx(bool value = true)
    {
        _preserveUserLtx = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithPreserveMcmSettings(bool value = true)
    {
        _preserveMcmSettings = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithModPackMakerPath(string? path)
    {
        _modPackMakerPath = path;
        return this;
    }

    public GammaInstallerArgsBuilder WithModListPath(string? path)
    {
        _modListPath = path;
        return this;
    }

    public GammaInstallerArgsBuilder WithGroupedAddonRecords(IList<IDownloadableRecord> records)
    {
        _groupedAddonRecords = records;
        return this;
    }

    public GammaInstallerArgsBuilder WithAnomalyRecord(IDownloadableRecord? record)
    {
        _anomalyRecord = record;
        return this;
    }

    public GammaInstallerArgs Build() =>
        new()
        {
            Anomaly = anomaly,
            Gamma = gamma,
            Cache = cache,
            DownloadGithubArchives = _downloadGithubArchives,
            SkipExtractOnHashMatch = _skipExtractOnHashMatch,
            CancellationToken = _cancellationToken,
            Mo2Profile = _mo2Profile,
            Minimal = _minimal,
            Offline = _offline,
            PreserveUserLtx = _preserveUserLtx,
            PreserveMcmSettings = _preserveMcmSettings,
            ModPackMakerPath = _modPackMakerPath,
            ModListPath = _modListPath,
            GroupedAddonRecords = _groupedAddonRecords,
            AnomalyRecord = _anomalyRecord,
        };
}


public interface IGammaInstaller
{
    IGammaProgress Progress { get; }
    Task<IList<IDownloadableRecord>> BuildGroupedAddonRecordsAsync(GammaInstallerArgs args);
    void BuildSpecialRepoRecords(GammaInstallerArgs args);
    Task InstallAsync(GammaInstallerArgs args);
    IDownloadableRecord BuildAnomalyRecord(GammaInstallerArgs args);
    Task<IList<IDownloadableRecord>> BuildUpdateGroupedAddonRecordsAsync(GammaInstallerArgs args);
}

public class GammaInstaller : IGammaInstaller
{
    public GammaInstaller(
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
        PreserveMcmSettings preserveMcmSettings
    )
    {
        Progress = gammaProgress;
        Settings = settings;
        DownloadModOrganizerService = downloadModOrganizerService;
        _getStalkerModsFromApi = getStalkerModsFromApi;
        _downloadableRecordFactory = downloadableRecordFactory;
        ModListRecordFactory = modListRecordFactory;
        SeparatorsFactory = separatorsFactory;
        PowerShellCmdBuilder = powerShellCmdBuilder;
        _getStalkerModsFromLocal = getStalkerModsFromLocal;
        PreserveUserLtxSettingsService = preserveUserLtxSettingsService;
        PreserveMcmSettings = preserveMcmSettings;
        _hc = hcf.CreateClient();
    }

    public IGammaProgress Progress { get; }
    protected StalkerGammaSettings Settings { get; }
    protected IDownloadModOrganizerService DownloadModOrganizerService { get; }
    protected IModListRecordFactory ModListRecordFactory { get; }
    protected ISeparatorsFactory SeparatorsFactory { get; }
    protected PowerShellCmdBuilder PowerShellCmdBuilder { get; }
    protected PreserveUserLtxSettingsService PreserveUserLtxSettingsService { get; }
    protected PreserveMcmSettings PreserveMcmSettings { get; }
    private readonly IGetStalkerModsFromApi _getStalkerModsFromApi;
    private readonly IDownloadableRecordFactory _downloadableRecordFactory;
    private readonly IGetStalkerModsFromLocal _getStalkerModsFromLocal;
    private readonly HttpClient _hc;

    public async Task<IList<IDownloadableRecord>> BuildGroupedAddonRecordsAsync(
        GammaInstallerArgs args
    )
    {
        var modpackMakerTxt = await GetModpackMakerTxt(args);
        var modpackMakerRecords = ModListRecordFactory.Create(modpackMakerTxt);
        var addonRecords = modpackMakerRecords
            .Select(rec =>
            {
                if (!_downloadableRecordFactory.TryCreate(args.Gamma, rec, out var dlRec))
                    return null;
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
        return _downloadableRecordFactory
            .CreateGroupedDownloadableRecords(addonRecords)
            .Select(dlRec =>
                args.SkipExtractOnHashMatch
                    ? _downloadableRecordFactory.CreateSkipExtractWhenNotDownloadedRecord(dlRec)
                    : dlRec
            )
            .ToList();
    }

    public void BuildSpecialRepoRecords(GammaInstallerArgs args)
    {
        args.GammaLargeFilesRecord = _downloadableRecordFactory.CreateGammaLargeFilesRecord(
            args.Gamma,
            Settings.GammaLargeFilesRepo,
            Settings.GammaLargeFilesRepoBranch
        );
        args.TeivazAnomalyGunslingerRecord =
            _downloadableRecordFactory.CreateTeivazAnomalyGunslingerRecord(
                args.Gamma,
                Settings.TeivazAnomalyGunslingerRepo,
                Settings.TeivazAnomalyGunslingerRepoBranch
            );
        args.GammaSetupRecord = _downloadableRecordFactory.CreateGammaSetupRecord(
            args.Gamma,
            Settings.GammaSetupRepo,
            Settings.GammaSetupRepoBranch
        );
        args.StalkerGammaRecord = _downloadableRecordFactory.CreateStalkerGammaRecord(
            args.Gamma,
            args.Anomaly,
            Settings.StalkerGammaRepo,
            Settings.StalkerGammaRepoBranch
        );
    }

    public virtual async Task InstallAsync(GammaInstallerArgs args)
    {
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

        var brokenAddons = new ConcurrentBag<IDownloadableRecord>();

        IList<IDownloadableRecord> mainBatchRecords = args.AnomalyRecord is not null
            ? [args.AnomalyRecord, .. args.GroupedAddonRecords]
            : [.. args.GroupedAddonRecords];

        var mainBatch = Task.Run(
            async () =>
                await ProcessAddonsAsync(
                    mainBatchRecords,
                    brokenAddons,
                    args.Minimal,
                    cancellationToken: args.CancellationToken
                ),
            args.CancellationToken
        );
        var teivazDlTask = Task.Run(
            async () =>
            {
                await args.TeivazAnomalyGunslingerRecord!.DownloadAsync(args.CancellationToken);
                await ((TeivazAnomalyGunslingerRepo)args.TeivazAnomalyGunslingerRecord!).ExpandFilesAsync(
                    args.CancellationToken
                );
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
                await ((GammaSetupRepo)args.GammaSetupRecord!).ExpandFilesAsync(args.CancellationToken);
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
                ? await _getStalkerModsFromApi.GetModsAsync(args.CancellationToken)
            : File.Exists(args.ModPackMakerPath)
                ? await File.ReadAllTextAsync(args.ModPackMakerPath)
            : throw new FileNotFoundException(
                $"{nameof(args.ModPackMakerPath)} file not found: {args.ModPackMakerPath}"
            );
    }

    public async Task<IList<IDownloadableRecord>> BuildUpdateGroupedAddonRecordsAsync(
        GammaInstallerArgs args
    )
    {
        var modpackMakerTxt = await _getStalkerModsFromApi.GetModsAsync(args.CancellationToken);
        var onlineModPackMakerRecords = ModListRecordFactory.Create(modpackMakerTxt);
        var localRecords = await _getStalkerModsFromLocal.GetMods(args.Gamma, args.Mo2Profile);
        var addedOrModifiedRecords = localRecords
            .Diff(onlineModPackMakerRecords)
            .Where(x => x.DiffType is DiffType.Added or DiffType.Modified)
            .Select(x => x.NewListRecord!)
            .ToList();
        var addonRecords = addedOrModifiedRecords
            .Select(rec =>
                _downloadableRecordFactory.TryCreate(args.Gamma, rec, out var dlRec) ? dlRec : null
            )
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
        return _downloadableRecordFactory.CreateGroupedDownloadableRecords(addonRecords).ToList();
    }

    public IDownloadableRecord BuildAnomalyRecord(GammaInstallerArgs args)
    {
        var anomalyRecord = _downloadableRecordFactory.CreateAnomalyRecord(
            Path.Join(args.Gamma, "downloads"),
            args.Anomaly
        );
        return args.SkipExtractOnHashMatch
            ? _downloadableRecordFactory.CreateSkipExtractWhenNotDownloadedRecord(anomalyRecord)
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
                catch (ModDbBotDetectedException)
                {
                    throw;
                }
                catch (Exception)
                {
                    brokenAddons.Add(grs);
                }
            }
        );

}

