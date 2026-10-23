using Stalker.Gamma.Models;

namespace Stalker.Gamma.GammaInstallerServices.SpecialRepos;

public interface ISpecialRepo : IDownloadableRecord
{
    public string Branch { get; }

    /// <summary>
    /// Expands the repo's files into a temp directory that <see cref="IDownloadableRecord.ExtractAsync"/> moves into place
    /// </summary>
    /// <param name="ct"></param>
    /// <param name="includePath">Only expand files whose repo relative path this returns true for</param>
    public Task ExpandFilesAsync(
        CancellationToken ct = default,
        Func<string, bool>? includePath = null
    );

    /// <summary>
    /// The name of the addon folder in the mods directory a repo relative file path is extracted into,
    /// or null if it isn't extracted into one
    /// </summary>
    public string? GetModsDirName(string repoPath);

    /// <summary>
    /// The path a repo relative file path is extracted to, or null if it isn't extracted
    /// </summary>
    public string? GetDestinationPath(string repoPath);

    public static string[] SplitRepoPath(string repoPath) =>
        repoPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

    public static string NormalizeRepoPath(string repoPath) => repoPath.Replace('\\', '/');
}
