using System.Collections.Concurrent;
using System.Text.Json;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices.SpecialRepos;
using Stalker.Gamma.Models;
using Stalker.Gamma.ModOrganizer;
using Stalker.Gamma.ModOrganizer.DownloadModOrganizer;
using Stalker.Gamma.Services;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices;

public class OfflineGammaInstaller(
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
) : GammaInstaller(
    settings,
    gammaProgress,
    downloadModOrganizerService,
    getStalkerModsFromApi,
    downloadableRecordFactory,
    modListRecordFactory,
    separatorsFactory,
    hcf,
    powerShellCmdBuilder,
    getStalkerModsFromLocal,
    preserveUserLtxSettingsService,
    preserveMcmSettings
)
{
    public override async Task InstallAsync(GammaInstallerArgs args)
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
        var teivazTask = Task.Run(
            async () =>
                await ((TeivazAnomalyGunslingerRepo)args.TeivazAnomalyGunslingerRecord!).ExpandFilesAsync(
                    args.CancellationToken
                ),
            args.CancellationToken
        );
        var gammaLargeFilesTask = Task.Run(
            async () =>
                await ((GammaLargeFilesRepo)args.GammaLargeFilesRecord!).ExpandFilesAsync(
                    args.CancellationToken
                ),
            args.CancellationToken
        );
        var gammaSetupTask = Task.Run(
            async () =>
                await ((GammaSetupRepo)args.GammaSetupRecord!).ExpandFilesAsync(
                    args.CancellationToken
                ),
            args.CancellationToken
        );
        var stalkerGammaTask = Task.Run(
            async () =>
                await ((StalkerGammaRepo)args.StalkerGammaRecord!).ExpandFilesAsync(
                    args.CancellationToken
                ),
            args.CancellationToken
        );

        await Task.WhenAll(
            mainBatch,
            teivazTask,
            gammaLargeFilesTask,
            gammaSetupTask,
            stalkerGammaTask
        );

        foreach (var brokenAddon in brokenAddons)
        {
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
        if (!string.IsNullOrWhiteSpace(args.ModListPath))
        {
            var modList = await File.ReadAllTextAsync(
                args.ModListPath,
                cancellationToken: args.CancellationToken
            );
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

    protected override async Task ProcessAddonsAsync(
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
                    if (grs.ArchiveExists())
                    {
                        await grs.ExtractAsync(cancellationToken);
                        if (minimal)
                        {
                            grs.DeleteArchive();
                        }
                    }
                }
                catch (Exception)
                {
                    brokenAddons.Add(grs);
                }
            }
        );
}
