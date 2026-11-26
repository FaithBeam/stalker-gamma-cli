using System.ComponentModel.DataAnnotations;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Services;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Extensions;
using Stalker.Gamma.GammaInstallerServices.GammaInstaller;
using Stalker.Gamma.GammaInstallerServices.SpecialRepos;
using Stalker.Gamma.Models;
using Stalker.Gamma.Services;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("update")]
public class UpdateCmds(
    ILogger logger,
    CliSettings cliSettings,
    GetRemoteGitRepoCommit getRemoteGitRepoCommit,
    IGammaInstaller gammaInstaller,
    InstallCommandSupport support
)
{
    /// <summary>
    /// Check for updates
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    [Command("check")]
    public async Task<int> CheckUpdates(
        CancellationToken cancellationToken = default,
        bool json = false
    )
    {
        return await support.RunAsync(
            CheckCommand,
            "Update check failed!",
            json,
            async () =>
            {
                var profile = support.Validate(usesDownloadThreads: false);
                support.ApplySettings(profile);
                var updateArgs = GammaInstallerArgs
                    .Create(profile.Anomaly, profile.Gamma, profile.Cache)
                    .WithCancellationToken(cancellationToken)
                    .WithMo2Profile(profile.Mo2Profile)
                    .Build();
                var diffed = await gammaInstaller.DiffAddonRecordsAsync(updateArgs);

                var localGitRepoDiffs = await GetLocalGitRepoDiffs(cancellationToken);

                var diffs = diffed.Diffs.Concat(localGitRepoDiffs).ToList();
                if (json)
                {
                    JsonOutputUtility.WriteUpdates(diffs);
                }
                else if (diffs.Count > 0)
                {
                    var olds = diffs
                        .Where(x =>
                            x.OldListRecord is not null
                            && !string.IsNullOrWhiteSpace(x.OldListRecord.AddonName)
                        )
                        .Select(x => x.OldListRecord!);
                    var news = diffs
                        .Where(x =>
                            x.NewListRecord is not null
                            && !string.IsNullOrWhiteSpace(x.NewListRecord.AddonName)
                        )
                        .Select(x => x.NewListRecord!);
                    var joined = olds.Concat(news).ToList();
                    var padRightAddonName =
                        (joined.Count > 0 ? joined.Max(x => x.AddonName!.Length) : 0) + 5;
                    var padRightOldZipName =
                        diffs
                            .MaxBy(x => x.OldListRecord?.ZipName?.Length)
                            ?.OldListRecord?.ZipName?.Length
                        ?? 3;
                    var padRightStatus = nameof(DiffType.Modified).Length;

                    logger.Information("Updates available: {NumberUpdates}", diffs.Count);

                    foreach (var diff in diffs)
                    {
                        if (diff.DiffType == DiffType.Modified)
                        {
                            logger.Information(
                                "{Status}: {AddonName} {OldZipName} -> {NewZipName}",
                                diff.DiffType.ToString().PadRight(padRightStatus),
                                (
                                    diff.OldListRecord!.AddonName ?? diff.OldListRecord.DlLink
                                ).PadRight(padRightAddonName),
                                (diff.OldListRecord.ZipName ?? "N/A").PadRight(padRightOldZipName),
                                diff.NewListRecord!.ZipName ?? "N/A"
                            );
                        }
                        else
                        {
                            logger.Information(
                                "{Status}: {AddonName} {OldZipName} -> {NewZipName}",
                                diff.DiffType.ToString().PadRight(padRightStatus),
                                diff.DiffType switch
                                {
                                    DiffType.Added =>
                                        $"{diff.NewListRecord?.AddonName ?? diff.NewListRecord?.DlLink ?? "N/A"}".PadRight(
                                            padRightAddonName
                                        ),
                                    DiffType.Removed => diff.OldListRecord?.AddonName?.PadRight(
                                        padRightAddonName
                                    ),
                                    _ => throw new ArgumentOutOfRangeException(),
                                },
                                $"{diff.OldListRecord?.ZipName ?? "N/A"}".PadRight(
                                    padRightOldZipName
                                ),
                                $"{diff.NewListRecord?.ZipName ?? "N/A"}"
                            );
                        }
                    }

                    logger.Information("To apply updates, run `stalker-gamma update apply`");
                }
                else
                {
                    logger.Information("No updates found");
                }
            }
        );
    }

    private async Task<List<ModPackMakerRecordDiff>> GetLocalGitRepoDiffs(
        CancellationToken cancellationToken
    )
    {
        var profile = cliSettings.ActiveProfile!;
        var gammaDownloadsPath = Path.Join(profile.Gamma, "downloads");
        List<(string Name, string Url, string Branch)> repos =
        [
            ("gamma_setup", profile.GammaSetupRepoUrl, profile.GammaSetupRepoBranch),
            (
                "gamma_large_files_v2",
                profile.GammaLargeFilesRepoUrl,
                profile.GammaLargeFilesRepoBranch
            ),
            ("Stalker_GAMMA", profile.StalkerGammaRepoUrl, profile.StalkerGammaRepoBranch),
            (
                "teivaz_anomaly_gunslinger",
                profile.TeivazAnomalyGunslingerRepoUrl,
                profile.TeivazAnomalyGunslingerRepoBranch
            ),
        ];
        var localRepos = repos
            .Select(x => new
            {
                x.Name,
                x.Branch,
                DlLink = x.Url.TrimEnd('/'),
                Path = Path.Join(gammaDownloadsPath, $"{x.Name}.git"),
            })
            .Where(repoDir => Directory.Exists(repoDir.Path))
            .ToList();
        var remoteShas = await Task.WhenAll(
            localRepos.Select(repoDir =>
                GetRemoteCommitAsync(
                    repoDir.Name,
                    repoDir.DlLink,
                    repoDir.Branch,
                    cancellationToken
                )
            )
        );
        List<ModPackMakerRecord> localRepoModPackMakerRecs = [];
        List<ModPackMakerRecord> remoteRepoModPackMakerRecs = [];
        foreach (var (repoDir, remoteSha) in localRepos.Zip(remoteShas))
        {
            // a repo that couldn't be checked isn't reported as an update
            if (remoteSha is null)
            {
                continue;
            }
            var installedSha = GetInstalledCommit(repoDir.Path, repoDir.Branch);
            localRepoModPackMakerRecs.Add(RepoRecord(repoDir.Name, repoDir.DlLink, installedSha));
            remoteRepoModPackMakerRecs.Add(RepoRecord(repoDir.Name, repoDir.DlLink, remoteSha));
        }
        return localRepoModPackMakerRecs.Diff(remoteRepoModPackMakerRecs);
    }

    private static ModPackMakerRecord RepoRecord(string name, string url, string sha) =>
        new()
        {
            DlLink = url,
            AddonName = name,
            Md5ModDb = sha,
            ZipName = ShortSha(sha),
        };

    /// <summary>
    /// The commit the repo's branch is at upstream, from GitHub's API for GitHub repos, or
    /// else like <c>git ls-remote</c>, which works with any host or a local folder.
    /// Null, with a warning, if neither can tell.
    /// </summary>
    private async Task<string?> GetRemoteCommitAsync(
        string name,
        string url,
        string branch,
        CancellationToken cancellationToken
    )
    {
        List<string> errors = [];
        if (TryGetGithubOwnerAndRepo(url, out var owner, out var repo))
        {
            try
            {
                if (
                    await getRemoteGitRepoCommit.ExecuteAsync(
                        owner,
                        repo,
                        branch,
                        cancellationToken
                    ) is
                    { } sha
                )
                {
                    return sha;
                }
            }
            // e.g. GitHub's rate limit for requests without a token
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                errors.Add(e.Message);
            }
        }

        try
        {
            if (
                await Task.Run(
                    () => GitService.GetRemoteCommitSha(url, branch),
                    cancellationToken
                ) is
                { } sha
            )
            {
                return sha;
            }
            errors.Add($"it has no branch or tag named {branch}");
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            errors.Add(e.InnerException?.Message ?? e.Message);
        }

        logger.Warning(
            "Unable to check {Repo} for updates from {Url}: {Reason}",
            name,
            url,
            string.Join("; ", errors)
        );
        return null;
    }

    /// <summary>
    /// The commit whose files are installed: the one update apply last extracted, or else the
    /// one the configured branch is at, which is what full-install extracted
    /// </summary>
    private static string GetInstalledCommit(string repoPath, string branch)
    {
        var appliedCommitPath = UpdateSpecialRepoRecord.GetAppliedCommitPath(repoPath);
        var appliedCommit = File.Exists(appliedCommitPath)
            ? File.ReadAllText(appliedCommitPath).Trim()
            : null;
        return string.IsNullOrWhiteSpace(appliedCommit)
            ? GitService.GetCommitSha(repoPath, branch)
            : appliedCommit;
    }

    private static bool TryGetGithubOwnerAndRepo(string url, out string owner, out string repo)
    {
        owner = repo = "";
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        owner = segments[0];
        repo = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];
        return true;
    }

    private static string ShortSha(string sha) => sha.Length > 7 ? sha[..7] : sha;

    /// <summary>
    /// Apply any updates
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <param name="verbose">More verbose logging</param>
    /// <param name="minimal">Delete cache files after extracting</param>
    /// <param name="preserveUserSettings">Preserve user settings (user.ltx)</param>
    /// <param name="preserveMcmSettings">Preserve MCM settings</param>
    /// <param name="stalkerGammaServerUrl">URL to the experimental stalker gamma cli server when connecting to moddb for addons</param>
    /// <param name="downloadThreads">Override downloadThreads defined in your profile</param>
    /// <param name="addFoldersToWinDefenderExclusion">(Windows) Add the anomaly, gamma, and cache folders to the Windows Defender Exclusion list</param>
    /// <param name="enableLongPaths">(Windows) Enable long paths</param>
    /// <param name="resume">Resume a previously failed update apply from resume.txt in your gamma folder</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    /// <param name="progressUpdateIntervalMs">How frequently to write progress to the console for each download, extract, etc. in milliseconds</param>
    public async Task<int> Apply(
        CancellationToken cancellationToken,
        bool verbose = false,
        bool minimal = false,
        bool preserveUserSettings = false,
        bool preserveMcmSettings = false,
        string? stalkerGammaServerUrl = null,
        [Range(CliProfile.MinDownloadThreads, CliProfile.MaxDownloadThreads)]
            int? downloadThreads = null,
        bool addFoldersToWinDefenderExclusion = false,
        bool enableLongPaths = false,
        bool resume = false,
        bool json = false,
        [Hidden] long progressUpdateIntervalMs = 1000
    )
    {
        string? gamma = null;
        ResumeState? resumeState = null;
        return await support.RunAsync(
            ApplyResumeCommand,
            "Update failed!",
            json,
            progressReporting: support.ReportProgress(
                gammaInstaller.Progress,
                verbose,
                json,
                progressUpdateIntervalMs
            ),
            action: async () =>
            {
                var profile = support.Validate(downloadThreads: downloadThreads);
                support.ApplySettings(
                    profile,
                    downloadThreads,
                    stalkerGammaServerUrl,
                    addFoldersToWinDefenderExclusion: addFoldersToWinDefenderExclusion,
                    enableLongPaths: enableLongPaths
                );
                gamma = profile.Gamma;

                resumeState = await ResumeUtility.LoadAsync(
                    logger,
                    resume,
                    gamma,
                    ApplyResumeCommand,
                    cancellationToken
                );
                var updateArgs = GammaInstallerArgs
                    .Create(profile.Anomaly, gamma, profile.Cache)
                    .WithCancellationToken(cancellationToken)
                    .WithMo2Profile(profile.Mo2Profile)
                    .WithMinimal(minimal)
                    .WithPreserveUserLtx(preserveUserSettings)
                    .WithPreserveMcmSettings(preserveMcmSettings)
                    .WithUseStalkerGammaServerUrl(stalkerGammaServerUrl)
                    .WithResumeState(resumeState)
                    .Build();
                updateArgs.GroupedAddonRecords =
                    await gammaInstaller.BuildUpdateGroupedAddonRecordsAsync(updateArgs);
                gammaInstaller.BuildUpdateSpecialRepoRecords(updateArgs);
                await gammaInstaller.InstallAsync(updateArgs);
                ResumeUtility.Delete(gamma);
                logger.Information("Update finished");
            },
            onFailure: () =>
            {
                if (resumeState is not null)
                {
                    ResumeUtility.Write(logger, resumeState, gamma!);
                }
            }
        );
    }

    private const string CheckCommand = "update check";
    private const string ApplyResumeCommand = "update apply";
}
