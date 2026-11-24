using System.Reactive.Linq;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Services;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Extensions;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.GammaInstallerServices.GammaInstaller;
using Stalker.Gamma.GammaInstallerServices.SpecialRepos;
using Stalker.Gamma.Models;
using Stalker.Gamma.Services;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("update")]
public class UpdateCmds(
    ILogger logger,
    CliSettings cliSettings,
    StalkerGammaSettings stalkerGammaSettings,
    IGetStalkerModsFromApi getStalkerModsFromApi,
    IModListRecordFactory modListRecordFactory,
    GetRemoteGitRepoCommit getRemoteGitRepoCommit,
    IGammaInstaller gammaInstaller,
    UtilitiesReady utilitiesReady,
    ProgressLoggingService progressLoggingService
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
        var statusCode = 0;
        string? error = null;
        LogAndExitOnDependencyError.Check(utilitiesReady, _logger);
        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
        stalkerGammaSettings.ModpackMakerList = _cliSettings.ActiveProfile!.ModPackMakerUrl;

        try
        {
            var updateArgs = GammaInstallerArgs
                .Create(
                    _cliSettings.ActiveProfile!.Anomaly,
                    _cliSettings.ActiveProfile!.Gamma,
                    _cliSettings.ActiveProfile!.Cache
                )
                .WithCancellationToken(cancellationToken)
                .WithMo2Profile(_cliSettings.ActiveProfile!.Mo2Profile)
                .Build();
            var diffed = await gammaInstaller.DiffAddonRecordsAsync(updateArgs);

            var localGitRepoDiffs = await GetLocalGitRepoDiffs(cancellationToken);

            var diffs = diffed
                .LocalRecords.Diff(diffed.OnlineRecords)
                .Concat(localGitRepoDiffs)
                .ToList();
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

                _logger.Information("Updates available: {NumberUpdates}", diffs.Count);

                foreach (var diff in diffs)
                {
                    if (diff.DiffType == DiffType.Modified)
                    {
                        _logger.Information(
                            "{Status}: {AddonName} {OldZipName} -> {NewZipName}",
                            diff.DiffType.ToString().PadRight(padRightStatus),
                            (diff.OldListRecord!.AddonName ?? diff.OldListRecord.DlLink).PadRight(
                                padRightAddonName
                            ),
                            (diff.OldListRecord.ZipName ?? "N/A").PadRight(padRightOldZipName),
                            diff.NewListRecord!.ZipName ?? "N/A"
                        );
                    }
                    else
                    {
                        _logger.Information(
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
                            $"{diff.OldListRecord?.ZipName ?? "N/A"}".PadRight(padRightOldZipName),
                            $"{diff.NewListRecord?.ZipName ?? "N/A"}"
                        );
                    }
                }

                _logger.Information("To apply updates, run `stalker-gamma update apply`");
            }
            else
            {
                _logger.Information("No updates found");
            }
        }
        catch (Exception e)
        {
            progressLoggingService.WriteToLogFile();
            _logger.Error(e, "Update check failed! {ExceptionMessage}", e.Message);
            statusCode = 1;
            error = e.Message;
        }
        finally
        {
            progressLoggingService.WriteToLogFile();
        }

        if (json)
        {
            JsonOutputUtility.WriteResult(CheckCommand, statusCode, error);
        }

        return statusCode;
    }

    private async Task<List<ModPackMakerRecordDiff>> GetLocalGitRepoDiffs(
        CancellationToken cancellationToken
    )
    {
        var profile = _cliSettings.ActiveProfile!;
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

        _logger.Warning(
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
    /// <param name="verbose"></param>
    /// <param name="minimal"></param>
    /// <param name="preserveUserSettings">Preserve user settings (user.ltx)</param>
    /// <param name="preserveMcmSettings">Preserve MCM settings</param>
    /// <param name="stalkerGammaServerUrl">URL to the experimental stalker gamma cli server when connecting to moddb for addons</param>
    /// <param name="resume">Resume a previously failed update apply from resume.txt in your gamma folder</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    /// <param name="progressUpdateIntervalMs"></param>
    public async Task<int> Apply(
        CancellationToken cancellationToken,
        bool verbose = false,
        bool minimal = false,
        bool preserveUserSettings = false,
        bool preserveMcmSettings = false,
        string? stalkerGammaServerUrl = null,
        bool resume = false,
        bool json = false,
        [Hidden] long progressUpdateIntervalMs = 250
    )
    {
        var statusCode = 0;
        string? error = null;

        LogAndExitOnDependencyError.Check(utilitiesReady, _logger);

        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
        ValidateActiveProfile.ValidateDownloadThreads(_logger, _cliSettings.ActiveProfile!);

        InitializeSettings(
            stalkerGammaServerUrl,
            out var anomaly,
            out var gamma,
            out var cache,
            out var mo2Profile
        );

        SetUpLogging(
            verbose,
            json,
            progressUpdateIntervalMs,
            out var gammaProgressDisposable,
            out var gammaWriteFileDisposable
        );

        ResumeState? resumeState = null;
        try
        {
            resumeState = await ResumeUtility.LoadAsync(
                _logger,
                resume,
                gamma,
                ApplyResumeCommand,
                cancellationToken
            );
            var updateArgs = GammaInstallerArgs
                .Create(anomaly, gamma, cache)
                .WithCancellationToken(cancellationToken)
                .WithMo2Profile(mo2Profile)
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
            _logger.Information("Update finished");
        }
        catch (Exception e)
        {
            progressLoggingService.WriteToLogFile();
            _logger.Error(e, "Update failed! {ExceptionMessage}", e.Message);
            if (resumeState is not null)
            {
                ResumeUtility.Write(_logger, resumeState, gamma);
            }
            statusCode = 1;
            error = e.Message;
        }
        finally
        {
            progressLoggingService.WriteToLogFile();
            gammaWriteFileDisposable.Dispose();
            gammaProgressDisposable.Dispose();
        }

        if (json)
        {
            JsonOutputUtility.WriteResult(ApplyResumeCommand, statusCode, error);
        }

        return statusCode;
    }

    private void InitializeSettings(
        string? stalkerGammaServerUrl,
        out string anomaly,
        out string gamma,
        out string cache,
        out string mo2Profile
    )
    {
        stalkerGammaSettings.ModpackMakerList = _cliSettings.ActiveProfile!.ModPackMakerUrl;
        anomaly = _cliSettings.ActiveProfile!.Anomaly;
        gamma = _cliSettings.ActiveProfile!.Gamma;
        cache = _cliSettings.ActiveProfile!.Cache;
        mo2Profile = _cliSettings.ActiveProfile!.Mo2Profile;
        var modpackMakerUrl = _cliSettings.ActiveProfile!.ModPackMakerUrl;
        var modListUrl = _cliSettings.ActiveProfile!.ModListUrl;
        var profile = _cliSettings.ActiveProfile!;
        stalkerGammaSettings.DownloadThreads = profile.DownloadThreads;
        stalkerGammaSettings.ModpackMakerList = modpackMakerUrl;
        stalkerGammaSettings.ModListUrl = modListUrl;
        // the special repos are fetched and extracted from these, same as full-install
        stalkerGammaSettings.GammaSetupRepo = profile.GammaSetupRepoUrl;
        stalkerGammaSettings.GammaSetupRepoBranch = profile.GammaSetupRepoBranch;
        stalkerGammaSettings.StalkerGammaRepo = profile.StalkerGammaRepoUrl;
        stalkerGammaSettings.StalkerGammaRepoBranch = profile.StalkerGammaRepoBranch;
        stalkerGammaSettings.GammaLargeFilesRepo = profile.GammaLargeFilesRepoUrl;
        stalkerGammaSettings.GammaLargeFilesRepoBranch = profile.GammaLargeFilesRepoBranch;
        stalkerGammaSettings.TeivazAnomalyGunslingerRepo = profile.TeivazAnomalyGunslingerRepoUrl;
        stalkerGammaSettings.TeivazAnomalyGunslingerRepoBranch =
            profile.TeivazAnomalyGunslingerRepoBranch;
        // ModDB requests only go through the server when this is set
        stalkerGammaSettings.StalkerGammaServerUrl = stalkerGammaServerUrl;
    }

    private void SetUpLogging(
        bool verbose,
        bool json,
        long progressUpdateIntervalMs,
        out IDisposable gammaProgressDisposable,
        out IDisposable gammaWriteFileDisposable
    )
    {
        gammaInstaller.Progress.Warning += OnWarning;

        var gammaWriteFileObs = Observable
            .FromEventPattern<GammaProgress.GammaInstallProgressEventArgs>(
                handler => gammaInstaller.Progress.ProgressChanged += handler,
                handler => gammaInstaller.Progress.ProgressChanged -= handler
            )
            .Select(x => x.EventArgs);
        gammaWriteFileDisposable = gammaWriteFileObs.Subscribe(
            progressLoggingService.OnProgressChangedWriteToFile
        );

        var gammaProgressObservable = Observable
            .FromEventPattern<GammaProgress.GammaInstallProgressEventArgs>(
                handler => gammaInstaller.Progress.ProgressChanged += handler,
                handler => gammaInstaller.Progress.ProgressChanged -= handler
            )
            .Select(x => x.EventArgs);
        gammaProgressDisposable = gammaProgressObservable.SubscribeSampledOnDedicatedThread(
            TimeSpan.FromMilliseconds(progressUpdateIntervalMs),
            json ? JsonOutputUtility.WriteProgress
                : verbose ? OnProgressChangedVerbose
                : OnProgressChangedInformational
        );
    }

    private void OnWarning(object? sender, GammaProgress.GammaInstallWarningEventArgs e) =>
        _logger.Warning(e.Exception, "{Message}", e.Message);

    private void OnProgressChangedInformational(GammaProgress.GammaInstallProgressEventArgs e) =>
        _logger.Information(
            Informational,
            DateTimeOffset.Now.ToString("HH:mm:ss"),
            e.Name[..Math.Min(e.Name.Length, 35)].PadRight(40),
            e.ProgressType.GetHumanReadableString().PadRight(10),
            e.GetHumanReadableProgress().PadRight(10),
            $"[{e.Complete}/{e.Total}]"
        );

    private void OnProgressChangedVerbose(GammaProgress.GammaInstallProgressEventArgs e) =>
        _logger.Information(
            Verbose,
            e.Name[..Math.Min(e.Name.Length, 35)].PadRight(40),
            e.ProgressType.GetHumanReadableString().PadRight(10),
            e.GetHumanReadableProgress().PadRight(10),
            $"[{e.Complete}/{e.Total}]",
            e.Url
        );

    private const string CheckCommand = "update check";
    private const string ApplyResumeCommand = "update apply";

    private const string Informational =
        "\e[97m[{DateTime}]\e[0m "
        + "\e[96m{AddonName}\e[0m "
        + "\e[97m|\e[0m "
        + "\e[96m{Operation}\e[0m "
        + "\e[97m|\e[0m "
        + "\e[96m{Percent}\e[0m "
        + "\e[97m|\e[0m "
        + "\e[96m{CompleteTotal}\e[0m";
    private const string Verbose =
        "{AddonName} | {Operation} | {Percent} | {CompleteTotal} | {Url}";

    private readonly ILogger _logger = logger;
    private readonly CliSettings _cliSettings = cliSettings;
    private readonly IGetStalkerModsFromApi _getStalkerModsFromApi = getStalkerModsFromApi;
    private readonly IModListRecordFactory _modListRecordFactory = modListRecordFactory;
}
