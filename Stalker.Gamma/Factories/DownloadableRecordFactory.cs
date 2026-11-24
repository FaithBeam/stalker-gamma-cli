using System.Security.Cryptography;
using System.Text;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.GammaInstallerServices.SpecialRepos;
using Stalker.Gamma.ModDb.Models;
using Stalker.Gamma.Models;
using Stalker.Gamma.Services;
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
    ArchiveService archiveService,
    GitService gitService,
    GetCanonicalLinkFromModDbStartLink getCanonicalLinkFromModDbStartLink,
    ModDbGetAddonMetadataService modDbGetAddonMetadataService,
    ModDbService modDbService,
    StalkerGammaSettings settings
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
            archiveService,
            httpClientFactory,
            settings.UseAnomalyMirror ? settings.AnomalyMirrorUrl : null
        );

    public IDownloadableRecord CreateGammaSetupRecord(
        string gammaDir,
        string gammaSetupRepo,
        string gammaSetupBranch
    ) => new GammaSetupRepo(gammaProgress, gammaDir, gammaSetupRepo, gammaSetupBranch, gitService);

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
            gitService
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
            gitService
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
            gitService
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
            .. GroupGithubRecords(records.OfType<GithubRecord>()),
        ];

    /// <summary>
    /// Groups records that download the same url. Archives are named after the repo, so
    /// different urls from repos with the same name, e.g. another branch, tag or owner, get a
    /// suffix from their url instead of overwriting each other's archive.
    /// </summary>
    private IEnumerable<GithubRecordGroup> GroupGithubRecords(IEnumerable<GithubRecord> records)
    {
        // case-insensitive because the cache may be on a case-insensitive file system
        var archiveNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in records.GroupBy(r => r.Url).Select(g => g.ToList()))
        {
            var archiveName = group[0].ArchiveName;
            if (!archiveNames.Add(archiveName))
            {
                var urlHash = Convert.ToHexStringLower(
                    SHA256.HashData(Encoding.UTF8.GetBytes(group[0].Url))
                )[..8];
                archiveName =
                    $"{Path.GetFileNameWithoutExtension(archiveName)}-{urlHash}{Path.GetExtension(archiveName)}";
                archiveNames.Add(archiveName);
            }

            foreach (var record in group)
            {
                record.ArchiveName = archiveName;
            }
            yield return new GithubRecordGroup(gammaProgress, group);
        }
    }

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
            var outputDirName = GetOutputDirName(record);
            var instructions = ProcessInstructions(record.Instructions);
            downloadableRecord = new ModDbRecordGetMetadata(
                record.AddonName!,
                record.DlLink,
                instructions,
                outputDirName,
                gammaDir,
                archiveService,
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

            // e.g. https://github.com/{owner}/{repo}/archive/refs/heads/main.zip
            var segments = Uri.TryCreate(record.DlLink, UriKind.Absolute, out var uri)
                ? uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                : [];
            if (segments.Length < 2)
            {
                throw new DownloadableRecordFactoryException(
                    $"Invalid github link, expected https://github.com/<owner>/<repo>/...: {record.DlLink}"
                );
            }
            var archiveName = $"{segments[1]}.zip";
            var outputDirName = GetOutputDirName(record);
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
                archiveService
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
            var outputDirName = GetOutputDirName(record);
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
                archiveService,
                modDbService
            );
            return true;
        }
        return false;
    }

    /// <summary>
    /// The name of the folder in the mods folder a record is extracted to
    /// </summary>
    public static string GetOutputDirName(ModPackMakerRecord record) =>
        $"{record.Counter}- {record.AddonName} {record.Patch}";

    private static List<string> ProcessInstructions(string? instructions) =>
        string.IsNullOrWhiteSpace(instructions) || instructions == "0"
            ? []
            : instructions
                .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(y => y.TrimStart('\\').Replace('\\', Path.DirectorySeparatorChar))
                .ToList();
}

public class DownloadableRecordFactoryException(string msg) : Exception(msg);
