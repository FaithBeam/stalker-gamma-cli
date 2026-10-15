using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Models;

public interface IDownloadableRecord
{
    public string Name { get; }
    public string ArchiveName { get; }
    string DownloadPath { get; }
    public Task DownloadAsync(CancellationToken cancellationToken);
    public Task ExtractAsync(CancellationToken cancellationToken);
    public bool Downloaded { get; }

    /// <summary>
    /// Identifies this record in the resume file. Must be known before the record is downloaded.
    /// </summary>
    public string ResumeKey => $"{Name} | {ArchiveName}";

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
