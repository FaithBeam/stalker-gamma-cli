using Stalker.Gamma.GammaInstallerServices;

namespace Stalker.Gamma.Models;

/// <summary>
/// Records the status of the wrapped record in <see cref="ResumeState"/>.
/// If the record was already complete when this was created, download and extract are skipped.
/// </summary>
public class ResumableRecord(
    GammaProgress gammaProgress,
    IDownloadableRecord record,
    string key,
    ResumeState resumeState
) : IDownloadableRecord
{
    private readonly bool _alreadyComplete = resumeState.IsComplete(key);
    public string Name => record.Name;
    public string ArchiveName => record.ArchiveName;
    public string DownloadPath => record.DownloadPath;

    public async Task DownloadAsync(CancellationToken cancellationToken)
    {
        if (_alreadyComplete)
        {
            OnSkipped();
            return;
        }
        await record.DownloadAsync(cancellationToken);
        resumeState.SetStatus(key, ResumeStatus.Extract);
    }

    public async Task ExtractAsync(CancellationToken cancellationToken)
    {
        if (_alreadyComplete)
        {
            gammaProgress.IncrementCompletedMods();
            OnSkipped();
            return;
        }
        await record.ExtractAsync(cancellationToken);
        resumeState.SetStatus(key, ResumeStatus.Complete);
    }

    public bool Downloaded => record.Downloaded;

    // A skipped record never ran its download, so its download path may not be resolved
    public void DeleteArchive()
    {
        if (!_alreadyComplete)
        {
            record.DeleteArchive();
        }
    }

    private void OnSkipped() =>
        gammaProgress.OnProgressChanged(
            new GammaProgress.GammaInstallProgressEventArgs
            {
                Name = Name,
                ProgressType = GammaProgressType.Skipped,
                Progress = 1,
                Url = "Resumed",
                ArchiveName = ArchiveName ?? "",
                DownloadPath = "Resumed",
                ExtractPath = "Resumed",
            }
        );
}
