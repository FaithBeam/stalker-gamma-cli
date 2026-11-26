using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Stalker.Gamma.Extensions;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices.SpecialRepos;
using Stalker.Gamma.ModDb.Models;
using Stalker.Gamma.ModDb.Services;
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
    StalkerGammaServerProxy stalkerGammaServerProxy
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
        return downloadableRecordFactory
            .CreateGroupedDownloadableRecords(addonRecords)
            .Select(dlRec => CreateResumableRecord(args, dlRec, args.SkipExtractOnHashMatch))
            .Shuffle() // randomize the order addons are downloaded
            .ToList();
    }

    /// <summary>
    /// Wraps a record so its status is tracked in <see cref="GammaInstallerArgs.ResumeState"/>
    /// </summary>
    private IDownloadableRecord CreateResumableRecord(
        GammaInstallerArgs args,
        IDownloadableRecord record,
        bool skipExtractOnHashMatch
    )
    {
        var key = record.ResumeKey;
        // a record that was downloaded but not extracted before the previous run died must be extracted,
        // even if this run doesn't need to download it again.
        // offline installs never download, so skipping records that weren't downloaded would skip all of them
        var inner =
            skipExtractOnHashMatch
            && !args.Offline
            && args.ResumeState.GetStatus(key) != ResumeStatus.Extract
                ? downloadableRecordFactory.CreateSkipExtractWhenNotDownloadedRecord(record)
                : record;
        args.ResumeState.Register(key);
        return new ResumableRecord(gammaProgress, inner, key, args.ResumeState);
    }

    public void BuildSpecialRepoRecords(GammaInstallerArgs args)
    {
        CreateSpecialRepoRecords(args);
        // every file is extracted, so the commit applied by a previous update is stale.
        // the next update falls back to the commit the repo is at before it fetches, which is the one extracted here
        foreach (
            var record in new[]
            {
                args.GammaLargeFilesRecord!,
                args.TeivazAnomalyGunslingerRecord!,
                args.GammaSetupRecord!,
                args.StalkerGammaRecord!,
            }
        )
        {
            if (File.Exists(UpdateSpecialRepoRecord.GetAppliedCommitPath(record)))
            {
                File.Delete(UpdateSpecialRepoRecord.GetAppliedCommitPath(record));
            }
        }
    }

    private void CreateSpecialRepoRecords(GammaInstallerArgs args)
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
        args.ResumeState.Register(args.GammaLargeFilesRecord.ResumeKey);
        args.ResumeState.Register(args.TeivazAnomalyGunslingerRecord.ResumeKey);
        args.ResumeState.Register(args.GammaSetupRecord.ResumeKey);
        args.ResumeState.Register(args.StalkerGammaRecord.ResumeKey);
    }

    /// <summary>
    /// Builds special repo records that only extract into the addon folders in
    /// <see cref="GammaInstallerArgs.UpdateOutputDirNames"/> and the files that changed since the last update
    /// </summary>
    public void BuildUpdateSpecialRepoRecords(GammaInstallerArgs args)
    {
        CreateSpecialRepoRecords(args);
        args.GammaLargeFilesRecord = new UpdateSpecialRepoRecord(
            (ISpecialRepo)args.GammaLargeFilesRecord!,
            args.UpdateOutputDirNames
        );
        args.TeivazAnomalyGunslingerRecord = new UpdateSpecialRepoRecord(
            (ISpecialRepo)args.TeivazAnomalyGunslingerRecord!,
            args.UpdateOutputDirNames
        );
        args.GammaSetupRecord = new UpdateSpecialRepoRecord(
            (ISpecialRepo)args.GammaSetupRecord!,
            args.UpdateOutputDirNames
        );
        args.StalkerGammaRecord = new UpdateSpecialRepoRecord(
            (ISpecialRepo)args.StalkerGammaRecord!,
            args.UpdateOutputDirNames
        );
    }

    /// <summary>
    /// Downloads and expands a special repo unless a previous run already completed it
    /// </summary>
    protected static async Task DownloadSpecialRepoAsync(
        GammaInstallerArgs args,
        IDownloadableRecord record,
        Func<Task> downloadAsync
    )
    {
        if (args.ResumeState.IsComplete(record.ResumeKey))
        {
            return;
        }
        await downloadAsync();
        args.ResumeState.SetStatus(record.ResumeKey, ResumeStatus.Extract);
    }

    /// <summary>
    /// Extracts a special repo unless a previous run already completed it
    /// </summary>
    protected async Task ExtractSpecialRepoAsync(
        GammaInstallerArgs args,
        IDownloadableRecord record
    )
    {
        if (args.ResumeState.IsComplete(record.ResumeKey))
        {
            gammaProgress.IncrementCompletedMods();
            return;
        }
        await record.ExtractAsync(args.CancellationToken);
        args.ResumeState.SetStatus(record.ResumeKey, ResumeStatus.Complete);
    }

    public async Task InstallAsync(GammaInstallerArgs args)
    {
        await WaitForStalkerGammaServerAsync(args);

        args.Mo2Version ??= ModOrganizerRelease.Version;
        args.Cache = ToFullPath(args.Cache);
        args.Gamma = ToFullPath(args.Gamma);
        args.Anomaly = ToFullPath(args.Anomaly);

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

        IDownloadableRecord[] specialRepos =
        [
            args.TeivazAnomalyGunslingerRecord!,
            args.GammaLargeFilesRecord!,
            args.GammaSetupRecord!,
            args.StalkerGammaRecord!,
        ];
        gammaProgress.TotalMods =
            args.GroupedAddonRecords.Count
            + specialRepos.Length
            + (args.AnomalyRecord is not null ? 1 : 0);

        foreach (var separator in separators)
        {
            await separator.WriteAsync(args.Gamma);
        }

        IList<IDownloadableRecord> mainBatchRecords = args.AnomalyRecord is not null
            ? [args.AnomalyRecord, .. args.GroupedAddonRecords]
            : [.. args.GroupedAddonRecords];

        ConcurrentBag<IDownloadableRecord> brokenAddons = [];
        await RunBatchAsync(
            args.CancellationToken,
            [
                ct => ProcessAddonsAsync(mainBatchRecords, brokenAddons, args.Minimal, ct),
                .. specialRepos.Select<IDownloadableRecord, Func<CancellationToken, Task>>(record =>
                    ct =>
                        DownloadSpecialRepoAsync(
                            args,
                            record,
                            () => PrepareSpecialRepoAsync((ISpecialRepo)record, ct)
                        )
                ),
            ]
        );

        await RetryBrokenAddonsAsync(brokenAddons, args.Minimal, args.CancellationToken);

        await ExtractSpecialRepoAsync(args, args.GammaSetupRecord!);
        await ExtractSpecialRepoAsync(args, args.GammaLargeFilesRecord!);
        await ExtractSpecialRepoAsync(args, args.TeivazAnomalyGunslingerRecord!);
        await ExtractSpecialRepoAsync(args, args.StalkerGammaRecord!);
        if (args.Minimal)
        {
            foreach (var specialRepo in specialRepos)
            {
                specialRepo.DeleteArchive();
            }
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

        await InstallModOrganizerAsync(args);
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
        if (await GetModListAsync(args) is { } modList)
        {
            await File.WriteAllTextAsync(
                Path.Join(mo2ProfilePath, "modlist.txt"),
                modList,
                args.CancellationToken
            );
        }

        await File.WriteAllTextAsync(
            Path.Join(mo2ProfilePath, "modpack_maker_list.txt"),
            modpackMakerTxt,
            args.CancellationToken
        );
        await File.WriteAllTextAsync(
            Path.Join(mo2ProfilePath, "modpack_maker_list.json"),
            JsonSerializer.Serialize(
                modpackMakerRecords,
                jsonTypeInfo: ModPackMakerCtx.Default.ListModPackMakerRecord
            ),
            args.CancellationToken
        );

        gammaProgress.Reset();
    }

    private static string ToFullPath(string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(path);

    /// <summary>
    /// Runs the work concurrently. The first failure cancels the rest, so a broken special repo or
    /// a Cloudflare challenge stops the install right away instead of after every addon downloads.
    /// </summary>
    private static async Task RunBatchAsync(
        CancellationToken cancellationToken,
        IEnumerable<Func<CancellationToken, Task>> work
    )
    {
        using var batchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ExceptionDispatchInfo? batchFailure = null;

        var tasks = work.Select(w =>
                Task.Run(
                    async () =>
                    {
                        try
                        {
                            await w(batchCts.Token);
                        }
                        catch (Exception e) when (!batchCts.IsCancellationRequested)
                        {
                            Interlocked.CompareExchange(
                                ref batchFailure,
                                ExceptionDispatchInfo.Capture(e),
                                null
                            );
                            await batchCts.CancelAsync();
                            throw;
                        }
                    },
                    batchCts.Token
                )
            )
            .ToList();
        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // the rest of the batch fails with cancellations, or errors caused by being cancelled,
            // so report the failure that cancelled it
            batchFailure?.Throw();
            throw;
        }
    }

    /// <summary>
    /// Retries the addons that failed while the others were installing. Every one is retried
    /// before the failures are reported, so one run finds all the addons that still fail.
    /// </summary>
    /// <exception cref="AddonsFailedException">Some addons failed again</exception>
    internal static async Task RetryBrokenAddonsAsync(
        IEnumerable<IDownloadableRecord> brokenAddons,
        bool minimal,
        CancellationToken cancellationToken
    )
    {
        List<(string Name, Exception Error)> failures = [];
        foreach (var brokenAddon in brokenAddons)
        {
            try
            {
                await brokenAddon.DownloadAsync(cancellationToken);
                await brokenAddon.ExtractAsync(cancellationToken);
                if (minimal)
                {
                    brokenAddon.DeleteArchive();
                }
            }
            // a challenge fails every ModDB addon the same way, so there's no point going on
            catch (Exception e)
                when (e is not CloudflareChallengeException and not OperationCanceledException)
            {
                failures.Add((brokenAddon.Name, e));
            }
        }

        if (failures.Count > 0)
        {
            throw new AddonsFailedException(failures);
        }
    }

    /// <summary>
    /// Waits for the experimental stalker gamma server when ModDB requests go through it
    /// </summary>
    /// <exception cref="TimeoutException">It isn't ready within <see cref="ServerReadyTimeout"/></exception>
    protected virtual async Task WaitForStalkerGammaServerAsync(GammaInstallerArgs args)
    {
        if (args is { StalkerGammaServerUrl: not null, ExperimentalPythonServerSettings: not null })
        {
            await WaitUntilReadyAsync(
                stalkerGammaServerProxy.Ready,
                args.StalkerGammaServerUrl,
                ServerReadyTimeout,
                TimeSpan.FromSeconds(1),
                args.CancellationToken
            );
        }
    }

    /// <summary>
    /// Polls <paramref name="ready"/> until it's true
    /// </summary>
    /// <exception cref="TimeoutException">It isn't true within <paramref name="timeout"/></exception>
    internal static async Task WaitUntilReadyAsync(
        Func<Task<bool>> ready,
        string serverUrl,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken
    )
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            while (!await ready())
            {
                await Task.Delay(pollInterval, timeoutCts.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The stalker gamma server at {serverUrl} wasn't ready after {timeout.TotalSeconds:0.###} seconds. Check that it started without errors."
            );
        }
    }

    /// <summary>
    /// The server starts a browser, which can take a while on a slow machine
    /// </summary>
    private static readonly TimeSpan ServerReadyTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Gets a special repo's files ready to be extracted
    /// </summary>
    protected virtual async Task PrepareSpecialRepoAsync(
        ISpecialRepo record,
        CancellationToken cancellationToken
    )
    {
        await record.DownloadAsync(cancellationToken);
        await record.ExpandFilesAsync(cancellationToken);
    }

    protected virtual async Task InstallModOrganizerAsync(GammaInstallerArgs args)
    {
        await DownloadModOrganizerService.DownloadAsync(
            cachePath: args.Cache,
            extractPath: args.Gamma,
            version: args.Mo2Version!,
            cancellationToken: args.CancellationToken
        );
        await DownloadModOrganizerService.ExtractAsync(
            version: args.Mo2Version!,
            cachePath: args.Cache,
            extractPath: args.Gamma,
            cancellationToken: args.CancellationToken
        );
    }

    /// <summary>
    /// The modlist.txt to install, from --mod-list-path or else the profile's url, or null to keep
    /// the one the GAMMA profile came with
    /// </summary>
    protected virtual async Task<string?> GetModListAsync(GammaInstallerArgs args) =>
        !string.IsNullOrWhiteSpace(args.ModListPath)
            ? await File.ReadAllTextAsync(args.ModListPath, args.CancellationToken)
        : !string.IsNullOrWhiteSpace(Settings.ModListUrl)
            ? await _hc.GetStringAsync(Settings.ModListUrl, args.CancellationToken)
        : null;

    protected async Task<string> GetModpackMakerTxt(GammaInstallerArgs args)
    {
        return string.IsNullOrWhiteSpace(args.ModPackMakerPath)
                ? await getStalkerModsFromApi.GetModsAsync(args.CancellationToken)
            : File.Exists(args.ModPackMakerPath)
                ? await File.ReadAllTextAsync(args.ModPackMakerPath, args.CancellationToken)
            : throw new FileNotFoundException(
                $"{nameof(args.ModPackMakerPath)} file not found: {args.ModPackMakerPath}"
            );
    }

    public class DiffedAddonRecords
    {
        public required List<ModPackMakerRecord> OnlineRecords { get; set; }
        public required List<ModPackMakerRecord> AddedOrModifiedRecords { get; set; }
        public required List<ModPackMakerRecord> LocalRecords { get; set; }

        /// <summary>
        /// The local records of addons that moved to another line in the online list, so their
        /// addon folder is renamed
        /// </summary>
        public List<ModPackMakerRecord> RenumberedRecords { get; set; } = [];
    }

    public async Task<DiffedAddonRecords> DiffAddonRecordsAsync(GammaInstallerArgs args)
    {
        var modpackMakerTxt = await getStalkerModsFromApi.GetModsAsync(args.CancellationToken);
        var onlineModPackMakerRecords = ModListRecordFactory.Create(modpackMakerTxt);
        var localRecords = await getStalkerModsFromLocal.GetMods(args.Gamma, args.Mo2Profile);
        var diffs = localRecords.Diff(onlineModPackMakerRecords);
        var addedOrModifiedRecords = diffs
            .Where(x => x.DiffType is DiffType.Added or DiffType.Modified)
            .Select(x => x.NewListRecord!)
            .ToList();
        var renumberedRecords = diffs
            .Where(x =>
                x.DiffType is DiffType.Modified
                && DiffModPackMakerLists.IsRenumbered(x.OldListRecord!, x.NewListRecord!)
            )
            .Select(x => x.OldListRecord!)
            .ToList();
        return new DiffedAddonRecords
        {
            LocalRecords = localRecords,
            OnlineRecords = onlineModPackMakerRecords,
            AddedOrModifiedRecords = addedOrModifiedRecords,
            RenumberedRecords = renumberedRecords,
        };
    }

    public async Task<IList<IDownloadableRecord>> BuildUpdateGroupedAddonRecordsAsync(
        GammaInstallerArgs args
    )
    {
        var diffedAddonRecords = await DiffAddonRecordsAsync(args);
        DeleteRenumberedAddonFolders(args, diffedAddonRecords);
        var addonRecords = diffedAddonRecords
            .AddedOrModifiedRecords.Select(rec =>
                downloadableRecordFactory.TryCreate(args.Gamma, rec, out var dlRec) ? dlRec : null
            )
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
        args.UpdateOutputDirNames = addonRecords
            .Select(rec =>
                rec switch
                {
                    ModDbRecord mdr => mdr.OutputDirName,
                    ModDbRecordGetMetadata mdrgm => mdrgm.OutputDirName,
                    GithubRecord ghr => ghr.OutputDirName,
                    _ => null,
                }
            )
            .Where(x => x is not null)
            .Select(x => x!)
            .ToHashSet();
        return downloadableRecordFactory
            .CreateGroupedDownloadableRecords(addonRecords)
            .Select(dlRec => CreateResumableRecord(args, dlRec, skipExtractOnHashMatch: false))
            .ToList();
    }

    /// <summary>
    /// Renumbered addons are extracted to a folder with their new number, so the folder with
    /// their old number would be left behind, unused by the new modlist.txt
    /// </summary>
    private static void DeleteRenumberedAddonFolders(
        GammaInstallerArgs args,
        DiffedAddonRecords diffedAddonRecords
    )
    {
        var onlineDirNames = diffedAddonRecords
            .OnlineRecords.Select(DownloadableRecordFactory.GetOutputDirName)
            .ToHashSet();
        foreach (var renumbered in diffedAddonRecords.RenumberedRecords)
        {
            var oldDirName = DownloadableRecordFactory.GetOutputDirName(renumbered);
            // another addon may now be extracted to this folder
            if (onlineDirNames.Contains(oldDirName))
            {
                continue;
            }

            var oldDir = Path.Join(args.Gamma, "mods", oldDirName);
            if (Directory.Exists(oldDir))
            {
                DirUtils.NormalizePermissions(oldDir);
                Directory.Delete(oldDir, true);
            }
        }
    }

    public IDownloadableRecord BuildAnomalyRecord(GammaInstallerArgs args)
    {
        var anomalyRecord = downloadableRecordFactory.CreateAnomalyRecord(
            Path.Join(args.Gamma, "downloads"),
            args.Anomaly
        );
        return CreateResumableRecord(args, anomalyRecord, args.SkipExtractOnHashMatch);
    }

    protected virtual async Task ProcessAddonsAsync(
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
                try
                {
                    await grs.DownloadAsync(ct);
                    await grs.ExtractAsync(ct);
                    if (minimal)
                    {
                        grs.DeleteArchive();
                    }
                }
                catch (Exception e)
                    when (e is not CloudflareChallengeException and not OperationCanceledException)
                {
                    gammaProgress.OnWarning(
                        new GammaProgress.GammaInstallWarningEventArgs
                        {
                            Message =
                                $"{grs.Name} failed, retrying after the other addons finish: {e.Message}",
                            Exception = e,
                        }
                    );
                    brokenAddons.Add(grs);
                }
            }
        );

    public void Dispose()
    {
        _hc.Dispose();
    }
}

/// <summary>
/// Addons failed again when they were retried after the others finished
/// </summary>
public class AddonsFailedException : AggregateException
{
    public AddonsFailedException(IReadOnlyList<(string Name, Exception Error)> failures)
        : base(failures.Select(x => x.Error))
    {
        AddonNames = failures.Select(x => x.Name).ToList();
        _message =
            $"{failures.Count} addon{(failures.Count == 1 ? "" : "s")} failed after retrying:"
            + string.Concat(
                failures.Select(x => $"{Environment.NewLine}  {x.Name}: {x.Error.Message}")
            );
    }

    public IReadOnlyList<string> AddonNames { get; }

    // AggregateException's message would repeat every inner message without the addon names
    public override string Message => _message;

    private readonly string _message;
}
