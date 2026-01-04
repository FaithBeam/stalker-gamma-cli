namespace Stalker.Gamma.Models;

public interface IDownloadableRecord
{
    public string Name { get; }
    public string ArchiveName { get; }
    string DownloadPath { get; }
    public Task DownloadAsync(CancellationToken cancellationToken);
    public Task ExtractAsync(CancellationToken cancellationToken);
    public bool Downloaded { get; }
}
