using System.Collections.Concurrent;
using LibGit2Sharp;

namespace Stalker.Gamma.Services;

public partial class GitService
{
    public string GetLatestCommitHash(string pathToRepo)
    {
        try
        {
            using var repo = new Repository(pathToRepo);
            return repo.Head.Tip.Sha;
        }
        catch (Exception e)
        {
            throw new GitUtilityException(
                $"""
                Error getting latest commit hash
                Path to repo: {pathToRepo}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    public void FetchGitRepo(
        string pathToRepo,
        Action<double>? onProgress = null,
        CancellationToken ct = default
    )
    {
        try
        {
            using var repo = new Repository(pathToRepo);
            var remote = repo.Network.Remotes["origin"] ?? repo.Network.Remotes["Grokitach"];
            var options = new FetchOptions
            {
                OnTransferProgress = progress =>
                {
                    if (ct.IsCancellationRequested)
                    {
                        return false;
                    }
                    if (progress.TotalObjects > 0)
                    {
                        onProgress?.Invoke(
                            (double)progress.ReceivedObjects / progress.TotalObjects
                        );
                    }
                    return true;
                },
            };
            Commands.Fetch(
                repo,
                remote.Name,
                ["+refs/heads/*:refs/heads/*", "+refs/tags/*:refs/tags/*"],
                options,
                "Fetch updates"
            );
        }
        catch (Exception e)
        {
            throw new GitUtilityException(
                $"""
                Error fetching updates for repo
                Path to repo: {pathToRepo}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    public void CloneGitRepo(
        string outputDir,
        string repoUrl,
        Action<double>? onProgress = null,
        bool bare = false,
        CancellationToken ct = default
    )
    {
        try
        {
            var options = new CloneOptions
            {
                IsBare = bare,
                Checkout = !bare,
                FetchOptions =
                {
                    OnTransferProgress = progress =>
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return false;
                        }
                        if (progress.TotalObjects > 0)
                        {
                            onProgress?.Invoke(
                                (double)progress.ReceivedObjects / progress.TotalObjects
                            );
                        }
                        return true;
                    },
                },
            };
            Repository.Clone(repoUrl, outputDir, options);
            using var repo = new Repository(outputDir);
            repo.Config.Set("core.longpaths", true);
        }
        catch (Exception e)
        {
            throw new GitUtilityException(
                $"""
                Error cloning repo
                Output Dir: {outputDir}
                Repo URL: {repoUrl}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    private record ExtractableBlob(string Path, ObjectId Id, long Size);

    private static void CollectExtractableBlobs(
        Repository repo,
        Tree tree,
        List<ExtractableBlob> blobs,
        Func<string, bool>? includePath = null
    )
    {
        foreach (var entry in tree)
        {
            switch (entry.TargetType)
            {
                case TreeEntryTargetType.Blob when includePath?.Invoke(entry.Path) ?? true:
                    blobs.Add(
                        new ExtractableBlob(
                            entry.Path,
                            entry.Target.Id,
                            repo.ObjectDatabase.RetrieveObjectMetadata(entry.Target.Id).Size
                        )
                    );
                    break;
                case TreeEntryTargetType.Tree:
                    CollectExtractableBlobs(repo, (Tree)entry.Target, blobs, includePath);
                    break;
            }
        }
    }

    /// <param name="includePath">Only extract blobs whose repo relative path this returns true for</param>
    public static async Task ExtractAsync(
        string pathToRepo,
        string outputDir,
        Action<double>? onProgress = null,
        string branch = "main",
        Func<string, bool>? includePath = null,
        CancellationToken ct = default
    )
    {
        try
        {
            List<ExtractableBlob> blobs = [];
            using (var repo = new Repository(pathToRepo))
            {
                CollectExtractableBlobs(repo, LookupCommit(repo, branch).Tree, blobs, includePath);
            }
            await Task.Run(() => ExtractBlobs(pathToRepo, outputDir, blobs, onProgress, ct), ct);
        }
        catch (Exception e)
        {
            throw new GitUtilityException(
                $"""
                Error extracting repo
                Path to repo: {pathToRepo}
                Output Dir: {outputDir}
                Branch: {branch}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    private static Commit LookupCommit(Repository repo, string branch)
    {
        var branchOrSha = branch is "main" or "dev2" ? $"refs/remotes/origin/{branch}" : branch;
        var gammaLauncherBranch = $"refs/remotes/Grokitach/{branch}";
        return repo.Lookup<Commit>(branchOrSha) ?? repo.Lookup<Commit>(gammaLauncherBranch);
    }

    /// <summary>
    /// The sha of the commit <see cref="ExtractAsync"/> extracts for a branch
    /// </summary>
    public static string GetCommitSha(string pathToRepo, string branch = "main")
    {
        try
        {
            using var repo = new Repository(pathToRepo);
            return LookupCommit(repo, branch).Sha;
        }
        catch (Exception e)
        {
            throw new GitUtilityException(
                $"""
                Error getting commit sha
                Path to repo: {pathToRepo}
                Branch: {branch}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    /// <summary>
    /// The repo relative paths of the files that changed between two commits,
    /// or null if either commit isn't in the repo
    /// </summary>
    public static GitTreeChanges? GetChanges(string pathToRepo, string oldSha, string newSha)
    {
        try
        {
            using var repo = new Repository(pathToRepo);
            var oldCommit = repo.Lookup<Commit>(oldSha);
            var newCommit = repo.Lookup<Commit>(newSha);
            if (oldCommit is null || newCommit is null)
            {
                return null;
            }

            List<string> changedPaths = [];
            List<string> deletedPaths = [];
            foreach (var change in repo.Diff.Compare<TreeChanges>(oldCommit.Tree, newCommit.Tree))
            {
                switch (change.Status)
                {
                    case ChangeKind.Unmodified:
                        break;
                    case ChangeKind.Deleted:
                        deletedPaths.Add(change.OldPath);
                        break;
                    case ChangeKind.Renamed:
                        deletedPaths.Add(change.OldPath);
                        changedPaths.Add(change.Path);
                        break;
                    default:
                        changedPaths.Add(change.Path);
                        break;
                }
            }
            return new GitTreeChanges(changedPaths, deletedPaths);
        }
        catch (Exception e)
        {
            throw new GitUtilityException(
                $"""
                Error getting changes between commits
                Path to repo: {pathToRepo}
                Old Sha: {oldSha}
                New Sha: {newSha}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    /// <summary>
    /// The repo relative paths of every file in a commit
    /// </summary>
    public static List<string> GetFilePaths(string pathToRepo, string sha)
    {
        try
        {
            using var repo = new Repository(pathToRepo);
            List<string> paths = [];
            CollectBlobPaths(repo.Lookup<Commit>(sha).Tree, paths);
            return paths;
        }
        catch (Exception e)
        {
            throw new GitUtilityException(
                $"""
                Error getting file paths
                Path to repo: {pathToRepo}
                Sha: {sha}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    private static void CollectBlobPaths(Tree tree, List<string> paths)
    {
        foreach (var entry in tree)
        {
            switch (entry.TargetType)
            {
                case TreeEntryTargetType.Blob:
                    paths.Add(entry.Path);
                    break;
                case TreeEntryTargetType.Tree:
                    CollectBlobPaths((Tree)entry.Target, paths);
                    break;
            }
        }
    }

    private static void ExtractBlobs(
        string pathToRepo,
        string outputDir,
        List<ExtractableBlob> blobs,
        Action<double>? onProgress,
        CancellationToken ct
    )
    {
        Directory.CreateDirectory(outputDir);
        foreach (
            var directoryName in blobs
                .Select(blob => Path.GetDirectoryName(Path.Combine(outputDir, blob.Path)))
                .Distinct()
        )
        {
            if (!string.IsNullOrWhiteSpace(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
        }

        var extracted = 0;
        Parallel.ForEach(
            // largest first so a big file doesn't start last and leave the other workers idle.
            // NoBuffering hands out one blob at a time in this order instead of in chunks
            Partitioner.Create(
                blobs.OrderByDescending(blob => blob.Size),
                EnumerablePartitionerOptions.NoBuffering
            ),
            // leave half the cores free so the rest of the app (and the machine) stays responsive
            new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
            },
            // libgit2 handles aren't safe to share across threads, so each worker opens its own
            () => new Repository(pathToRepo),
            (blob, _, repo) =>
            {
                using var content = repo.Lookup<Blob>(blob.Id).GetContentStream();
                using var file = File.Create(Path.Combine(outputDir, blob.Path));
                content.CopyTo(file);
                onProgress?.Invoke((double)Interlocked.Increment(ref extracted) / blobs.Count);
                return repo;
            },
            repo => repo.Dispose()
        );
    }

    public static bool Ready => true;
}

public record GitTreeChanges(
    IReadOnlyList<string> ChangedPaths,
    IReadOnlyList<string> DeletedPaths
);

public class GitUtilityException(string msg, Exception innerException)
    : Exception(msg, innerException);
