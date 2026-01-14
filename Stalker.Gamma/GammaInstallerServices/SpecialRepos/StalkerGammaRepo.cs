using LibGit2Sharp;
using Stalker.Gamma.Models;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices.SpecialRepos;

public interface IStalkerGammaRepo : IDownloadableRecord;

public class StalkerGammaRepo(
    GammaProgress gammaProgress,
    string gammaDir,
    string anomalyDir,
    string url,
    GitUtility gitUtility
) : IStalkerGammaRepo
{
    public string Name { get; } = "Stalker_GAMMA";
    protected string Url = url;
    public string ArchiveName { get; } = "";
    public string DownloadPath => Path.Join(gammaDir, "downloads", Name);
    private string GammaModsDir => Path.Join(gammaDir, "mods");
    private string AnomalyDir => anomalyDir;

    public virtual Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (Directory.Exists(DownloadPath))
            {
                try
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
                catch (LibGit2SharpException e) when (e.Message.Contains("no tracking information"))
                {
                    DirUtils.NormalizePermissions(DownloadPath);
                    Directory.Delete(DownloadPath, true);
                    return DownloadAsync(cancellationToken);
                }
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
                Error downloading from Stalker Gamma Repo
                Url: {Url}
                Download Path: {DownloadPath}
                Destination Dir: {GammaModsDir}
                Anomaly Dir: {AnomalyDir}
                {e}
                """,
                e
            );
        }
    }

    public virtual Task ExtractAsync(CancellationToken cancellationToken = default)
    {
        DirUtils.CopyDirectory(
            Path.Join(DownloadPath, "G.A.M.M.A", "modpack_addons"),
            GammaModsDir,
            onProgress: pct =>
                gammaProgress.OnProgressChanged(
                    new GammaProgress.GammaInstallProgressEventArgs(Name, "Extract", pct, Url)
                )
        );
        DirUtils.CopyDirectory(
            Path.Join(DownloadPath, "G.A.M.M.A", "modpack_patches"),
            AnomalyDir,
            onProgress: pct =>
                gammaProgress.OnProgressChanged(
                    new GammaProgress.GammaInstallProgressEventArgs(Name, "Extract", pct, Url)
                )
        );
        File.Copy(
            Path.Join(DownloadPath, "G.A.M.M.A_definition_version.txt"),
            Path.Join(GammaModsDir, "..", "version.txt"),
            true
        );
        gammaProgress.IncrementCompletedMods();
        return Task.CompletedTask;
    }

    public bool Downloaded { get; set; }
}
