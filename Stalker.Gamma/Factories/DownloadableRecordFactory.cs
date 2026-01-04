using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.GammaInstallerServices.SpecialRepos;
using Stalker.Gamma.Models;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Factories;

public interface IDownloadableRecordFactory
{
    IDownloadableRecord CreateAnomalyRecord(string downloadDirectory, string anomalyDir);
    IDownloadableRecord CreateGammaSetupRecord(string gammaDir, string anomalyDir);
    IDownloadableRecord CreateGammaLargeFilesRecord(string gammaDir);
    IDownloadableRecord CreateStalkerGammaRecord(string gammaDir, string anomalyDir);
    IDownloadableRecord CreateTeivazAnomalyGunslingerRecord(string gammaDir);

    bool TryCreate(
        string gammaDir,
        ModPackMakerRecord record,
        out IDownloadableRecord? downloadableRecord
    );

    List<IDownloadableRecord> CreateGroupedDownloadableRecords(IList<IDownloadableRecord> records);

    IDownloadableRecord CreateSkippedRecord(IDownloadableRecord record);

    IDownloadableRecord CreateSkipExtractWhenNotDownloadedRecord(IDownloadableRecord record);
}

public class DownloadableRecordFactory(
    StalkerGammaSettings stalkerGammaSettings,
    IHttpClientFactory httpClientFactory,
    GammaProgress gammaProgress,
    ModDbUtility modDbUtility,
    ArchiveUtility archiveUtility,
    GitUtility gitUtility
) : IDownloadableRecordFactory
{
    public IDownloadableRecord CreateSkippedRecord(IDownloadableRecord record) =>
        new SkippedRecord(gammaProgress, record);

    public IDownloadableRecord CreateSkipExtractWhenNotDownloadedRecord(
        IDownloadableRecord record
    ) => new SkipExtractWhenNotDownloadedRecord(gammaProgress, record);

    public IDownloadableRecord CreateAnomalyRecord(string downloadDirectory, string anomalyDir) =>
        new AnomalyInstaller(
            gammaProgress,
            downloadDirectory,
            anomalyDir,
            modDbUtility,
            archiveUtility
        );

    public IDownloadableRecord CreateGammaSetupRecord(string gammaDir, string anomalyDir) =>
        new GammaSetupRepo(
            gammaProgress,
            gammaDir,
            stalkerGammaSettings.GammaSetupRepo,
            gitUtility
        );

    public IDownloadableRecord CreateGammaLargeFilesRecord(string gammaDir) =>
        new GammaLargeFilesRepo(
            gammaProgress,
            gammaDir,
            stalkerGammaSettings.GammaLargeFilesRepo,
            gitUtility
        );

    public IDownloadableRecord CreateStalkerGammaRecord(string gammaDir, string anomalyDir) =>
        new StalkerGammaRepo(
            gammaProgress,
            gammaDir,
            anomalyDir,
            stalkerGammaSettings.StalkerGammaRepo,
            gitUtility
        );

    public IDownloadableRecord CreateTeivazAnomalyGunslingerRecord(string gammaDir) =>
        new TeivazAnomalyGunslingerRepo(
            gammaProgress,
            gammaDir,
            stalkerGammaSettings.TeivazAnomalyGunslingerRepo,
            gitUtility
        );

    public List<IDownloadableRecord> CreateGroupedDownloadableRecords(
        IList<IDownloadableRecord> records
    ) =>
        [
            .. records
                .Where(r => r is ModDbRecord)
                .Cast<ModDbRecord>()
                .GroupBy(r => r.ArchiveName)
                .Select(r => new ModDbRecordGroup(gammaProgress, r.ToList())),
            .. records
                .Where(r => r is GithubRecord)
                .Cast<GithubRecord>()
                .GroupBy(r => r.ArchiveName)
                .Select(r => new GithubRecordGroup(gammaProgress, r.ToList())),
        ];

    public bool TryCreate(
        string gammaDir,
        ModPackMakerRecord record,
        out IDownloadableRecord? downloadableRecord
    )
    {
        downloadableRecord = null;

        if (TryParseModDbRecord(gammaDir, record, out var modDbRecord))
        {
            downloadableRecord = modDbRecord;
            return true;
        }

        if (TryParseGithubRecord(gammaDir, record, out var githubRecord))
        {
            downloadableRecord = githubRecord;
            return true;
        }

        return false;
    }

    private bool TryParseGithubRecord(
        string gammaDir,
        ModPackMakerRecord record,
        out GithubRecord? downloadableRecord
    )
    {
        downloadableRecord = null;

        if (record.DlLink.Contains("github"))
        {
            if (
                string.IsNullOrWhiteSpace(record.AddonName)
                || string.IsNullOrWhiteSpace(record.Patch)
            )
            {
                throw new DownloadableRecordFactoryException($"Invalid record: {record}");
            }

            var instructions = ProcessInstructions(record.Instructions);

            var archiveName = $"{record.DlLink.Split('/')[4]}.zip";
            var outputDirName = $"{record.Counter}- {record.AddonName} {record.Patch}";
            downloadableRecord = new GithubRecord(
                gammaProgress,
                record.AddonName,
                record.DlLink,
                record.ModDbUrl ?? record.DlLink,
                archiveName,
                record.Md5ModDb,
                gammaDir,
                outputDirName,
                instructions,
                httpClientFactory,
                archiveUtility
            );
            return true;
        }

        return false;
    }

    private bool TryParseModDbRecord(
        string gammaDir,
        ModPackMakerRecord record,
        out ModDbRecord? downloadableRecord
    )
    {
        downloadableRecord = null;
        if (record.DlLink.Contains("moddb"))
        {
            if (
                string.IsNullOrWhiteSpace(record.AddonName)
                || string.IsNullOrWhiteSpace(record.Patch)
                || string.IsNullOrWhiteSpace(record.ZipName)
            )
            {
                throw new DownloadableRecordFactoryException($"Invalid record: {record}");
            }
            var outputDirName = $"{record.Counter}- {record.AddonName} {record.Patch}";
            var instructions = ProcessInstructions(record.Instructions);
            downloadableRecord = new ModDbRecord(
                gammaProgress,
                record.AddonName,
                record.DlLink,
                record.ModDbUrl ?? record.DlLink,
                record.ZipName,
                record.Md5ModDb,
                gammaDir,
                outputDirName,
                instructions,
                archiveUtility,
                modDbUtility
            );
            return true;
        }
        return false;
    }

    private static List<string> ProcessInstructions(string? instructions) =>
        string.IsNullOrWhiteSpace(instructions) || instructions == "0"
            ? []
            : instructions
                .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(y => y.TrimStart('\\').Replace('\\', Path.DirectorySeparatorChar))
                .ToList();
}

public class DownloadableRecordFactoryException(string msg) : Exception(msg);
