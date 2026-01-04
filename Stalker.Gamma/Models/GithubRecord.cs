using System.Buffers;
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
        const int bufferSize = 1024 * 1024;

        if (!Download && File.Exists(DownloadPath))
        {
            return;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            using var response = await _hc.GetAsync(
                Url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;

            await using var fs = new FileStream(
                DownloadPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: bufferSize
            );
            await using var contentStream = await response.Content.ReadAsStreamAsync(
                cancellationToken
            );

            long totalBytesRead = 0;
            int bytesRead;

            while (
                (
                    bytesRead = await contentStream.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken
                    )
                ) > 0
            )
            {
                await fs.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                if (totalBytes.HasValue)
                {
                    var progressPercentage = (double)totalBytesRead / totalBytes.Value;
                    gammaProgress.OnProgressChanged(
                        new GammaProgress.GammaInstallProgressEventArgs(
                            Name,
                            "Download",
                            progressPercentage,
                            Url
                        )
                    );
                }
            }

            gammaProgress.OnProgressChanged(
                new GammaProgress.GammaInstallProgressEventArgs(Name, "Download", 1, Url)
            );
            Downloaded = true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
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
