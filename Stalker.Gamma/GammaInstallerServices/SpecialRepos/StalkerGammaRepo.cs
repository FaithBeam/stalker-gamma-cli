using Stalker.Gamma.Models;
using Stalker.Gamma.Services;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices.SpecialRepos;

public interface IStalkerGammaRepo : ISpecialRepo;

public class StalkerGammaRepo(
    GammaProgress gammaProgress,
    string gammaDir,
    string anomalyDir,
    string url,
    string branch,
    GitService gitService
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
                gitService.FetchGitRepo(
                    DownloadPath,
                    ct: cancellationToken,
                    onProgress: pct => OnProgress(GammaProgressType.Download, pct)
                );
            }
            else
            {
                gitService.CloneGitRepo(
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

    public async Task ExpandFilesAsync(
        CancellationToken ct = default,
        Func<string, bool>? includePath = null
    )
    {
        try
        {
            await GitService.ExtractAsync(
                DownloadPath,
                TempDir,
                branch: Branch,
                includePath: includePath,
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

    public string? GetModsDirName(string repoPath) =>
        ISpecialRepo.SplitRepoPath(repoPath) is { Length: > 3 } segments
        && segments[0] == "G.A.M.M.A"
        && segments[1] == "modpack_addons"
            ? segments[2]
            : null;

    public string? GetDestinationPath(string repoPath) =>
        ISpecialRepo.SplitRepoPath(repoPath) switch
        {
            ["G.A.M.M.A", "modpack_addons", .. var rest] when rest.Length > 0 => Path.Join([
                GammaModsDir,
                .. rest,
            ]),
            ["G.A.M.M.A", "modpack_patches", .. var rest] when rest.Length > 0 => Path.Join([
                AnomalyDir,
                .. rest,
            ]),
            ["G.A.M.M.A_definition_version.txt"] => Path.Join(GammaModsDir, "..", "version.txt"),
            _ => null,
        };

    public virtual Task ExtractAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // these may not exist when only some files were expanded
            var modpackAddonsDir = Path.Join(TempDir, "G.A.M.M.A", "modpack_addons");
            var modpackPatchesDir = Path.Join(TempDir, "G.A.M.M.A", "modpack_patches");
            var definitionVersionPath = Path.Join(TempDir, "G.A.M.M.A_definition_version.txt");
            if (Directory.Exists(modpackAddonsDir))
            {
                DirUtils.CopyDirectory(
                    modpackAddonsDir,
                    GammaModsDir,
                    onProgress: pct => OnProgress(GammaProgressType.Extract, pct),
                    moveFile: true,
                    cancellationToken: cancellationToken
                );
            }
            if (Directory.Exists(modpackPatchesDir))
            {
                DirUtils.CopyDirectory(
                    modpackPatchesDir,
                    AnomalyDir,
                    onProgress: pct => OnProgress(GammaProgressType.Extract, pct),
                    moveFile: true,
                    cancellationToken: cancellationToken
                );
            }
            if (File.Exists(definitionVersionPath))
            {
                File.Copy(
                    definitionVersionPath,
                    Path.Join(GammaModsDir, "..", "version.txt"),
                    true
                );
            }
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
