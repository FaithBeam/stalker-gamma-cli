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
    public string DownloadPath => Path.Join(gammaDir, "downloads", $"{Name}.git");
    public string TempDir => Path.Join(gammaDir, "downloads", Name);
    private string GammaModsDir => Path.Join(gammaDir, "mods");
    private string AnomalyDir => anomalyDir;

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
        try
        {
            DirUtils.CopyDirectory(
                Path.Join(TempDir, "G.A.M.M.A", "modpack_addons"),
                GammaModsDir,
                onProgress: pct =>
                    gammaProgress.OnProgressChanged(
                        new GammaProgress.GammaInstallProgressEventArgs(Name, "Extract", pct, Url)
                    ),
                moveFile: true
            );
            DirUtils.CopyDirectory(
                Path.Join(TempDir, "G.A.M.M.A", "modpack_patches"),
                AnomalyDir,
                onProgress: pct =>
                    gammaProgress.OnProgressChanged(
                        new GammaProgress.GammaInstallProgressEventArgs(Name, "Extract", pct, Url)
                    ),
                moveFile: true
            );
            File.Copy(
                Path.Join(TempDir, "G.A.M.M.A_definition_version.txt"),
                Path.Join(GammaModsDir, "..", "version.txt"),
                true
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
