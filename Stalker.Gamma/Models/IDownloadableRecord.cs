using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Models;

public interface IDownloadableRecord
{
    public string Name { get; }
    public string ArchiveName { get; }
    string DownloadPath { get; }

    /// <summary>ModDB page, GitHub release, or other download link, if known.</summary>
    string? DownloadLink => null;
    public Task DownloadAsync(CancellationToken cancellationToken);
    public Task ExtractAsync(CancellationToken cancellationToken);
    public bool Downloaded { get; }

    public bool ArchiveExists() => File.Exists(DownloadPath) || Directory.Exists(DownloadPath);

    public void DeleteArchive()
    {
        if (File.Exists(DownloadPath))
        {
            File.Delete(DownloadPath);
        }
        else if (Directory.Exists(DownloadPath))
        {
            DirUtils.NormalizePermissions(DownloadPath);
            Directory.Delete(DownloadPath, true);
        }
    }
}
