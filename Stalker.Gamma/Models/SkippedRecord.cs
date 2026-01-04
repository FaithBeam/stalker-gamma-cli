using Stalker.Gamma.GammaInstallerServices;

namespace Stalker.Gamma.Models;

public class SkippedRecord(GammaProgress gammaProgress, IDownloadableRecord record)
    : IDownloadableRecord
{
    public string Name { get; } = record.Name;
    public string ArchiveName { get; } = record.ArchiveName;
    public string DownloadPath => record.DownloadPath;

    public Task DownloadAsync(CancellationToken cancellationToken)
    {
        gammaProgress.OnProgressChanged(
            new GammaProgress.GammaInstallProgressEventArgs(Name, "Skipped", 1, "")
        );
        return Task.CompletedTask;
    }

    public Task ExtractAsync(CancellationToken cancellationToken)
    {
        gammaProgress.OnProgressChanged(
            new GammaProgress.GammaInstallProgressEventArgs(Name, "Skipped", 1, "")
        );
        gammaProgress.IncrementCompletedMods();
        return Task.CompletedTask;
    }

    public bool Downloaded { get; }
}
