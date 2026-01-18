using System.Text.RegularExpressions;
using LibGit2Sharp;

namespace Stalker.Gamma.Utilities;

public partial class GitUtility
{
    public void SetConfig<TValue>(
        string pathToRepo,
        string key,
        TValue? value,
        ConfigurationLevel configurationLevel = ConfigurationLevel.Local
    )
    {
        using var repo = new Repository(pathToRepo);
        repo.Config.Set(key, value, configurationLevel);
    }

    public void CloneGitRepo(
        string outputDir,
        string repoUrl,
        Action<double>? onProgress = null,
        CancellationToken ct = default,
        IList<string>? extraArgs = null
    )
    {
        var options = new CloneOptions
        {
            FetchOptions =
            {
                OnTransferProgress = progress =>
                {
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
    }

    public void PullGitRepo(
        string pathToRepo,
        Action<double>? onProgress = null,
        CancellationToken ct = default
    )
    {
        using var repo = new Repository(pathToRepo);
        Commands.Pull(repo, _signature, null);
    }

    public bool Ready => true;

    private readonly Signature _signature = new(
        "Stalker_GAMMA",
        "stalker@gamma.com",
        DateTimeOffset.Now
    );

    [GeneratedRegex(@"Receiving objects:\s*(\d+)%")]
    private partial Regex ProgressRegex();
}

public class GitUtilityException(string msg, Exception innerException)
    : Exception(msg, innerException);
