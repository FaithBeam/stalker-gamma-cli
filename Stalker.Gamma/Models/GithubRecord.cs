using Stalker.Gamma.GammaInstallerServices;
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
    ArchiveUtility archiveUtility
) : IDownloadableRecord
{
    public string Name { get; } = name;
    private string Url { get; } = url;
    private string NiceUrl { get; } = niceUrl;
    public string ArchiveName { get; } = archiveName;
    private string? Md5 { get; } = md5;
    public string DownloadPath => Path.Join(gammaDir, "downloads", ArchiveName);
    private string ExtractPath => Path.Join(gammaDir, "mods", outputDirName);
    private IList<string> Instructions { get; } = instructions;
    private readonly HttpClient _hc = hcf.CreateClient("githubDlArchive");
    public bool Download { get; set; } = true;

    public async Task DownloadAsync(CancellationToken cancellationToken)
    {
        if (!Download && File.Exists(DownloadPath))
        {
            return;
        }

        await DownloadFileFast.DownloadAsync(
            _hc,
            Url,
            DownloadPath,
            onProgress: pct =>
                gammaProgress.OnProgressChanged(
                    new GammaProgress.GammaInstallProgressEventArgs(Name, "Download", pct, Url)
                ),
            cancellationToken: cancellationToken
        );

        gammaProgress.OnProgressChanged(
            new GammaProgress.GammaInstallProgressEventArgs(Name, "Download", 1, Url)
        );
        Downloaded = true;
    }

    public async Task ExtractAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ExtractPath);

        await archiveUtility.ExtractAsync(
            DownloadPath,
            ExtractPath,
            pct =>
                gammaProgress.OnProgressChanged(
                    new GammaProgress.GammaInstallProgressEventArgs(Name, "Extract", pct, Url)
                ),
            ct: cancellationToken
        );

        ProcessInstructions.Process(ExtractPath, Instructions);

        CleanExtractPath.Clean(ExtractPath);

        WriteAddonMetaIni.Write(ExtractPath, ArchiveName, NiceUrl);
    }

    public bool Downloaded { get; set; }
}
