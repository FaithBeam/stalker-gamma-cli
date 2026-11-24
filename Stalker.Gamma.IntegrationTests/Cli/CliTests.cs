using System.Text.Json;
using Stalker.Gamma.Models;
using Stalker.Gamma.Services;

namespace Stalker.Gamma.IntegrationTests.Cli;

/// <summary>
/// Runs the CLI the way people and GUI wrappers do: a profile from config create, then
/// full-install from an offline archive of <see cref="FakeGamma"/>, then commands on the install.
/// Nothing touches the network: the mod pack maker list is served from 127.0.0.1 and the repo
/// urls point at local folders.
/// </summary>
public class CliTests : IDisposable
{
    private readonly FakeGamma _fake = new(offlineArchive: true);
    private readonly ListServer _server = new() { Body = FakeGamma.ListText };
    private readonly CliRunner _cli;

    public CliTests() => _cli = new CliRunner(Path.Join(_fake.Root, "appdata"), _fake.Root);

    public void Dispose()
    {
        _server.Dispose();
        _fake.Dispose();
    }

    private static void AssertSucceeded(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.ToString());
        Assert.True(result.Result.GetProperty("success").GetBoolean(), result.ToString());
    }

    private string Relative(string path) => Path.GetRelativePath(_fake.Root, path);

    /// <param name="stalkerGammaRepoUrl">Defaults to the fixture's Stalker_GAMMA repo</param>
    private async Task CreateProfileAsync(string? stalkerGammaRepoUrl = null)
    {
        AssertSucceeded(
            await _cli.RunAsync(
                "config",
                "create",
                // relative to the working directory, which is the fixture's root
                "--anomaly",
                Relative(_fake.Anomaly),
                "--gamma",
                Relative(_fake.Gamma),
                "--cache",
                Relative(_fake.Cache),
                "--mod-pack-maker-url",
                _server.Url,
                // update check compares the installed repos with these local folders
                "--gamma-setup-repo-url",
                _fake.RepoPath("gamma_setup"),
                "--stalker-gamma-repo-url",
                stalkerGammaRepoUrl ?? _fake.RepoPath("Stalker_GAMMA"),
                "--gamma-large-files-repo-url",
                _fake.RepoPath("gamma_large_files_v2"),
                "--teivaz-anomaly-gunslinger-repo-url",
                _fake.RepoPath("teivaz_anomaly_gunslinger"),
                "--json"
            )
        );
    }

    private async Task<CliResult> FullInstallAsync(params string[] extraArgs)
    {
        var archive = _fake.CreateOfflineArchive("offline.zip");
        return await _cli.RunAsync([
            "full-install",
            "--offline-archive-path",
            archive,
            "--json",
            .. extraArgs,
        ]);
    }

    private string ModList => Path.Join(_fake.Gamma, "profiles", "G.A.M.M.A", "modlist.txt");

    [SevenZipFact]
    public async Task Config_create_saves_the_profile_with_full_paths()
    {
        await CreateProfileAsync();

        var result = await _cli.RunAsync("config", "--json");

        AssertSucceeded(result);
        var message = result.MessagesOfType("profile").Single();
        Assert.Equal(
            Path.Join(_cli.AppData, "settings.json"),
            message.GetProperty("settingsPath").GetString()
        );
        Assert.True(File.Exists(Path.Join(_cli.AppData, "settings.json")));
        var profile = message.GetProperty("profile");
        AssertSameFolder(_fake.Anomaly, profile.GetProperty("anomaly").GetString()!);
        AssertSameFolder(_fake.Gamma, profile.GetProperty("gamma").GetString()!);
        AssertSameFolder(_fake.Cache, profile.GetProperty("cache").GetString()!);
    }

    /// <summary>
    /// The CLI resolves relative paths against its working directory, which may be spelled
    /// differently, e.g. /private/var for /var on macOS
    /// </summary>
    private static void AssertSameFolder(string expected, string stored)
    {
        Assert.True(Path.IsPathFullyQualified(stored), stored);
        Directory.CreateDirectory(expected);
        var marker = Path.Join(expected, $"{Guid.NewGuid():N}.marker");
        File.WriteAllText(marker, "");
        try
        {
            Assert.True(File.Exists(Path.Join(stored, Path.GetFileName(marker))), stored);
        }
        finally
        {
            File.Delete(marker);
        }
    }

    [SevenZipFact]
    public async Task Commands_that_need_a_profile_fail_without_one()
    {
        var result = await _cli.RunAsync("full-install", "--json");

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(Directory.Exists(Path.Join(_fake.Gamma, "mods")));
    }

    [SevenZipFact]
    public async Task Full_install_from_an_offline_archive()
    {
        await CreateProfileAsync();

        var result = await FullInstallAsync();

        AssertSucceeded(result);
        Assert.Equal(
            "simple",
            File.ReadAllText(
                _fake.ModFile("2- Simple Addon - Author A", "gamedata", "scripts", "simple.script")
            )
        );
        Assert.Equal(
            "core",
            File.ReadAllText(_fake.ModFile("G.A.M.M.A. Core", "gamedata", "configs", "core.ltx"))
        );
        Assert.Equal(
            "anomaly exe",
            File.ReadAllText(Path.Join(_fake.Anomaly, "bin", "AnomalyDX11.exe"))
        );
        // the lists come from the archive
        Assert.Equal(FakeGamma.ModListText, File.ReadAllText(ModList));
        // GUI wrappers show progress and warnings from the JSON
        Assert.NotEmpty(result.MessagesOfType("progress"));
        var warnings = result
            .MessagesOfType("log")
            .Where(x => x.GetProperty("level").GetString() == "Warning")
            .Select(x => x.GetProperty("message").GetString()!)
            .ToList();
        Assert.Contains(warnings, w => w.StartsWith("Metadata Addon was not installed"));
        Assert.Contains(warnings, w => w.StartsWith("Missing Addon was not installed"));
        // a finished install leaves nothing to resume
        Assert.False(File.Exists(ResumeState.GetPath(_fake.Gamma)));
        Assert.NotEmpty(Directory.EnumerateFiles(Path.Join(_cli.AppData, "logs")));
    }

    [SevenZipFact]
    public async Task Missing_offline_archive_is_an_error()
    {
        await CreateProfileAsync();

        var result = await _cli.RunAsync(
            "full-install",
            "--offline-archive-path",
            Path.Join(_fake.Root, "missing.zip"),
            "--json"
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(Directory.Exists(Path.Join(_fake.Gamma, "mods")));
    }

    [SevenZipFact]
    public async Task Failed_install_resumes_where_it_stopped()
    {
        await CreateProfileAsync();
        // the repos are installed after every addon, so all of them are done when it fails
        _fake.SetRepoBranch("Stalker_GAMMA", exists: false);

        var failed = await FullInstallAsync();

        Assert.Equal(1, failed.ExitCode);
        Assert.False(failed.Result.GetProperty("success").GetBoolean());
        Assert.Contains("Stalker_GAMMA", failed.Result.GetProperty("error").GetString());
        var resumePath = ResumeState.GetPath(_fake.Gamma);
        Assert.Contains("Complete\tSimple Addon | simple_addon.zip", File.ReadAllLines(resumePath));
        Assert.False(Directory.Exists(_fake.ModFile("G.A.M.M.A. Core")));

        // left in an addon that's already installed, so it's only kept if the addon is skipped
        var marker = _fake.ModFile("2- Simple Addon - Author A", "gamedata", "marker.txt");
        File.WriteAllText(marker, "");
        _fake.SetRepoBranch("Stalker_GAMMA", exists: true);

        var resumed = await FullInstallAsync("--resume");

        AssertSucceeded(resumed);
        Assert.True(File.Exists(marker));
        Assert.Equal(
            "core",
            File.ReadAllText(_fake.ModFile("G.A.M.M.A. Core", "gamedata", "configs", "core.ltx"))
        );
        Assert.False(File.Exists(resumePath));
    }

    [SevenZipFact]
    public async Task Mo2_mod_commands_edit_the_installed_modlist()
    {
        await CreateProfileAsync();
        AssertSucceeded(await FullInstallAsync());

        AssertSucceeded(
            await _cli.RunAsync("mo2", "mod", "disable", "2- Simple Addon - Author A", "--json")
        );
        AssertSucceeded(
            await _cli.RunAsync("mo2", "mod", "enable", "9- Missing Addon - Author D", "--json")
        );

        var lines = File.ReadAllLines(ModList);
        Assert.Contains("-2- Simple Addon - Author A", lines);
        Assert.Contains("+9- Missing Addon - Author D", lines);
        Assert.Contains("+6- Exos - arti", lines);

        var status = await _cli.RunAsync(
            "mo2",
            "mod",
            "status",
            "2- Simple Addon - Author A",
            "--json"
        );
        AssertSucceeded(status);
        Assert.Contains("Disabled", status.StdOut);
    }

    [SevenZipFact]
    public async Task Update_check_lists_what_changed_in_the_online_list()
    {
        await CreateProfileAsync();
        AssertSucceeded(await FullInstallAsync());
        var lines = FakeGamma.ListLines.ToArray();
        // a new version of an addon has a new archive, so a new MD5
        lines[1] = lines[1]
            .Replace("simple_addon.zip", "simple_addon_v2.zip")
            .Replace("00000000000000000000000000000001", "10000000000000000000000000000001");
        _server.Body = string.Join(
            '\n',
            [
                .. lines,
                FakeGamma.Line(
                    "https://www.moddb.com/addons/start/100005",
                    "0",
                    " - Author E",
                    " New Addon",
                    "https://www.moddb.com/mods/stalker-anomaly/addons/new-addon",
                    "new_addon.zip",
                    "00000000000000000000000000000005"
                ),
            ]
        );

        var result = await _cli.RunAsync("update", "check", "--json");

        AssertSucceeded(result);
        Assert.Equal(
            [
                ("Added", "New Addon", null, "new_addon.zip"),
                ("Modified", "Simple Addon", "simple_addon.zip", "simple_addon_v2.zip"),
            ],
            Updates(result)
        );
    }

    [SevenZipFact]
    public async Task Update_check_finds_nothing_right_after_an_install()
    {
        await CreateProfileAsync();
        AssertSucceeded(await FullInstallAsync());

        var result = await _cli.RunAsync("update", "check", "--json");

        AssertSucceeded(result);
        Assert.Empty(Updates(result));
        Assert.Empty(Warnings(result));
    }

    [SevenZipFact]
    public async Task Update_check_lists_a_repo_with_new_commits()
    {
        await CreateProfileAsync();
        AssertSucceeded(await FullInstallAsync());
        var installed = GitService.GetCommitSha(
            Path.Join(_fake.Gamma, "downloads", "Stalker_GAMMA.git")
        );
        var latest = _fake.AddUpstreamCommit(
            "Stalker_GAMMA",
            "G.A.M.M.A/modpack_addons/G.A.M.M.A. Core/gamedata/configs/new.ltx",
            "new"
        );

        var result = await _cli.RunAsync("update", "check", "--json");

        AssertSucceeded(result);
        Assert.Equal([("Modified", "Stalker_GAMMA", installed[..7], latest[..7])], Updates(result));
    }

    [SevenZipFact]
    public async Task Update_check_warns_about_a_repo_it_cant_check()
    {
        // e.g. a mirror that's down, or a typo in the url
        var unreachable = Path.Join(_fake.Root, "no-such-repo");
        await CreateProfileAsync(stalkerGammaRepoUrl: unreachable);
        AssertSucceeded(await FullInstallAsync());

        var result = await _cli.RunAsync("update", "check", "--json");

        // it isn't reported as an update when its latest commit is unknown
        AssertSucceeded(result);
        Assert.Empty(Updates(result));
        var warning = Assert.Single(Warnings(result));
        Assert.StartsWith("Unable to check Stalker_GAMMA for updates", warning);
    }

    private static List<(string? Status, string? Name, string? Old, string? New)> Updates(
        CliResult result
    ) =>
        result
            .MessagesOfType("updates")
            .Single()
            .GetProperty("updates")
            .EnumerateArray()
            .Select(x =>
                (
                    Status: x.GetProperty("status").GetString(),
                    Name: x.GetProperty("addonName").GetString(),
                    Old: Text(x, "oldZipName"),
                    New: Text(x, "newZipName")
                )
            )
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

    private static List<string> Warnings(CliResult result) =>
        result
            .MessagesOfType("log")
            .Where(x => x.GetProperty("level").GetString() == "Warning")
            .Select(x => x.GetProperty("message").GetString()!)
            .ToList();

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
