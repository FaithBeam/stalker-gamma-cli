using Stalker.Gamma.Models;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices.SpecialRepos;

public interface IGammaSetupRepo : IDownloadableRecord;

public class GammaSetupRepo(
    GammaProgress gammaProgress,
    string gammaDir,
    string url,
    GitUtility gitUtility
) : IGammaSetupRepo
{
    public string Name { get; } = "gamma_setup";
    public string ArchiveName { get; } = "";
    protected string Url = url;
    public string DownloadPath => Path.Join(gammaDir, "downloads", Name);
    private string GammaModsDir => Path.Join(gammaDir, "mods");

    public virtual Task DownloadAsync(CancellationToken cancellationToken = default)
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
                    ct: cancellationToken
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
                    ct: cancellationToken,
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
                Error downloading from Gamma Setup Repo
                Url: {Url}
                Download Path: {DownloadPath}
                Destination Dir: {GammaModsDir}
                {e}
                """,
                e
            );
        }
    }

    public virtual Task ExtractAsync(CancellationToken cancellationToken = default)
    {
        DirUtils.CopyDirectory(
            Path.Join(DownloadPath, "modpack_addons"),
            GammaModsDir,
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
