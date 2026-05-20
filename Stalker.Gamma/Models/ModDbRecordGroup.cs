using Stalker.Gamma.GammaInstallerServices;

namespace Stalker.Gamma.Models;

public class ModDbRecordGroup(GammaProgress gammaProgress, IList<ModDbRecord> modDbRecords)
    : IDownloadableRecord
{
    private IList<ModDbRecord> ModDbRecords { get; } = modDbRecords;
    public string Name { get; } = modDbRecords.First().Name;
    public string ArchiveName { get; } = modDbRecords.First().ArchiveName;
    public string DownloadPath { get; } = modDbRecords.First().DownloadPath;

    public virtual async Task DownloadAsync(CancellationToken cancellationToken) =>
        await ModDbRecords.First().DownloadAsync(cancellationToken);

    public virtual async Task ExtractAsync(CancellationToken cancellationToken)
    {
        foreach (var modDbRecord in ModDbRecords)
        {
            await modDbRecord.ExtractAsync(cancellationToken);
        }
        gammaProgress.IncrementCompletedMods();
    }

    public bool Downloaded => ModDbRecords.Any(x => x.Downloaded);
}
