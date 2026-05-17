using System.Security.Cryptography;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Models;
using Stalker.Gamma.Services;
using Stalker.Gamma.Utilities;
using ModDbGetAddonMetadataService = Stalker.Gamma.ModDb.Services.ModDbGetAddonMetadataService;
using ModDbService = Stalker.Gamma.ModDb.Services.ModDbService;

namespace Stalker.Gamma.ModDb.Models;

public class ModDbRecordGetMetadata(
    string name,
    string startLink,
    List<string> instructions,
    string outputDirName,
    string gammaDir,
    ArchiveService archiveService,
    GammaProgress gammaProgress,
    ModDbService modDbService,
    GetCanonicalLinkFromModDbStartLink getCanonicalLinkFromModDbStartLink,
    ModDbGetAddonMetadataService modDbGetAddonMetadataService
) : IDownloadableRecord
{
    public string Name { get; } = name;
    public string ArchiveName { get; set; } = null!;
    public string DownloadPath => Path.Join(_gammaDir, "downloads", ArchiveName);
    public string ExtractPath => Path.Join(_gammaDir, "mods", OutputDirName);
    public string StartLink { get; set; } = startLink;
    public string NiceUrl { get; set; } = null!;
    private List<string> Instructions { get; } = instructions;
    private string OutputDirName { get; } = outputDirName;
    public string Url => StartLink;
    public string Md5 { get; set; } = null!;

    public async Task DownloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await GetModDbAddonMetadataAsync(cancellationToken);
            if (
                Path.Exists(DownloadPath)
                    && !string.IsNullOrWhiteSpace(Md5)
                    && await HashUtils.HashFile(
                        DownloadPath,
                        HashAlgorithmName.MD5,
                        pct => OnProgress(GammaProgressType.CheckMd5, pct),
                        cancellationToken
                    ) != Md5
                || !Path.Exists(DownloadPath)
            )
            {
                await _modDbService.DownloadAddonAsync(
                    Url,
                    DownloadPath,
                    pct => OnProgress(GammaProgressType.Download, pct),
                    cancellationToken: cancellationToken
                );
                Downloaded = true;
            }
        }
        catch (Exception e)
        {
            throw new ModDbRecordException(
                $"""
                Error downloading ModDb record
                {ToString()}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    public async Task ExtractAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Delete what was previously extracted
            if (Directory.Exists(ExtractPath))
            {
                DirUtils.NormalizePermissions(ExtractPath);
                DirUtils.RecursivelyDeleteDirectory(ExtractPath, doNotMatch: []);
            }

            Directory.CreateDirectory(ExtractPath);

            await _archiveService.ExtractAsync(
                DownloadPath,
                ExtractPath,
                pct => OnProgress(GammaProgressType.Extract, pct),
                ct: cancellationToken
            );

            ProcessInstructions.Process(ExtractPath, Instructions, cancellationToken);

            CleanExtractPath.Clean(ExtractPath);

            WriteAddonMetaIni.Write(ExtractPath, ArchiveName, NiceUrl);
        }
        catch (Exception e)
        {
            throw new ModDbRecordException(
                $"""
                Error extracting ModDb record
                {ToString()}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    private async Task GetModDbAddonMetadataAsync(CancellationToken cancellationToken)
    {
        var canonicalLink = await _getCanonicalLinkFromModDbStartLink.GetCanonicalLinkAsync(
            StartLink,
            cancellationToken
        );
        var metadata = await _modDbGetAddonMetadataService.GetAsync(
            canonicalLink,
            cancellationToken
        );
        ArchiveName = metadata.Filename;
        Md5 = metadata.Md5Hash;
        NiceUrl = canonicalLink;
    }

    public bool Downloaded { get; set; }

    private void OnProgress(GammaProgressType operation, double pct) =>
        _gammaProgress.OnProgressChanged(ProgFunc(operation, pct));

    private GammaProgress.GammaInstallProgressEventArgs ProgFunc(
        GammaProgressType operation,
        double pct
    ) =>
        new()
        {
            Name = Name,
            ProgressType = operation,
            Progress = pct,
            Url = Url,
            ArchiveName = ArchiveName,
            DownloadPath = DownloadPath,
            ExtractPath = ExtractPath,
        };

    private readonly GammaProgress _gammaProgress = gammaProgress;
    private readonly string _gammaDir = gammaDir;
    private readonly ArchiveService _archiveService = archiveService;
    private readonly ModDbService _modDbService = modDbService;
    private readonly GetCanonicalLinkFromModDbStartLink _getCanonicalLinkFromModDbStartLink =
        getCanonicalLinkFromModDbStartLink;
    private readonly ModDbGetAddonMetadataService _modDbGetAddonMetadataService =
        modDbGetAddonMetadataService;
}
