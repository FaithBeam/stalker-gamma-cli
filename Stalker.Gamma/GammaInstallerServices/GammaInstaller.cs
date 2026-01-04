using System.Collections.Concurrent;
using System.Text.Json;
using Stalker.Gamma.Extensions;
using Stalker.Gamma.Factories;
using Stalker.Gamma.Models;
using Stalker.Gamma.ModOrganizer;
using Stalker.Gamma.ModOrganizer.DownloadModOrganizer;
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
}

public class InstallUpdatesArgs
{
    public required string Anomaly { get; set; }
    public required string Gamma { get; set; }
    public required string Cache { get; set; }
    public string? Mo2Version { get; set; }
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
    public string Mo2Profile { get; set; } = "G.A.M.M.A";
}

public class GammaInstaller(
    StalkerGammaSettings settings,
    GammaProgress gammaProgress,
    IDownloadModOrganizerService downloadModOrganizerService,
    IGetStalkerModsFromApi getStalkerModsFromApi,
    IDownloadableRecordFactory downloadableRecordFactory,
    IModListRecordFactory modListRecordFactory,
    ISeparatorsFactory separatorsFactory,
    IHttpClientFactory hcf
)
{
    public IGammaProgress Progress { get; } = gammaProgress;

    public virtual async Task FullInstallAsync(GammaInstallerArgs args)
    {
        args.Mo2Version ??= OperatingSystem.IsWindows() ? "v2.5.2" : "v2.4.4";
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
        CreateSymbolicLinkUtility.Create(gammaDownloadsPath, args.Cache);

        var modpackMakerTxt = await getStalkerModsFromApi.GetModsAsync();
        var modpackMakerRecords = modListRecordFactory.Create(modpackMakerTxt);
        var separators = separatorsFactory.Create(modpackMakerRecords);
        var anomalyRecord = downloadableRecordFactory.CreateAnomalyRecord(
            Path.Join(args.Gamma, "downloads"),
            args.Anomaly
        );
        if (args.SkipExtractOnHashMatch)
        {
            anomalyRecord = downloadableRecordFactory.CreateSkipExtractWhenNotDownloadedRecord(
                anomalyRecord
            );
        }

        var addonRecords = modpackMakerRecords
            .Select(rec =>
            {
                if (!downloadableRecordFactory.TryCreate(args.Gamma, rec, out var dlRec))
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
        var groupedAddonRecords = downloadableRecordFactory
            .CreateGroupedDownloadableRecords(addonRecords)
            .Select(dlRec =>
            {
                if (args.SkipExtractOnHashMatch)
                {
                    return downloadableRecordFactory.CreateSkipExtractWhenNotDownloadedRecord(
                        dlRec
                    );
                }

                return dlRec;
            })
            .ToList();
        var gammaLargeFilesRecord = downloadableRecordFactory.CreateGammaLargeFilesRecord(
            args.Gamma
        );
        var teivazAnomalyGunslingerRecord =
            downloadableRecordFactory.CreateTeivazAnomalyGunslingerRecord(args.Gamma);
        var gammaSetupRecord = downloadableRecordFactory.CreateGammaSetupRecord(
            args.Gamma,
            args.Anomaly
        );
        var stalkerGammaRecord = downloadableRecordFactory.CreateStalkerGammaRecord(
            args.Gamma,
            args.Anomaly
        );

        var internalProgress = Progress as GammaProgress;
        internalProgress!.TotalMods = new List<IDownloadableRecord>(groupedAddonRecords)
        {
            anomalyRecord,
            gammaLargeFilesRecord,
            teivazAnomalyGunslingerRecord,
            gammaSetupRecord,
            stalkerGammaRecord,
        }.Count;

        foreach (var separator in separators)
        {
            await separator.WriteAsync(args.Gamma);
        }

        var brokenAddons = new ConcurrentBag<IDownloadableRecord>();

        // Batch #1
        var mainBatch = Task.Run(
            async () =>
                await ProcessAddonsAsync(
                    [anomalyRecord, .. groupedAddonRecords],
                    brokenAddons,
                    args.CancellationToken
                ),
            args.CancellationToken
        );
        var teivazDlTask = Task.Run(
            async () => await teivazAnomalyGunslingerRecord.DownloadAsync(args.CancellationToken),
            args.CancellationToken
        );
        var gammaLargeFilesDlTask = Task.Run(
            async () => await gammaLargeFilesRecord.DownloadAsync(args.CancellationToken),
            args.CancellationToken
        );
        var gammaSetupDownloadTask = Task.Run(
            async () => await gammaSetupRecord.DownloadAsync(args.CancellationToken),
            args.CancellationToken
        );
        var stalkerGammaDownloadTask = Task.Run(
            async () => await stalkerGammaRecord.DownloadAsync(args.CancellationToken),
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

        await gammaSetupRecord.ExtractAsync(args.CancellationToken);
        await stalkerGammaRecord.ExtractAsync(args.CancellationToken);
        await gammaLargeFilesRecord.ExtractAsync(args.CancellationToken);
        await teivazAnomalyGunslingerRecord.ExtractAsync(args.CancellationToken);

        DeleteReshadeDlls.Delete(anomalyBinPath);
        DeleteShaderCache.Delete(args.Anomaly);
        await UserLtxForceBorderless.ForceBorderless(args.Anomaly);

        await downloadModOrganizerService.DownloadAsync(
            cachePath: args.Cache,
            extractPath: args.Gamma,
            version: args.Mo2Version,
            cancellationToken: args.CancellationToken
        );

        await InstallModOrganizerGammaProfile.InstallAsync(
            Path.Join(gammaDownloadsPath, stalkerGammaRecord.Name),
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
        if (!string.IsNullOrWhiteSpace(settings.ModListUrl))
        {
            var modlist = await _hc.GetStringAsync(settings.ModListUrl);
            Directory.CreateDirectory(mo2ProfilePath);
            await File.WriteAllTextAsync(Path.Join(mo2ProfilePath, "modlist.txt"), modlist);
        }

        var mo2ProfileModListPath = Path.Join(mo2ProfilePath, "modpack_maker_list.json");
        await File.WriteAllTextAsync(
            mo2ProfileModListPath,
            JsonSerializer.Serialize(
                modpackMakerRecords,
                jsonTypeInfo: ModPackMakerCtx.Default.ListModPackMakerRecord
            )
        );

        internalProgress.Reset();
    }

    public virtual async Task UpdateAsync(InstallUpdatesArgs args)
    {
        args.Mo2Version ??= OperatingSystem.IsWindows() ? "v2.5.2" : "v2.4.4";
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
        CreateSymbolicLinkUtility.Create(gammaDownloadsPath, args.Cache);

        var onlineModPackMakerRecords = modListRecordFactory.Create(
            await getStalkerModsFromApi.GetModsAsync()
        );
        var localRecords =
            JsonSerializer.Deserialize<List<ModPackMakerRecord>>(
                await File.ReadAllTextAsync(
                    Path.Join(args.Gamma, "profiles", args.Mo2Profile, "modpack_maker_list.json")
                ),
                jsonTypeInfo: ModPackMakerCtx.Default.ListModPackMakerRecord
            ) ?? [];
        var addedOrModifiedRecords = localRecords
            .Diff(onlineModPackMakerRecords)
            .Where(x => x.DiffType is DiffType.Added or DiffType.Modified)
            .Select(x => x.NewListRecord!)
            .ToList();

        var separators = separatorsFactory.Create(onlineModPackMakerRecords);

        var addonRecords = addedOrModifiedRecords
            .Select(rec =>
                downloadableRecordFactory.TryCreate(args.Gamma, rec, out var dlRec) ? dlRec : null
            )
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
        var groupedAddonRecords = downloadableRecordFactory
            .CreateGroupedDownloadableRecords(addonRecords)
            .ToList();
        var gammaLargeFilesRecord = downloadableRecordFactory.CreateGammaLargeFilesRecord(
            args.Gamma
        );
        var teivazAnomalyGunslingerRecord =
            downloadableRecordFactory.CreateTeivazAnomalyGunslingerRecord(args.Gamma);
        var gammaSetupRecord = downloadableRecordFactory.CreateGammaSetupRecord(
            args.Gamma,
            args.Anomaly
        );
        var stalkerGammaRecord = downloadableRecordFactory.CreateStalkerGammaRecord(
            args.Gamma,
            args.Anomaly
        );

        var internalProgress = Progress as GammaProgress;
        internalProgress!.TotalMods = new List<IDownloadableRecord>(groupedAddonRecords)
        {
            gammaLargeFilesRecord,
            teivazAnomalyGunslingerRecord,
            gammaSetupRecord,
            stalkerGammaRecord,
        }.Count;

        var brokenAddons = new ConcurrentBag<IDownloadableRecord>();

        var mainBatch = Task.Run(
            async () =>
                await ProcessAddonsAsync(groupedAddonRecords, brokenAddons, args.CancellationToken),
            args.CancellationToken
        );
        var teivazDlTask = Task.Run(
            async () => await teivazAnomalyGunslingerRecord.DownloadAsync(args.CancellationToken),
            args.CancellationToken
        );
        var gammaLargeFilesDlTask = Task.Run(
            async () => await gammaLargeFilesRecord.DownloadAsync(args.CancellationToken),
            args.CancellationToken
        );
        var gammaSetupDownloadTask = Task.Run(
            async () => await gammaSetupRecord.DownloadAsync(args.CancellationToken),
            args.CancellationToken
        );
        var stalkerGammaDownloadTask = Task.Run(
            async () => await stalkerGammaRecord.DownloadAsync(args.CancellationToken),
            args.CancellationToken
        );

        foreach (var separator in separators)
        {
            await separator.WriteAsync(args.Gamma);
        }

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

        await gammaSetupRecord.ExtractAsync(args.CancellationToken);
        await stalkerGammaRecord.ExtractAsync(args.CancellationToken);
        await gammaLargeFilesRecord.ExtractAsync(args.CancellationToken);
        await teivazAnomalyGunslingerRecord.ExtractAsync(args.CancellationToken);

        DeleteReshadeDlls.Delete(anomalyBinPath);
        DeleteShaderCache.Delete(args.Anomaly);
        await UserLtxForceBorderless.ForceBorderless(args.Anomaly);

        await downloadModOrganizerService.DownloadAsync(
            cachePath: args.Cache,
            extractPath: args.Gamma,
            version: args.Mo2Version,
            cancellationToken: args.CancellationToken
        );

        await InstallModOrganizerGammaProfile.InstallAsync(
            Path.Join(gammaDownloadsPath, stalkerGammaRecord.Name),
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
        if (!string.IsNullOrWhiteSpace(settings.ModListUrl))
        {
            var modlist = await _hc.GetStringAsync(settings.ModListUrl);
            Directory.CreateDirectory(mo2ProfilePath);
            await File.WriteAllTextAsync(Path.Join(mo2ProfilePath, "modlist.txt"), modlist);
        }

        var mo2ProfileModListPath = Path.Join(mo2ProfilePath, "modpack_maker_list.json");
        await File.WriteAllTextAsync(
            mo2ProfileModListPath,
            JsonSerializer.Serialize(
                onlineModPackMakerRecords,
                jsonTypeInfo: ModPackMakerCtx.Default.ListModPackMakerRecord
            )
        );

        internalProgress.Reset();
    }

    private async Task ProcessAddonsAsync(
        IList<IDownloadableRecord> addons,
        ConcurrentBag<IDownloadableRecord> brokenAddons,
        CancellationToken cancellationToken = default
    ) =>
        await Parallel.ForEachAsync(
            addons,
            new ParallelOptions { MaxDegreeOfParallelism = settings.DownloadThreads },
            async (grs, _) =>
            {
                try
                {
                    await grs.DownloadAsync(cancellationToken);
                    await grs.ExtractAsync(cancellationToken);
                }
                catch (Exception)
                {
                    brokenAddons.Add(grs);
                }
            }
        );

    private readonly HttpClient _hc = hcf.CreateClient();
}
