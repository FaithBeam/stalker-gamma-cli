using Stalker.Gamma.GammaInstallerServices;

namespace Stalker.Gamma.Models;

/// <summary>
/// Skip extract on hash match, but if the archive does not exist, it will be downloaded and extracted.
/// </summary>
public class SkipExtractWhenNotDownloadedRecord(
    GammaProgress gammaProgress,
    IDownloadableRecord record
) : IDownloadableRecord
{
    // read through to the wrapped record because some only know their archive after downloading
    public string Name => record.Name;
    public string ArchiveName => record.ArchiveName;
    public string DownloadPath => record.DownloadPath;
    public string ResumeKey => record.ResumeKey;

    public async Task DownloadAsync(CancellationToken cancellationToken) =>
        await record.DownloadAsync(cancellationToken);

    public async Task ExtractAsync(CancellationToken cancellationToken)
    {
        // if the underlying record downloads the archive, it was either missing or had a md5 mismatch
        if (Downloaded)
        {
            await record.ExtractAsync(cancellationToken);
        }
        else
        {
            gammaProgress.IncrementCompletedMods();
            gammaProgress.OnProgressChanged(
                new GammaProgress.GammaInstallProgressEventArgs
                {
                    Name = Name,
                    ProgressType = GammaProgressType.Skipped,
                    Progress = 1,
                    Url = "",
                    ArchiveName = ArchiveName,
                    DownloadPath = DownloadPath,
                    ExtractPath = "",
                }
            );
        }
    }

    public bool Downloaded => record.Downloaded;

    public bool ArchiveExists() => record.ArchiveExists();

    public void DeleteArchive() => record.DeleteArchive();
}
