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
    public string DownloadPath => Path.Join(gammaDir, "downloads", Name);
    private string GammaModsDir => Path.Join(gammaDir, "mods");

    public virtual async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(DownloadPath))
        {
            await gitUtility.PullGitRepo(
                DownloadPath,
                onProgress: pct =>
                    gammaProgress.OnProgressChanged(
                        new GammaProgress.GammaInstallProgressEventArgs(Name, "Download", pct, Url)
                    ),
                ct: cancellationToken
            );
        }
        else
        {
            await gitUtility.CloneGitRepo(
                DownloadPath,
                Url,
                onProgress: pct =>
                    gammaProgress.OnProgressChanged(
                        new GammaProgress.GammaInstallProgressEventArgs(Name, "Download", pct, Url)
                    ),
                ct: cancellationToken,
                extraArgs: new List<string> { "--depth", "1" }
            );
        }

        Downloaded = true;
    }

    public virtual Task ExtractAsync(CancellationToken cancellationToken = default)
    {
        gammaProgress.OnDebugProgressChanged(
            new GammaProgress.GammaInstallDebugProgressEventArgs { Text = "START COPY TEIVAZ" }
        );
        var dirs = Directory.GetDirectories(DownloadPath, "gamedata", SearchOption.AllDirectories);
        var ordered = dirs.Order().ToList();
        gammaProgress.OnDebugProgressChanged(
            new GammaProgress.GammaInstallDebugProgressEventArgs
            {
                Text = $"""
                UNORDERED:
                {string.Join(Environment.NewLine, dirs)}
                """,
            }
        );
        gammaProgress.OnDebugProgressChanged(
            new GammaProgress.GammaInstallDebugProgressEventArgs
            {
                Text = $"""
                ORDERED:
                {string.Join(Environment.NewLine, ordered)}
                """,
            }
        );

        foreach (var gameDataDir in ordered)
        {
            gammaProgress.OnDebugProgressChanged(
                new GammaProgress.GammaInstallDebugProgressEventArgs { Text = gameDataDir }
            );
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
                        new GammaProgress.GammaInstallProgressEventArgs(Name, "Extract", pct, Url)
                    ),
                txtProgress: txt =>
                    gammaProgress.OnDebugProgressChanged(
                        new GammaProgress.GammaInstallDebugProgressEventArgs { Text = txt }
                    )
            );
        }
        gammaProgress.OnDebugProgressChanged(
            new GammaProgress.GammaInstallDebugProgressEventArgs { Text = "END COPY TEIVAZ" }
        );

        gammaProgress.IncrementCompletedMods();
        return Task.CompletedTask;
    }

    public bool Downloaded { get; set; }
}
