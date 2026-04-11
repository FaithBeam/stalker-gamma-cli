using Stalker.Gamma.GammaInstallerServices;

namespace Stalker.Gamma.Models;

public class ModDbRecordGetMetadataGroup(
    GammaProgress gammaProgress,
    IList<ModDbRecordGetMetadata> modDbRecords
) : IDownloadableRecord
{
    public string Name => _modDbRecords.First().Name;
    public string ArchiveName => _modDbRecords.First().ArchiveName;
    public string DownloadPath => _modDbRecords.First().DownloadPath;
    private string StartLink => _modDbRecords.First().StartLink;
    private string Md5 => _modDbRecords.First().Md5;

    public virtual async Task DownloadAsync(CancellationToken cancellationToken) =>
        await _modDbRecords.First().DownloadAsync(cancellationToken);

    public virtual async Task ExtractAsync(CancellationToken cancellationToken)
    {
        foreach (var modDbRecord in _modDbRecords)
        {
            modDbRecord.ArchiveName = ArchiveName;
            modDbRecord.Md5 = Md5;
            modDbRecord.StartLink = StartLink;

            await modDbRecord.ExtractAsync(cancellationToken);
        }
        _gammaProgress.IncrementCompletedMods();
    }

    public bool Downloaded => _modDbRecords.Any(x => x.Downloaded);
    private readonly GammaProgress _gammaProgress = gammaProgress;
    private readonly IList<ModDbRecordGetMetadata> _modDbRecords = modDbRecords;
}
