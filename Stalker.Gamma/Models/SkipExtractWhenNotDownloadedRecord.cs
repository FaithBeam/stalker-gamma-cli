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
    public string Name { get; } = record.Name;
    public string ArchiveName { get; } = record.ArchiveName;
    public string DownloadPath { get; } = record.DownloadPath;

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
                new GammaProgress.GammaInstallProgressEventArgs(Name, "Skipped", 1, "")
            );
        }
    }

    public bool Downloaded => record.Downloaded;
}
