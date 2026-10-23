using Stalker.Gamma.Models;
using Stalker.Gamma.Services;

namespace Stalker.Gamma.GammaInstallerServices.SpecialRepos;

/// <summary>
/// Used by update apply. Only expands, and therefore only extracts, the special repo files that
/// <list type="bullet">
/// <item>go into addon folders that were re-extracted by updated ModDb and Github records.
/// Re-extracting a record deletes its addon folder, which removes any files the special repos put there.</item>
/// <item>changed in the repo since the commit that was last applied</item>
/// </list>
/// Files deleted from the repo since the last applied commit are deleted from where they were extracted to.
/// If the last applied commit is unknown, every file is expanded.
/// </summary>
/// <param name="specialRepo"></param>
/// <param name="outputDirNames">The output dir names of the updated ModDb and Github records</param>
public class UpdateSpecialRepoRecord(ISpecialRepo specialRepo, IReadOnlySet<string> outputDirNames)
    : ISpecialRepo
{
    public string Name => specialRepo.Name;
    public string ArchiveName => specialRepo.ArchiveName;
    public string DownloadPath => specialRepo.DownloadPath;
    public string Branch => specialRepo.Branch;
    public bool Downloaded => specialRepo.Downloaded;
    private string AppliedCommitPath => GetAppliedCommitPath(specialRepo);
    private string? _expandedCommit;
    private GitTreeChanges? _changes;

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// The file the sha of the commit that was last extracted from a special repo is written to
    /// </summary>
    public static string GetAppliedCommitPath(IDownloadableRecord specialRepo) =>
        Path.ChangeExtension(specialRepo.DownloadPath, ".applied-commit");

    public async Task DownloadAsync(CancellationToken cancellationToken)
    {
        // installs from before the applied commit was tracked: the commit before fetching is the one that was last applied
        if (!File.Exists(AppliedCommitPath) && Directory.Exists(DownloadPath))
        {
            await File.WriteAllTextAsync(
                AppliedCommitPath,
                GitService.GetCommitSha(DownloadPath, Branch),
                cancellationToken
            );
        }
        await specialRepo.DownloadAsync(cancellationToken);
    }

    public async Task ExpandFilesAsync(
        CancellationToken ct = default,
        Func<string, bool>? includePath = null
    )
    {
        _expandedCommit = GitService.GetCommitSha(DownloadPath, Branch);
        var appliedCommit = File.Exists(AppliedCommitPath)
            ? (await File.ReadAllTextAsync(AppliedCommitPath, ct)).Trim()
            : null;
        _changes = string.IsNullOrWhiteSpace(appliedCommit)
            ? null
            : GitService.GetChanges(DownloadPath, appliedCommit, _expandedCommit);

        if (_changes is null)
        {
            await specialRepo.ExpandFilesAsync(ct, includePath);
            return;
        }

        var changedPaths = _changes
            .ChangedPaths.Select(ISpecialRepo.NormalizeRepoPath)
            .ToHashSet(StringComparer.Ordinal);
        await specialRepo.ExpandFilesAsync(
            ct,
            repoPath =>
                (
                    IsInUpdatedAddon(repoPath)
                    || changedPaths.Contains(ISpecialRepo.NormalizeRepoPath(repoPath))
                ) && (includePath?.Invoke(repoPath) ?? true)
        );
    }

    public async Task ExtractAsync(CancellationToken cancellationToken)
    {
        await specialRepo.ExtractAsync(cancellationToken);
        if (_changes is not null)
        {
            DeleteRemovedFiles(_changes.DeletedPaths);
        }
        if (_expandedCommit is not null)
        {
            await File.WriteAllTextAsync(AppliedCommitPath, _expandedCommit, cancellationToken);
        }
    }

    public string? GetModsDirName(string repoPath) => specialRepo.GetModsDirName(repoPath);

    public string? GetDestinationPath(string repoPath) => specialRepo.GetDestinationPath(repoPath);

    private bool IsInUpdatedAddon(string repoPath) =>
        GetModsDirName(repoPath) is { } modsDirName && outputDirNames.Contains(modsDirName);

    private void DeleteRemovedFiles(IReadOnlyList<string> deletedPaths)
    {
        if (deletedPaths.Count == 0)
        {
            return;
        }

        // a file can still be extracted to the same destination from another path, e.g. teivaz's gamedata folders
        var stillExtracted = GitService
            .GetFilePaths(DownloadPath, _expandedCommit!)
            .Select(GetDestinationPath)
            .Where(x => x is not null)
            .Select(x => Path.GetFullPath(x!))
            .ToHashSet(PathComparer);

        foreach (var deletedPath in deletedPaths)
        {
            // updated addon folders were freshly re-extracted, so the file is already gone
            if (IsInUpdatedAddon(deletedPath) || GetDestinationPath(deletedPath) is not { } dest)
            {
                continue;
            }
            dest = Path.GetFullPath(dest);
            if (stillExtracted.Contains(dest) || !File.Exists(dest))
            {
                continue;
            }
            File.SetAttributes(dest, FileAttributes.Normal);
            File.Delete(dest);
        }
    }
}
