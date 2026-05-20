using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Services;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Models;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("cache")]
public class Cache(
    ILogger logger,
    CliSettings cliSettings,
    UtilitiesReady utilitiesReady,
    IGetStalkerModsFromApi getStalkerModsFromApi,
    IModListRecordFactory modListRecordFactory,
    IDownloadableRecordFactory downloadableRecordFactory,
    ProgressLoggingService progressLoggingService
)
{
    /// <summary>
    /// Check for out-of-date addons that can be pruned.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [Command("prune check")]
    public async Task PruneCheck(CancellationToken cancellationToken)
    {
        var pruneCheckLogic = new PruneCheck(
            logger,
            cliSettings,
            utilitiesReady,
            getStalkerModsFromApi,
            modListRecordFactory,
            downloadableRecordFactory,
            progressLoggingService
        );
        await pruneCheckLogic.ExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// Prune out-of-date addons.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [Command("prune apply")]
    public async Task PruneApply(CancellationToken cancellationToken)
    {
        var pruneApplyLogic = new PruneApply(
            logger,
            cliSettings,
            utilitiesReady,
            getStalkerModsFromApi,
            modListRecordFactory,
            downloadableRecordFactory,
            progressLoggingService
        );
        await pruneApplyLogic.ExecuteAsync(cancellationToken);
    }
}

public abstract class PruneLogic(
    ILogger logger,
    CliSettings cliSettings,
    UtilitiesReady utilitiesReady,
    IGetStalkerModsFromApi getStalkerModsFromApi,
    IModListRecordFactory modListRecordFactory,
    IDownloadableRecordFactory downloadableRecordFactory,
    ProgressLoggingService progressLoggingService
)
{
    protected ILogger Logger { get; } = logger;
    private CliSettings CliSettings { get; set; } = cliSettings;
    private UtilitiesReady UtilitiesReady { get; set; } = utilitiesReady;

    private IGetStalkerModsFromApi GetStalkerModsFromApi { get; set; } = getStalkerModsFromApi;

    private IModListRecordFactory ModListRecordFactory { get; set; } = modListRecordFactory;

    private IDownloadableRecordFactory DownloadableRecordFactory { get; set; } =
        downloadableRecordFactory;

    private ProgressLoggingService ProgressLoggingService { get; set; } = progressLoggingService;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        LogAndExitOnDependencyError.Check(UtilitiesReady, Logger);

        ValidateActiveProfile.Validate(Logger, CliSettings.ActiveProfile);

        try
        {
            var addonArchivesToDelete = await GetAddonsArchivesToDelete(cancellationToken);

            if (addonArchivesToDelete.Count == 0)
            {
                Logger.Information("No addons to prune");
                return;
            }

            var (maxPathLen, maxMbLen) = CalculateMaxLengths(addonArchivesToDelete);

            HandlePrunableArchives(addonArchivesToDelete, maxPathLen, maxMbLen);
        }
        catch (Exception e)
        {
            ProgressLoggingService.WriteToLogFile();
            LoggerExceptionMsg(e);
        }
        finally
        {
            ProgressLoggingService.WriteToLogFile();
        }
    }

    protected abstract void LoggerExceptionMsg(Exception e);

    protected abstract void HandlePrunableArchives(
        List<FileInfo> addonArchivesToDelete,
        int maxPathLen,
        int maxMbLen
    );

    private static (int maxPathLen, int maxMbLen) CalculateMaxLengths(
        List<FileInfo> addonArchivesToDelete
    )
    {
        var maxPathLen = addonArchivesToDelete.Max(x => x.FullName.Length);
        var maxMbLen = addonArchivesToDelete.Max(x => $"{x.Length / 1024 / 1024}mb".Length);
        return (maxPathLen, maxMbLen);
    }

    protected void LogArchiveDetails(FileInfo archive, int maxPathLen, int maxMbLen)
    {
        var mb = archive.Length / 1024 / 1024;
        var date = archive.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
        var paddedArchiveName = archive.FullName.PadRight(maxPathLen);
        var paddedMb = $"{mb}mb".PadRight(maxMbLen);
        Logger.Information(Informational, paddedArchiveName, paddedMb, date);
    }

    protected static long MbToReclaim(List<FileInfo> addonArchivesToDelete)
    {
        var mbToReclaim = addonArchivesToDelete.Sum(x => x.Length) / 1024 / 1024;
        return mbToReclaim;
    }

    private async Task<List<FileInfo>> GetAddonsArchivesToDelete(
        CancellationToken cancellationToken
    )
    {
        var onlineAddonsTxt = await GetStalkerModsFromApi.GetModsAsync(
            CliSettings.ActiveProfile!.ModPackMakerUrl,
            cancellationToken
        );
        var onlineAddons = ModListRecordFactory.Create(onlineAddonsTxt);
        var addonRecords = onlineAddons
            .Select(rec =>
                !DownloadableRecordFactory.TryCreate(
                    CliSettings.ActiveProfile.Gamma,
                    rec,
                    out var dlRec
                )
                    ? null
                    : dlRec
            )
            .Where(x => x is not null)
            .Cast<IDownloadableRecord>()
            .Select(x => x.DownloadPath)
            .Order()
            .ToList();
        var gammaDownloadsPath = Path.Join(CliSettings.ActiveProfile.Gamma, "downloads");
        var localAddons = new DirectoryInfo(gammaDownloadsPath)
            .GetFiles()
            .Where(local => _ignoreAddons.All(ignore => !local.Name.Contains(ignore)))
            .OrderBy(x => x.Name)
            .ToList();
        var addonArchivesToDelete = localAddons.ExceptBy(addonRecords, x => x.FullName).ToList();
        return addonArchivesToDelete;
    }

    private const string Informational =
        "\e[96m{File}\e[0m "
        + "\e[97m|\e[0m "
        + "\e[96m{Mb}\e[0m "
        + "\e[97m|\e[0m "
        + "\e[96m{Date}\e[0m";

    private readonly IReadOnlySet<string> _ignoreAddons = new HashSet<string>
    {
        "Anomaly-1.5.3-Full.2.7z",
        "ModOrganizer.v2.5.2.7z",
    };
}

public class PruneCheck(
    ILogger logger,
    CliSettings cliSettings,
    UtilitiesReady utilitiesReady,
    IGetStalkerModsFromApi getStalkerModsFromApi,
    IModListRecordFactory modListRecordFactory,
    IDownloadableRecordFactory downloadableRecordFactory,
    ProgressLoggingService progressLoggingService
)
    : PruneLogic(
        logger,
        cliSettings,
        utilitiesReady,
        getStalkerModsFromApi,
        modListRecordFactory,
        downloadableRecordFactory,
        progressLoggingService
    )
{
    protected override void LoggerExceptionMsg(Exception e) =>
        Logger.Error(e, "Prune check failed! {ExceptionMessage}", e.Message);

    protected override void HandlePrunableArchives(
        List<FileInfo> addonArchivesToDelete,
        int maxPathLen,
        int maxMbLen
    )
    {
        foreach (var archive in addonArchivesToDelete)
        {
            LogArchiveDetails(archive, maxPathLen, maxMbLen);
        }

        var mbToReclaim = MbToReclaim(addonArchivesToDelete);

        Logger.Information("Total size to reclaim: {Mb} MB", mbToReclaim);

        Logger.Information("Pruning check finished");
    }
}

public class PruneApply(
    ILogger logger,
    CliSettings cliSettings,
    UtilitiesReady utilitiesReady,
    IGetStalkerModsFromApi getStalkerModsFromApi,
    IModListRecordFactory modListRecordFactory,
    IDownloadableRecordFactory downloadableRecordFactory,
    ProgressLoggingService progressLoggingService
)
    : PruneLogic(
        logger,
        cliSettings,
        utilitiesReady,
        getStalkerModsFromApi,
        modListRecordFactory,
        downloadableRecordFactory,
        progressLoggingService
    )
{
    protected override void LoggerExceptionMsg(Exception e) =>
        Logger.Error(e, "Prune apply failed! {ExceptionMessage}", e.Message);

    protected override void HandlePrunableArchives(
        List<FileInfo> addonArchivesToDelete,
        int maxPathLen,
        int maxMbLen
    )
    {
        var mbToReclaim = MbToReclaim(addonArchivesToDelete);

        foreach (var archive in addonArchivesToDelete)
        {
            LogArchiveDetails(archive, maxPathLen, maxMbLen);
            archive.Delete();
        }

        Logger.Information("Reclaimed {Mb} MB", mbToReclaim);

        Logger.Information("Pruning finished");
    }
}
