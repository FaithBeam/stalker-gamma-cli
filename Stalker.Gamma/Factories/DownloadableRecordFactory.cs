using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.GammaInstallerServices.SpecialRepos;
using Stalker.Gamma.ModDb.Models;
using Stalker.Gamma.Models;
using Stalker.Gamma.Utilities;
using ModDbGetAddonMetadataService = Stalker.Gamma.ModDb.Services.ModDbGetAddonMetadataService;
using ModDbService = Stalker.Gamma.ModDb.Services.ModDbService;

namespace Stalker.Gamma.Factories;

public interface IDownloadableRecordFactory
{
    IDownloadableRecord CreateAnomalyRecord(string downloadDirectory, string anomalyDir);
    IDownloadableRecord CreateGammaSetupRecord(
        string gammaDir,
        string gammaSetupRepo,
        string gammaSetupBranch
    );
    IDownloadableRecord CreateGammaLargeFilesRecord(
        string gammaDir,
        string gammaLargeFilesRepo,
        string gammaLargeFilesBranch
    );
    IDownloadableRecord CreateStalkerGammaRecord(
        string gammaDir,
        string anomalyDir,
        string stalkerGammaRepo,
        string stalkerGammaBranch
    );
    IDownloadableRecord CreateTeivazAnomalyGunslingerRecord(
        string gammaDir,
        string teivazAnomalyGunslingerRepo,
        string teivazAnomalyGunslingerBranch
    );

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
    IHttpClientFactory httpClientFactory,
    GammaProgress gammaProgress,
    ModDbService modDbService,
    ArchiveUtility archiveUtility,
    GitUtility gitUtility,
    GetCanonicalLinkFromModDbStartLink getCanonicalLinkFromModDbStartLink,
    ModDbGetAddonMetadataService modDbGetAddonMetadataService
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
            modDbService,
            archiveUtility
        );

    public IDownloadableRecord CreateGammaSetupRecord(
        string gammaDir,
        string gammaSetupRepo,
        string gammaSetupBranch
    ) => new GammaSetupRepo(gammaProgress, gammaDir, gammaSetupRepo, gammaSetupBranch, gitUtility);

    public IDownloadableRecord CreateGammaLargeFilesRecord(
        string gammaDir,
        string gammaLargeFilesRepo,
        string gammaLargeFilesBranch
    ) =>
        new GammaLargeFilesRepo(
            gammaProgress,
            gammaDir,
            gammaLargeFilesRepo,
            gammaLargeFilesBranch,
            gitUtility
        );

    public IDownloadableRecord CreateStalkerGammaRecord(
        string gammaDir,
        string anomalyDir,
        string stalkerGammaRepo,
        string stalkerGammaBranch
    ) =>
        new StalkerGammaRepo(
            gammaProgress,
            gammaDir,
            anomalyDir,
            stalkerGammaRepo,
            stalkerGammaBranch,
            gitUtility
        );

    public IDownloadableRecord CreateTeivazAnomalyGunslingerRecord(
        string gammaDir,
        string teivazAnomalyGunslingerRepo,
        string teivazAnomalyGunslingerBranch
    ) =>
        new TeivazAnomalyGunslingerRepo(
            gammaProgress,
            gammaDir,
            teivazAnomalyGunslingerRepo,
            teivazAnomalyGunslingerBranch,
            gitUtility
        );

    public List<IDownloadableRecord> CreateGroupedDownloadableRecords(
        IList<IDownloadableRecord> records
    ) =>
        [
            .. records
                .OfType<ModDbRecord>()
                .GroupBy(r => r.ArchiveName)
                .Select(r => new ModDbRecordGroup(gammaProgress, r.ToList())),
            .. records
                .OfType<ModDbRecordGetMetadata>()
                .GroupBy(r => r.StartLink)
                .Select(r => new ModDbRecordGetMetadataGroup(gammaProgress, r.ToList())),
            .. records
                .OfType<GithubRecord>()
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

        if (TryParseModDbGetMetadataRecord(gammaDir, record, out var modDbGetMetadataRecord))
        {
            downloadableRecord = modDbGetMetadataRecord;
            return true;
        }

        if (TryParseGithubRecord(gammaDir, record, out var githubRecord))
        {
            downloadableRecord = githubRecord;
            return true;
        }

        return false;
    }

    private bool TryParseModDbGetMetadataRecord(
        string gammaDir,
        ModPackMakerRecord record,
        out ModDbRecordGetMetadata? downloadableRecord
    )
    {
        downloadableRecord = null;
        if (
            !string.IsNullOrWhiteSpace(record.AddonName)
            && string.IsNullOrWhiteSpace(record.ZipName)
            && !string.IsNullOrWhiteSpace(record.DlLink)
            && record.DlLink.Contains("moddb")
        )
        {
            var outputDirName = $"{record.Counter}- {record.AddonName} {record.Patch}";
            var instructions = ProcessInstructions(record.Instructions);
            downloadableRecord = new ModDbRecordGetMetadata(
                record.AddonName!,
                record.DlLink,
                instructions,
                outputDirName,
                gammaDir,
                archiveUtility,
                gammaProgress,
                modDbService,
                getCanonicalLinkFromModDbStartLink,
                modDbGetAddonMetadataService
            );
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
                // likely using the mod pack maker list from stalker_gamma repo with no moddb metadata
                return false;
            }

            // normal mod pack maker with moddb metadata
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
                modDbService
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
