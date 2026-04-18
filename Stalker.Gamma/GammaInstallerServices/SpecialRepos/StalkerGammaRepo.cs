using Stalker.Gamma.Models;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices.SpecialRepos;

public interface IStalkerGammaRepo : IDownloadableRecord;

public class StalkerGammaRepo(
    GammaProgress gammaProgress,
    string gammaDir,
    string anomalyDir,
    string url,
    string branch,
    GitUtility gitUtility
) : IStalkerGammaRepo
{
    public string Name { get; } = "Stalker_GAMMA";
    protected string Url = url;
    public string Branch { get; } = branch;
    public string ArchiveName { get; } = "";
    public string DownloadPath => Path.Join(gammaDir, "downloads", $"{Name}.git");
    public string TempDir => Path.Join(gammaDir, "downloads", Name);
    private string GammaModsDir => Path.Join(gammaDir, "mods");
    private string AnomalyDir => anomalyDir;
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
                Error downloading from Stalker Gamma Repo
                Url: {Url}
                Branch: {Branch}
                Download Path: {DownloadPath}
                Destination Dir: {GammaModsDir}
                Anomaly Dir: {AnomalyDir}
                Exception Message: {e.Message}
                """,
                e
            );
        }
        return Task.CompletedTask;
    }

    public async Task ExpandFilesAsync(CancellationToken ct = default)
    {
        try
        {
            await GitUtility.ExtractAsync(
                DownloadPath,
                TempDir,
                branch: Branch,
                ct: ct,
                onProgress: pct => OnProgress(GammaProgressType.Extract, pct)
            );
        }
        catch (Exception e)
        {
            throw new SpecialRepoException(
                $"""
                Error expanding Stalker Gamma Repo
                Url: {Url}
                Branch: {Branch}
                Download Path: {DownloadPath}
                Destination Dir: {GammaModsDir}
                Anomaly Dir: {AnomalyDir}
                Exception Message: {e.Message}
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
                onProgress: pct => OnProgress(GammaProgressType.Extract, pct),
                moveFile: true,
                cancellationToken: cancellationToken
            );
            DirUtils.CopyDirectory(
                Path.Join(TempDir, "G.A.M.M.A", "modpack_patches"),
                AnomalyDir,
                onProgress: pct => OnProgress(GammaProgressType.Extract, pct),
                moveFile: true,
                cancellationToken: cancellationToken
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
