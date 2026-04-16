using Stalker.Gamma.Models;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices.SpecialRepos;

public interface IGammaSetupRepo : IDownloadableRecord;

public class GammaSetupRepo(
    GammaProgress gammaProgress,
    string gammaDir,
    string url,
    string branch,
    GitUtility gitUtility
) : IGammaSetupRepo
{
    public string Branch { get; } = branch;
    public string Name { get; } = "gamma_setup";
    public string ArchiveName { get; } = "";
    protected string Url = url;
    public string DownloadPath => Path.Join(gammaDir, "downloads", $"{Name}.git");
    public string TempDir => Path.Join(gammaDir, "downloads", Name);
    private string GammaModsDir => Path.Join(gammaDir, "mods");
    private readonly GammaProgress _gammaProgress = gammaProgress;

    public virtual Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (Directory.Exists(DownloadPath))
            {
                gitUtility.FetchGitRepo(
                    DownloadPath,
                    ct: cancellationToken,
                    onProgress: pct => OnProgress(GammaProgressType.Download, pct)
                );
            }
            else
            {
                gitUtility.CloneGitRepo(
                    DownloadPath,
                    Url,
                    onProgress: pct => OnProgress(GammaProgressType.Download, pct),
                    ct: cancellationToken,
                    bare: true
                );
            }
            Downloaded = true;
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
        return Task.CompletedTask;
    }

    public async Task ExpandFilesAsync(CancellationToken ct = default) =>
        await GitUtility.ExtractAsync(
            DownloadPath,
            TempDir,
            branch: Branch,
            ct: ct,
            onProgress: pct => OnProgress(GammaProgressType.Extract, pct)
        );

    public virtual Task ExtractAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            DirUtils.CopyDirectory(
                Path.Join(TempDir, "modpack_addons"),
                GammaModsDir,
                onProgress: pct => OnProgress(GammaProgressType.Extract, pct),
                moveFile: true,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            DirUtils.NormalizePermissions(TempDir);
            Directory.Delete(TempDir, true);
        }

        _gammaProgress.IncrementCompletedMods();
        return Task.CompletedTask;
    }

    public bool Downloaded { get; set; }

    private void OnProgress(GammaProgressType operation, double pct) =>
        _gammaProgress.OnProgressChanged(ProgFunc(operation, pct));

    private GammaProgress.GammaInstallProgressEventArgs ProgFunc(
        GammaProgressType operation,
        double pct
    ) =>
        new()
        {
            Name = Name,
            ProgressType = operation,
            Progress = pct,
            Url = Url,
            ArchiveName = ArchiveName,
            DownloadPath = DownloadPath,
            ExtractPath = GammaModsDir,
        };
}
