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

    private static int CountBlobs(Tree tree)
    {
        var count = 0;

        foreach (var entry in tree)
        {
            switch (entry.TargetType)
            {
                case TreeEntryTargetType.Blob:
                    count++;
                    break;
                case TreeEntryTargetType.Tree:
                    count += CountBlobs((Tree)entry.Target);
                    break;
            }
        }

        return count;
    }

    public static async Task ExtractAsync(
        string pathToRepo,
        string outputDir,
        Action<double>? onProgress = null,
        string branch = "main",
        CancellationToken ct = default
    )
    {
        try
        {
            var branchOrSha = branch is "main" or "dev2" ? $"refs/remotes/origin/{branch}" : branch;
            var gammaLauncherBranch = $"refs/remotes/Grokitach/{branch}";
            using var repo = new Repository(pathToRepo);
            var commit =
                repo.Lookup<Commit>(branchOrSha) ?? repo.Lookup<Commit>(gammaLauncherBranch);
            await ExtractTreeAsync(commit.Tree, outputDir, ct: ct, onProgress: onProgress);
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

    private static async Task<int> ExtractTreeAsync(
        Tree tree,
        string basePath,
        Action<double>? onProgress = null,
        int total = 0,
        int current = 0,
        CancellationToken ct = default
    )
    {
        total = total == 0 ? CountBlobs(tree) : total;
        if (!Directory.Exists(basePath))
        {
            Directory.CreateDirectory(basePath);
        }

        foreach (var entry in tree)
        {
            var entryPath = Path.Combine(basePath, entry.Path);
            switch (entry.TargetType)
            {
                case TreeEntryTargetType.Tree:
                    current = await ExtractTreeAsync(
                        (Tree)entry.Target,
                        basePath,
                        ct: ct,
                        onProgress: onProgress,
                        total: total,
                        current: current
                    );
                    break;
                case TreeEntryTargetType.Blob:
                {
                    var directoryName = Path.GetDirectoryName(entryPath);
                    if (
                        !string.IsNullOrWhiteSpace(directoryName)
                        && !Directory.Exists(directoryName)
                    )
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                    var blob = (Blob)entry.Target;
                    await using var content = blob.GetContentStream();
                    await using var file = File.Create(entryPath);
                    await content.CopyToAsync(file, ct);
                    current++;
                    onProgress?.Invoke((double)current / total);
                    break;
                }
            }
        }
        return current;
    }

    public static bool Ready => true;
}

public class GitUtilityException(string msg, Exception innerException)
    : Exception(msg, innerException);
