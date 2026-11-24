using Microsoft.Extensions.DependencyInjection;
using Stalker.Gamma.Extensions;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.GammaInstallerServices.GammaInstaller;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.IntegrationTests;

/// <summary>
/// Installs <see cref="FakeGamma"/> with the offline installer, wired the way full-install
/// --offline-archive-path wires it, and checks what ends up on disk. Nothing touches the network.
/// </summary>
public class OfflineInstallTests : IDisposable
{
    private readonly FakeGamma _fake = new();
    private readonly List<string> _warnings = [];

    public void Dispose() => _fake.Dispose();

    private async Task InstallAsync(bool minimal = false)
    {
        NativeSevenZip.EnsureLoaded();
        await using var services = new ServiceCollection()
            .RegisterCoreGammaServices()
            .BuildServiceProvider();
        services.GetRequiredService<StalkerGammaSettings>().DownloadThreads = 2;
        services.GetRequiredService<GammaProgress>().Warning += (_, e) => _warnings.Add(e.Message);
        await using var scope = services.CreateAsyncScope();
        var installer = scope.ServiceProvider.GetRequiredService<OfflineGammaInstaller>();

        // the same steps as FullInstall.cs
        var args = GammaInstallerArgs
            .Create(_fake.Anomaly, _fake.Gamma, _fake.Cache)
            .WithOffline()
            .WithMinimal(minimal)
            .WithModPackMakerPath(_fake.ModPackMakerList)
            .WithModListPath(_fake.ModList)
            .Build();
        args.GroupedAddonRecords = await installer.BuildGroupedAddonRecordsAsync(args);
        args.AnomalyRecord = installer.BuildAnomalyRecord(args);
        installer.BuildSpecialRepoRecords(args);
        await installer.InstallAsync(args);
    }

    private static string Read(string path) => File.ReadAllText(path);

    [SevenZipFact]
    public async Task Installs_anomaly()
    {
        await InstallAsync();

        Assert.Equal("anomaly exe", Read(Path.Join(_fake.Anomaly, "bin", "AnomalyDX11.exe")));
        Assert.Equal(
            "anomaly",
            Read(Path.Join(_fake.Anomaly, "gamedata", "configs", "system.ltx"))
        );
        // ReShade and the shader cache would break the game after an install
        Assert.False(File.Exists(Path.Join(_fake.Anomaly, "bin", "dxgi.dll")));
        Assert.False(File.Exists(Path.Join(_fake.Anomaly, "bin", "d3d9.dll")));
        Assert.False(Directory.Exists(Path.Join(_fake.Anomaly, "appdata", "shaders_cache", "r4")));
        Assert.Equal(
            "rs_screenmode borderless\nsnd_volume_eff 1",
            Read(Path.Join(_fake.Anomaly, "appdata", "user.ltx"))
        );
    }

    [SevenZipFact]
    public async Task Installs_each_addon_into_its_numbered_folder()
    {
        await InstallAsync();

        var simple = Path.Join(_fake.Mods, "2- Simple Addon - Author A");
        Assert.Equal("simple", Read(Path.Join(simple, "gamedata", "scripts", "simple.script")));
        // folders other than the game's are cleaned out, and so are dotfiles
        Assert.False(Directory.Exists(Path.Join(simple, "docs")));
        Assert.False(File.Exists(Path.Join(simple, ".gitattributes")));
        Assert.Contains("installationFile=simple_addon.zip", Read(Path.Join(simple, "meta.ini")));
    }

    [SevenZipFact]
    public async Task Addons_sharing_an_archive_each_follow_their_own_instructions()
    {
        await InstallAsync();

        Assert.Equal(
            "main",
            Read(_fake.ModFile("3- Stash - Author B", "gamedata", "configs", "stash.ltx"))
        );
        Assert.False(
            File.Exists(_fake.ModFile("3- Stash - Author B", "gamedata", "configs", "fix_only.ltx"))
        );
        Assert.Equal(
            "fix",
            Read(_fake.ModFile("4- Stash Fix - Author B", "gamedata", "configs", "stash.ltx"))
        );
        Assert.Equal(
            "fix only",
            Read(_fake.ModFile("4- Stash Fix - Author B", "gamedata", "configs", "fix_only.ltx"))
        );
    }

    [SevenZipFact]
    public async Task Links_to_the_same_repo_install_their_own_archives()
    {
        await InstallAsync();

        Assert.Equal(
            "latest",
            Read(_fake.ModFile("6- Exos - arti", "gamedata", "scripts", "exo.script"))
        );
        Assert.Equal(
            "master",
            Read(_fake.ModFile("7- Exos master - arti", "gamedata", "scripts", "exo.script"))
        );
    }

    [SevenZipFact]
    public async Task Writes_separators()
    {
        await InstallAsync();

        Assert.True(File.Exists(_fake.ModFile("1- Base_separator", "meta.ini")));
        Assert.True(File.Exists(_fake.ModFile("5- Weapons_separator", "meta.ini")));
    }

    [SevenZipFact]
    public async Task Installs_the_gamma_repos()
    {
        await InstallAsync();

        // Stalker_GAMMA: addons into mods, patches into Anomaly, and its version
        Assert.Equal(
            "core",
            Read(_fake.ModFile("G.A.M.M.A. Core", "gamedata", "configs", "core.ltx"))
        );
        Assert.Equal("patch", Read(Path.Join(_fake.Anomaly, "gamedata", "configs", "patch.ltx")));
        Assert.Equal("fake 1.0", Read(Path.Join(_fake.Gamma, "version.txt")));
        Assert.False(Directory.Exists(_fake.ModFile("modpack_data")));
        // gamma_setup
        Assert.Equal(
            "setup",
            Read(_fake.ModFile("G.A.M.M.A. Setup", "gamedata", "configs", "setup.ltx"))
        );
        Assert.False(File.Exists(_fake.ModFile("README.md")));
        // gamma_large_files_v2
        Assert.Equal(
            "large",
            Read(_fake.ModFile("100- Large Files - Grokitach", "gamedata", "sounds", "big.ogg"))
        );
        // teivaz_anomaly_gunslinger: every gamedata folder is merged into one addon
        Assert.Equal(
            "a",
            Read(_fake.ModFile(FakeGamma.TeivazFolder, "gamedata", "meshes", "a.ogf"))
        );
        Assert.Equal(
            "b",
            Read(_fake.ModFile(FakeGamma.TeivazFolder, "gamedata", "meshes", "b.ogf"))
        );
    }

    [SevenZipFact]
    public async Task Sets_up_mod_organizer()
    {
        await InstallAsync();

        Assert.Equal("mo2", Read(Path.Join(_fake.Gamma, "ModOrganizer.exe")));
        var ini = Read(Path.Join(_fake.Gamma, "ModOrganizer.ini"));
        Assert.Contains("selected_profile=@ByteArray(G.A.M.M.A)", ini);
        Assert.Contains("noregister=true", Read(Path.Join(_fake.Gamma, "nxmhandler.ini")));

        var profile = Path.Join(_fake.Gamma, "profiles", "G.A.M.M.A");
        Assert.Equal(FakeGamma.ModListText, Read(Path.Join(profile, "modlist.txt")));
        Assert.Contains("LocalSettings=true", Read(Path.Join(profile, "settings.txt")));
        // update check diffs the online list against these
        Assert.Equal(FakeGamma.ListText, Read(Path.Join(profile, "modpack_maker_list.txt")));
        var local = await new GetStalkerModsFromLocal().GetMods(_fake.Gamma, "G.A.M.M.A");
        Assert.Equal(FakeGamma.ListLines.Length, local.Count);
        Assert.Equal("Simple Addon", local[1].AddonName);
    }

    [SevenZipFact]
    public async Task Addons_missing_from_the_cache_are_skipped_with_a_warning()
    {
        await InstallAsync();

        Assert.False(Directory.Exists(_fake.ModFile("8- Metadata Addon - Author C")));
        Assert.False(Directory.Exists(_fake.ModFile("9- Missing Addon - Author D")));
        Assert.Equal(2, _warnings.Count);
        Assert.Contains(_warnings, w => w.StartsWith("Metadata Addon was not installed"));
        Assert.Contains(
            _warnings,
            w =>
                w.StartsWith("Missing Addon was not installed")
                && w.EndsWith(Path.Join("downloads", "missing.zip"))
        );
    }

    [SevenZipFact]
    public async Task Installing_again_replaces_an_addons_files()
    {
        await InstallAsync();
        var stale = _fake.ModFile(
            "2- Simple Addon - Author A",
            "gamedata",
            "scripts",
            "old.script"
        );
        File.WriteAllText(stale, "from an older version of the addon");
        _fake.WriteZip("simple_addon.zip", ("gamedata/scripts/simple.script", "simple v2"));

        await InstallAsync();

        Assert.False(File.Exists(stale));
        Assert.Equal(
            "simple v2",
            Read(
                _fake.ModFile("2- Simple Addon - Author A", "gamedata", "scripts", "simple.script")
            )
        );
    }

    [SevenZipFact]
    public async Task Minimal_install_empties_the_cache()
    {
        await InstallAsync(minimal: true);

        Assert.Empty(Directory.EnumerateFileSystemEntries(_fake.Cache));
        Assert.Equal(
            "simple",
            Read(
                _fake.ModFile("2- Simple Addon - Author A", "gamedata", "scripts", "simple.script")
            )
        );
        Assert.Equal(
            "core",
            Read(_fake.ModFile("G.A.M.M.A. Core", "gamedata", "configs", "core.ltx"))
        );
    }

    [SevenZipFact]
    public async Task A_broken_archive_fails_the_install()
    {
        File.WriteAllText(_fake.CachePath("simple_addon.zip"), "not an archive");

        var e = await Assert.ThrowsAnyAsync<Exception>(() => InstallAsync());

        Assert.Contains("simple_addon.zip", e.ToString());
    }
}
