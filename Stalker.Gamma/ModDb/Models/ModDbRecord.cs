using System.Security.Cryptography;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Models;
using Stalker.Gamma.Utilities;
using ModDbService = Stalker.Gamma.ModDb.Services.ModDbService;

namespace Stalker.Gamma.ModDb.Models;

public class ModDbRecord(
    GammaProgress gammaProgress,
    string name,
    string url,
    string niceUrl,
    string archiveName,
    string? md5,
    string gammaDir,
    string outputDirName,
    IList<string> instructions,
    ArchiveUtility archiveUtility,
    ModDbService modDbService
) : IDownloadableRecord
{
    private readonly GammaProgress _gammaProgress = gammaProgress;
    private readonly string _gammaDir = gammaDir;
    private readonly string _outputDirName = outputDirName;
    private readonly ArchiveUtility _archiveUtility = archiveUtility;
    private readonly ModDbService _modDbService = modDbService;
    public string Name { get; } = name;
    private string Url { get; } = url;
    private string NiceUrl { get; } = niceUrl;
    public string ArchiveName { get; } = archiveName;
    private string? Md5 { get; } = md5;
    public string DownloadPath => Path.Join(_gammaDir, "downloads", ArchiveName);
    private string ExtractPath => Path.Join(_gammaDir, "mods", _outputDirName);
    private IList<string> Instructions { get; } = instructions;

    public virtual async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
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
                await _modDbService.GetModDbLinkCurl(
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

    public virtual async Task ExtractAsync(CancellationToken cancellationToken = default)
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

            await _archiveUtility.ExtractAsync(
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

    public bool Downloaded { get; set; }

    public override string ToString() =>
        $"""
            Name: {Name}
            Archive Name: {ArchiveName}
            Url: {Url}
            NiceUrl: {NiceUrl}
            Download Path: {DownloadPath}
            Extract Path: {ExtractPath}
            Md5: {Md5}
            Downloaded: {Downloaded}
            Instructions: {string.Join(", ", Instructions)}
            """;

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
}

public class ModDbRecordException(string message, Exception innerException)
    : Exception(message, innerException);
