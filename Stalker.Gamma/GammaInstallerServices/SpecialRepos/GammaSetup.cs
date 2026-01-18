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
    public string DownloadPath => Path.Join(gammaDir, "downloads", $"{Name}.git");
    public string TempDir => Path.Join(gammaDir, "downloads", Name);
    private string GammaModsDir => Path.Join(gammaDir, "mods");

    public virtual async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (Directory.Exists(DownloadPath))
            {
                gitUtility.FetchGitRepo(
                    DownloadPath,
                    ct: cancellationToken,
                    onProgress: pct =>
                        gammaProgress.OnProgressChanged(
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
                    bare: true
                );
            }
            Downloaded = true;
            await GitUtility.ExtractAsync(
                DownloadPath,
                TempDir,
                ct: cancellationToken,
                onProgress: pct =>
                    gammaProgress.OnProgressChanged(
                        new GammaProgress.GammaInstallProgressEventArgs(Name, "Extract", pct, Url)
                    )
            );
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
        try
        {
            DirUtils.CopyDirectory(
                Path.Join(TempDir, "modpack_addons"),
                GammaModsDir,
                onProgress: pct =>
                    gammaProgress.OnProgressChanged(
                        new GammaProgress.GammaInstallProgressEventArgs(Name, "Extract", pct, Url)
                    ),
                moveFile: true
            );
        }
        finally
        {
            DirUtils.NormalizePermissions(TempDir);
            Directory.Delete(TempDir, true);
        }

        gammaProgress.IncrementCompletedMods();
        return Task.CompletedTask;
    }

    public bool Downloaded { get; set; }
}
