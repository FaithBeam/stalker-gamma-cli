using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Services;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Models;

public class GithubRecord(
    GammaProgress gammaProgress,
    string name,
    string url,
    string niceUrl,
    string archiveName,
    string? md5,
    string gammaDir,
    string outputDirName,
    IList<string> instructions,
    IHttpClientFactory hcf,
    ArchiveService archiveService
) : IDownloadableRecord
{
    public string Name { get; } = name;
    private string Url { get; } = url;
    private string NiceUrl { get; } = niceUrl;
    public string ArchiveName { get; } = archiveName;
    private string? Md5 { get; } = md5;
    public string DownloadPath => Path.Join(_gammaDir, "downloads", ArchiveName);
    private string ExtractPath => Path.Join(_gammaDir, "mods", _outputDirName);
    private IList<string> Instructions { get; } = instructions;
    private readonly HttpClient _hc = hcf.CreateClient("githubDlArchive");
    private readonly GammaProgress _gammaProgress = gammaProgress;
    private readonly string _gammaDir = gammaDir;
    private readonly string _outputDirName = outputDirName;
    private readonly ArchiveService _archiveService = archiveService;
    public bool Download { get; set; } = true;

    public async Task DownloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!Download && File.Exists(DownloadPath))
            {
                return;
            }

            await DownloadFileFast.DownloadAsync(
                _hc,
                Url,
                DownloadPath,
                onProgress: pct => OnProgress(GammaProgressType.Download, pct),
                cancellationToken: cancellationToken
            );

            OnProgress(GammaProgressType.Download, 1);
            Downloaded = true;
        }
        catch (Exception e)
        {
            throw new GithubRecordException(
                $"""
                Error downloading github record
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
            throw new GithubRecordException(
                $"""
                Error extracting github record
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
            Download: {Download}
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

public class GithubRecordException(string message, Exception innerException)
    : Exception(message, innerException);
