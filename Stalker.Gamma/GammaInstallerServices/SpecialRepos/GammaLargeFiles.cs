using Stalker.Gamma.Models;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices.SpecialRepos;

public interface IGammaLargeFilesRepo : IDownloadableRecord;

public class GammaLargeFilesRepo(
    GammaProgress gammaProgress,
    string gammaDir,
    string url,
    GitUtility gitUtility
) : IGammaLargeFilesRepo
{
    public string Name { get; } = "gamma_large_files_v2";
    public string ArchiveName { get; } = "";
    public string DownloadPath => Path.Join(gammaDir, "downloads", Name);
    protected string Url = url;
    private string DestinationDir => Path.Join(gammaDir, "mods");

    public virtual Task DownloadAsync(CancellationToken ct = default)
    {
        try
        {
            if (Directory.Exists(DownloadPath))
            {
                gitUtility.PullGitRepo(
                    DownloadPath,
                    onProgress: pct =>
                        gammaProgress.OnProgressChanged(
                            new GammaProgress.GammaInstallProgressEventArgs(
                                Name,
                                "Download",
                                pct,
                                Url
                            )
                        ),
                    ct
                );
            }
            else
            {
                gitUtility.CloneGitRepo(
                    DownloadPath,
                    Url,
                    onProgress: pct =>
                        gammaProgress.OnProgressChanged(
                            new GammaProgress.GammaInstallProgressEventArgs(
                                Name,
                                "Download",
                                pct,
                                Url
                            )
                        ),
                    ct: ct,
                    extraArgs: new List<string> { "--depth", "1" }
                );
            }

            Downloaded = true;
            return Task.CompletedTask;
        }
        catch (Exception e)
        {
            throw new SpecialRepoException(
                $"""
                Error downloading from Gamma Large Files Repo
                Url: {Url}
                Download Path: {DownloadPath}
                Destination Dir: {DestinationDir}
                {e}
                """,
                e
            );
        }
    }

    public virtual Task ExtractAsync(CancellationToken cancellationToken = default)
    {
        DirUtils.CopyDirectory(
            DownloadPath,
            DestinationDir,
            onProgress: pct =>
                gammaProgress.OnProgressChanged(
                    new GammaProgress.GammaInstallProgressEventArgs(Name, "Extract", pct, Url)
                )
        );
        gammaProgress.IncrementCompletedMods();
        return Task.CompletedTask;
    }

    public bool Downloaded { get; set; }
}
