using System.ComponentModel.DataAnnotations;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Services;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.GammaInstallerServices.GammaInstaller;
using Stalker.Gamma.Models;

namespace stalker_gamma_cli.Commands;

[RegisterCommands]
public class FullInstallCmd(
    ILogger logger,
    IGammaInstaller gammaInstaller,
    OfflineGammaInstaller offlineGammaInstaller,
    InstallCommandSupport support
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
    /// <param name="progressUpdateIntervalMs">How frequently to write progress to the console for each download, extract, etc. in milliseconds</param>
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
        [Range(CliProfile.MinDownloadThreads, CliProfile.MaxDownloadThreads)]
            int? downloadThreads = null,
        bool resume = false,
        bool json = false,
        [Hidden] long progressUpdateIntervalMs = 1000
    )
    {
        var offline = !string.IsNullOrWhiteSpace(offlineArchivePath);
        var installer = offline ? offlineGammaInstaller : gammaInstaller;
        string? gamma = null;
        ResumeState? resumeState = null;
        DirectoryInfo? offlineListsDir = null;
        return await support.RunAsync(
            ResumeCommand,
            "Install failed!",
            json,
            progressReporting: support.ReportProgress(
                installer.Progress,
                verbose,
                json,
                progressUpdateIntervalMs
            ),
            action: async () =>
            {
                try
                {
                    var profile = support.Validate(downloadThreads: downloadThreads);
                    ValidateOfflineArchivePath(offlineArchivePath);
                    if (offline && skipExtractOnHashMatch)
                    {
                        logger.Warning(
                            "--skip-extract-on-hash-match is ignored with --offline-archive-path, every archive is extracted"
                        );
                    }

                    support.ApplySettings(
                        profile,
                        downloadThreads,
                        stalkerGammaServerUrl,
                        useAnomalyMirror,
                        addFoldersToWinDefenderExclusion,
                        enableLongPaths
                    );
                    gamma = profile.Gamma;

                    if (offline)
                    {
                        offlineListsDir = Directory.CreateTempSubdirectory(
                            "stalker-gamma-offline-"
                        );
                        (modPackMakerPath, modListPath) = await ExtractOfflineArchiveAsync(
                            offlineArchivePath!,
                            profile.Cache,
                            offlineListsDir.FullName,
                            modPackMakerPath,
                            modListPath,
                            support.GetProgressWriter(verbose, json),
                            progressUpdateIntervalMs,
                            cancellationToken
                        );
                    }

                    resumeState = await ResumeUtility.LoadAsync(
                        logger,
                        resume,
                        gamma,
                        ResumeCommand,
                        cancellationToken
                    );
                    var args = GammaInstallerArgs
                        .Create(profile.Anomaly, gamma, profile.Cache)
                        .WithCancellationToken(cancellationToken)
                        .WithDownloadGithubArchives(!skipGithubDownloads)
                        .WithSkipExtractOnHashMatch(skipExtractOnHashMatch)
                        .WithMo2Profile(profile.Mo2Profile)
                        .WithMinimal(minimal)
                        .WithOffline(offline)
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
                    logger.Information("Install finished");
                }
                finally
                {
                    DeleteOfflineListsDir(offlineListsDir);
                }
            },
            onFailure: () =>
            {
                if (resumeState is not null)
                {
                    ResumeUtility.Write(logger, resumeState, gamma!);
                }
            }
        );
    }

    private static void ValidateOfflineArchivePath(string? offlineArchivePath)
    {
        if (!string.IsNullOrWhiteSpace(offlineArchivePath) && !File.Exists(offlineArchivePath))
        {
            throw new CommandValidationException(
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
        logger.Information(
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
        logger.Information(
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
            logger.Warning(e, "Unable to delete {OfflineListsDir}", dir.FullName);
        }
    }

    private const string ResumeCommand = "full-install";
}
