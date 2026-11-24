using System.Text;
using LibGit2Sharp;
using Stalker.Gamma.Services;

namespace Stalker.Gamma.Tests;

/// <summary>
/// update check asks a repo that isn't on GitHub for its latest commit with
/// <see cref="GitService.GetRemoteCommitSha"/>
/// </summary>
public class GitServiceGetRemoteCommitShaTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly string _repoPath;
    private readonly string _first;
    private readonly string _second;

    public GitServiceGetRemoteCommitShaTests()
    {
        _repoPath = _dir.Join("upstream.git");
        Repository.Init(_repoPath, isBare: true);
        using var repo = new Repository(_repoPath);
        var signature = new Signature("test", "test@example.com", DateTimeOffset.UnixEpoch);
        Commit CommitFile(string contents, params Commit[] parents)
        {
            var tree = new TreeDefinition();
            tree.Add(
                "a.txt",
                repo.ObjectDatabase.CreateBlob(new MemoryStream(Encoding.UTF8.GetBytes(contents))),
                Mode.NonExecutableFile
            );
            return repo.ObjectDatabase.CreateCommit(
                signature,
                signature,
                contents,
                repo.ObjectDatabase.CreateTree(tree),
                parents,
                false
            );
        }
        var first = CommitFile("first");
        var second = CommitFile("second", first);
        _first = first.Sha;
        _second = second.Sha;
        repo.Refs.Add("refs/heads/main", second.Id);
        repo.Refs.Add("refs/heads/old", first.Id);
        repo.Tags.Add("v1", first);
        repo.Tags.Add("v1-annotated", first, signature, "release");
    }

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Branch_is_at_its_latest_commit()
    {
        Assert.Equal(_second, GitService.GetRemoteCommitSha(_repoPath, "main"));
        Assert.Equal(_first, GitService.GetRemoteCommitSha(_repoPath, "old"));
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v1-annotated")]
    public void Tag_is_at_the_commit_it_tags(string tag)
    {
        Assert.Equal(_first, GitService.GetRemoteCommitSha(_repoPath, tag));
    }

    [Fact]
    public void Full_commit_sha_is_returned_as_is()
    {
        // a profile pinned to a commit never has an update
        Assert.Equal(_first, GitService.GetRemoteCommitSha(_repoPath, _first.ToUpperInvariant()));
    }

    [Theory]
    [InlineData("dev")]
    [InlineData("abc1234")]
    public void Unknown_branch_is_null(string branch)
    {
        Assert.Null(GitService.GetRemoteCommitSha(_repoPath, branch));
    }

    [Fact]
    public void Unreachable_repo_throws()
    {
        Assert.Throws<GitUtilityException>(() =>
            GitService.GetRemoteCommitSha(_dir.Join("missing.git"), "main")
        );
    }
}
