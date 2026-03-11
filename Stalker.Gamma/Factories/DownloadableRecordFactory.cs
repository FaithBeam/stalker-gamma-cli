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

    List<IDownloadableRecord> CreateGroupedDownloadableRecords(IList<IDownloadableRecord> records);

    IDownloadableRecord CreateSkippedRecord(IDownloadableRecord record);

    IDownloadableRecord CreateSkipExtractWhenNotDownloadedRecord(IDownloadableRecord record);

    bool TryCreateGithubRecord(
        string gammaDir,
        Github record,
        out GithubRecord? downloadableRecord
    );

    bool TryCreateModDbRecord(string gammaDir, ModDb record, out ModDbRecord? downloadableRecord);
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

    public bool TryCreateGithubRecord(
        string gammaDir,
        Github record,
        out GithubRecord? downloadableRecord
    )
    {
        var outputDirName = $"{record.Index}- {record.ArchiveName}";
        downloadableRecord = new GithubRecord(
            gammaProgress,
            record.Name,
            record.DownloadUrl,
            record.NiceUrl ?? record.DownloadUrl,
            record.ArchiveName!,
            record.Md5,
            gammaDir,
            outputDirName,
            record.Instructions,
            httpClientFactory,
            archiveUtility
        );
        return true;
    }

    public bool TryCreateModDbRecord(
        string gammaDir,
        ModDb record,
        out ModDbRecord? downloadableRecord
    )
    {
        var outputDirName = $"{record.Index}- {record.ArchiveName}";
        downloadableRecord = new ModDbRecord(
            gammaProgress,
            record.Name!,
            record.DownloadUrl,
            record.NiceUrl ?? record.DownloadUrl,
            record.ArchiveName!,
            record.Md5,
            gammaDir,
            outputDirName,
            record.Instructions,
            archiveUtility,
            modDbUtility
        );
        return true;
    }
}

public class DownloadableRecordFactoryException(string msg) : Exception(msg);
