using LibGit2Sharp;
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
    public string DownloadPath => Path.Join(_gammaDir, "downloads", $"{Name}.git");
    public string TempDir => Path.Join(_gammaDir, "downloads", Name);
    protected string Url = url;
    private readonly GammaProgress _gammaProgress = gammaProgress;
    private readonly string _gammaDir = gammaDir;
    private readonly GitUtility _gitUtility = gitUtility;
    private string DestinationDir => Path.Join(_gammaDir, "mods");

    public virtual async Task DownloadAsync(CancellationToken ct = default)
    {
        try
        {
            if (Directory.Exists(DownloadPath))
            {
                _gitUtility.FetchGitRepo(
                    DownloadPath,
                    ct: ct,
                    onProgress: pct =>
                        _gammaProgress.OnProgressChanged(
                            new GammaProgress.GammaInstallProgressEventArgs(
                                Name,
                                "Download",
                                pct,
                                Url
                            )
                        )
                );
            }
            else
            {
                _gitUtility.CloneGitRepo(
                    DownloadPath,
                    Url,
                    onProgress: pct =>
                        _gammaProgress.OnProgressChanged(
                            new GammaProgress.GammaInstallProgressEventArgs(
                                Name,
                                "Download",
                                pct,
                                Url
                            )
                        ),
                    ct: ct,
                    bare: true
                );
            }

            Downloaded = true;
            await GitUtility.ExtractAsync(
                DownloadPath,
                TempDir,
                ct: ct,
                onProgress: pct =>
                    _gammaProgress.OnProgressChanged(
                        new GammaProgress.GammaInstallProgressEventArgs(Name, "Extract", pct, Url)
                    )
            );
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
            TempDir,
            DestinationDir,
            onProgress: pct =>
                _gammaProgress.OnProgressChanged(
                    new GammaProgress.GammaInstallProgressEventArgs(Name, "Extract", pct, Url)
                )
        );
        _gammaProgress.IncrementCompletedMods();
        return Task.CompletedTask;
    }

    public bool Downloaded { get; set; }
}
