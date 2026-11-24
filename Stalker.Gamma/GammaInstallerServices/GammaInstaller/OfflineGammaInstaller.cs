using System.Collections.Concurrent;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices.SpecialRepos;
using Stalker.Gamma.Models;
using Stalker.Gamma.ModOrganizer.DownloadModOrganizer;
using Stalker.Gamma.Proxies;
using Stalker.Gamma.Services;

namespace Stalker.Gamma.GammaInstallerServices.GammaInstaller;

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
    PreserveMcmSettings preserveMcmSettings,
    StalkerGammaServerProxy stalkerGammaServerProxy
)
    : GammaInstaller(
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
        preserveMcmSettings,
        stalkerGammaServerProxy
    )
{
    /// <summary>
    /// Nothing is downloaded, so ModDB isn't reached through the server
    /// </summary>
    protected override Task WaitForStalkerGammaServerAsync(GammaInstallerArgs args) =>
        Task.CompletedTask;

    /// <summary>
    /// The repo is already in the cache
    /// </summary>
    protected override async Task PrepareSpecialRepoAsync(
        ISpecialRepo record,
        CancellationToken cancellationToken
    ) => await record.ExpandFilesAsync(cancellationToken);

    /// <summary>
    /// Mod Organizer's archive is already in the cache
    /// </summary>
    protected override async Task InstallModOrganizerAsync(GammaInstallerArgs args) =>
        await DownloadModOrganizerService.ExtractAsync(
            version: args.Mo2Version!,
            cachePath: args.Cache,
            extractPath: args.Gamma,
            cancellationToken: args.CancellationToken
        );

    /// <summary>
    /// Only the modlist.txt from the offline archive or --mod-list-path, never the profile's url
    /// </summary>
    protected override async Task<string?> GetModListAsync(GammaInstallerArgs args) =>
        !string.IsNullOrWhiteSpace(args.ModListPath)
            ? await File.ReadAllTextAsync(args.ModListPath, args.CancellationToken)
            : null;

    protected override async Task ProcessAddonsAsync(
        IList<IDownloadableRecord> addons,
        ConcurrentBag<IDownloadableRecord> brokenAddons,
        bool minimal = false,
        CancellationToken cancellationToken = default
    ) =>
        await Parallel.ForEachAsync(
            addons,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Settings.DownloadThreads,
                CancellationToken = cancellationToken,
            },
            async (grs, ct) =>
            {
                if (!grs.ArchiveExists())
                {
                    ((GammaProgress)Progress).OnWarning(
                        new GammaProgress.GammaInstallWarningEventArgs
                        {
                            Message =
                                $"{grs.Name} was not installed, its archive is not in the cache: {grs.DownloadPath}",
                        }
                    );
                    return;
                }

                await grs.ExtractAsync(ct);
                if (minimal)
                {
                    grs.DeleteArchive();
                }
            }
        );
}
