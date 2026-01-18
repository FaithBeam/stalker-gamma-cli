using Stalker.Gamma.Models;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices.SpecialRepos;

public interface ITeivazAnomalyGunslingerRepo : IDownloadableRecord;

public class TeivazAnomalyGunslingerRepo(
    GammaProgress gammaProgress,
    string gammaDir,
    string url,
    GitUtility gitUtility
) : ITeivazAnomalyGunslingerRepo
{
    public string Name { get; } = "teivaz_anomaly_gunslinger";
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
            await GitUtility.ExtractAsync(DownloadPath, TempDir, ct: cancellationToken);
        }
        catch (Exception e)
        {
            throw new SpecialRepoException(
                $"""
                Error downloading from Teivaz Anomaly Gunslinger Repo
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
        var dirs = Directory.GetDirectories(TempDir, "gamedata", SearchOption.AllDirectories);
        var ordered = dirs.Order().ToList();

        try
        {
            foreach (var gameDataDir in ordered)
            {
                DirUtils.CopyDirectory(
                    gameDataDir,
                    Path.Join(
                        GammaModsDir,
                        "312- Gunslinger Guns for Anomaly - Teivazcz & Gunslinger Team",
                        "gamedata"
                    ),
                    overwrite: true,
                    onProgress: pct =>
                        gammaProgress.OnProgressChanged(
                            new GammaProgress.GammaInstallProgressEventArgs(
                                Name,
                                "Extract",
                                pct,
                                Url
                            )
                        ),
                    moveFile: true
                );
            }
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
