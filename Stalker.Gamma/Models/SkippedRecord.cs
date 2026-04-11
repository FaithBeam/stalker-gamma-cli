using Stalker.Gamma.GammaInstallerServices;

namespace Stalker.Gamma.Models;

public class SkippedRecord(GammaProgress gammaProgress, IDownloadableRecord record)
    : IDownloadableRecord
{
    private readonly GammaProgress _gammaProgress = gammaProgress;
    private readonly IDownloadableRecord _record = record;
    public string Name { get; } = record.Name;
    public string ArchiveName { get; } = record.ArchiveName;
    public string DownloadPath => _record.DownloadPath;

    public Task DownloadAsync(CancellationToken cancellationToken)
    {
        OnProgress(GammaProgressType.Skipped, 1);
        return Task.CompletedTask;
    }

    public Task ExtractAsync(CancellationToken cancellationToken)
    {
        OnProgress(GammaProgressType.Skipped, 1);
        _gammaProgress.IncrementCompletedMods();
        return Task.CompletedTask;
    }

    public bool Downloaded { get; }

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
            Url = "Skipped",
            ArchiveName = ArchiveName,
            DownloadPath = DownloadPath,
            ExtractPath = "Skipped",
        };
}
