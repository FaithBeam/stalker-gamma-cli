using Stalker.Gamma.GammaInstallerServices;

namespace Stalker.Gamma.Models;

public class GithubRecordGroup(GammaProgress gammaProgress, IList<GithubRecord> githubRecords)
    : IDownloadableRecord
{
    public string Name { get; } = githubRecords.First().Name;
    public string ArchiveName { get; } = githubRecords.First().ArchiveName;
    public string DownloadPath { get; } = githubRecords.First().DownloadPath;
    private IList<GithubRecord> GithubRecords { get; } = githubRecords;

    public virtual async Task DownloadAsync(CancellationToken cancellationToken) =>
        await GithubRecords.First().DownloadAsync(cancellationToken);

    public virtual async Task ExtractAsync(CancellationToken cancellationToken)
    {
        foreach (var githubRecord in GithubRecords)
        {
            await githubRecord.ExtractAsync(cancellationToken);
        }
        gammaProgress.IncrementCompletedMods();
    }

    public bool Downloaded => GithubRecords.Any(x => x.Downloaded);
}
