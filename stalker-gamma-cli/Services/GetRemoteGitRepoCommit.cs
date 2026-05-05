using Stalker.Gamma.Factories;
using Stalker.Gamma.OpenApi.GithubClient;

namespace stalker_gamma_cli.Services;

public class GetRemoteGitRepoCommit(GithubClientFactory githubClientFactory)
{
    public async Task<string?> ExecuteAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default
    ) =>
        (
            await _githubClient
                .Repos[owner][repo]
                .Commits.GetAsync(cancellationToken: cancellationToken)
        )
            ?.FirstOrDefault()
            ?.Sha;

    private readonly GithubClient _githubClient = githubClientFactory.Create();
}
