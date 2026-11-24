using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Stalker.Gamma.Extensions;
using Stalker.Gamma.Factories;
using Stalker.Gamma.ModDb.Models;
using Stalker.Gamma.Models;
using static Stalker.Gamma.Tests.ModPackMakerLines;

namespace Stalker.Gamma.Tests;

public class DownloadableRecordFactoryTests : IDisposable
{
    private readonly TempDir _gamma = new();
    private readonly ServiceProvider _services;
    private readonly IDownloadableRecordFactory _factory;

    public DownloadableRecordFactoryTests()
    {
        // the app's own registrations; nothing here touches the network until a record downloads
        _services = new ServiceCollection().RegisterCoreGammaServices().BuildServiceProvider();
        _factory = _services.GetRequiredService<IDownloadableRecordFactory>();
    }

    public void Dispose()
    {
        _services.Dispose();
        _gamma.Dispose();
    }

    /// <summary>
    /// Parses the lines as one list, so each record gets its line number
    /// </summary>
    private List<IDownloadableRecord> Create(params string[] lines) =>
        new ModPackMakerRecordFactory()
            .Create(string.Join('\n', lines))
            .Select(rec => _factory.TryCreate(_gamma.Path, rec, out var dlRec) ? dlRec : null)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

    private static string UrlHash(string url) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..8];

    [Fact]
    public void ModDb_addon_with_an_archive_name()
    {
        var record = Assert.IsType<ModDbRecord>(Assert.Single(Create(MainMenuTheme)));

        Assert.Equal("Main Menu Theme - Deathcard Cabin", record.Name);
        Assert.Equal("Groks_main_menu_theme.zip", record.ArchiveName);
        Assert.Equal(_gamma.Join("downloads", "Groks_main_menu_theme.zip"), record.DownloadPath);
        Assert.Equal("1- Main Menu Theme - Deathcard Cabin - Grokitach", record.OutputDirName);
    }

    [Fact]
    public void ModDb_addon_without_an_archive_name_looks_it_up_while_downloading()
    {
        var record = Assert.IsType<ModDbRecordGetMetadata>(
            Assert.Single(Create(EftAimRattleMetadata))
        );

        Assert.Equal("https://www.moddb.com/addons/start/209799", record.StartLink);
        Assert.Null(record.ArchiveName);
    }

    [Fact]
    public void GitHub_addon_archive_is_named_after_the_repo()
    {
        var record = Assert.IsType<GithubRecord>(Assert.Single(Create(MagsRedux)));

        Assert.Equal("MagsRedux.zip", record.ArchiveName);
        Assert.Equal("1- Anomaly Magazines - RavenAscendant", record.OutputDirName);
    }

    [Fact]
    public void Separators_and_unknown_links_are_not_addons()
    {
        var unknown = Line("https://example.com/addon.zip", "0", " - Someone", " Unknown host");

        Assert.Empty(Create(AudioSeparator, unknown));
    }

    [Theory]
    [InlineData("https://github.com/")]
    [InlineData("https://github.com/owner-only")]
    [InlineData("github.com/owner/repo")]
    public void Malformed_github_link_is_a_clear_error(string link)
    {
        var line = Line(link, "0", " - Someone", " Broken addon");

        var e = Assert.Throws<DownloadableRecordFactoryException>(() => Create(line));

        Assert.Contains("Invalid github link", e.Message);
        Assert.Contains(link, e.Message);
    }

    [Fact]
    public void GitHub_addon_without_an_author_is_an_error()
    {
        var line = Line(
            "https://github.com/owner/repo/archive/refs/heads/main.zip",
            "0",
            "",
            " No author"
        );

        Assert.Throws<DownloadableRecordFactoryException>(() => Create(line));
    }

    [Fact]
    public void Addons_from_one_github_link_share_one_download()
    {
        var records = Create(PoweredExos, PoweredExosServoPatch);

        var group = Assert.Single(_factory.CreateGroupedDownloadableRecords(records));

        Assert.IsType<GithubRecordGroup>(group);
        Assert.Equal("anomaly-exo.zip", group.ArchiveName);
    }

    [Fact]
    public void GitHub_links_to_repos_with_the_same_name_get_their_own_archive()
    {
        // tags/latest.zip and heads/master.zip are both named anomaly-exo.zip, but they're
        // different downloads: the master zip holds anomaly-exo-master/, which the tag zip doesn't
        var records = Create(PoweredExos, PoweredExosServoPatch, PoweredExosMaster);

        var groups = _factory.CreateGroupedDownloadableRecords(records);

        var masterArchive =
            $"anomaly-exo-{UrlHash("https://github.com/ahuyn/anomaly-exo/archive/refs/heads/master.zip")}.zip";
        Assert.Equal(["anomaly-exo.zip", masterArchive], groups.Select(x => x.ArchiveName));
        Assert.Equal(_gamma.Join("downloads", masterArchive), groups[1].DownloadPath);
        // the records themselves download to and extract from the renamed archive
        Assert.Equal(
            ["anomaly-exo.zip", "anomaly-exo.zip", masterArchive],
            records.Select(x => x.ArchiveName)
        );
    }

    [Fact]
    public void Archive_names_that_differ_only_by_case_count_as_the_same()
    {
        // the cache may be on a case-insensitive file system, where these would be one file
        var fork = Line(
            "https://github.com/someone/magsredux/archive/refs/heads/main.zip",
            "magsredux-main",
            " - Someone",
            " Mags fork"
        );

        var groups = _factory.CreateGroupedDownloadableRecords(Create(MagsRedux, fork));

        Assert.Equal(2, groups.Count);
        Assert.Equal("MagsRedux.zip", groups[0].ArchiveName);
        Assert.StartsWith("magsredux-", groups[1].ArchiveName);
    }

    [Fact]
    public void Addons_from_one_moddb_archive_share_one_download()
    {
        var records = Create(MainMenuTheme, StashOverhaul, StashOverhaulTbFix);

        var groups = _factory.CreateGroupedDownloadableRecords(records);

        Assert.All(groups, g => Assert.IsType<ModDbRecordGroup>(g));
        Assert.Equal(
            ["Groks_main_menu_theme.zip", "2.2.0_Groks_Stash_Overhaul.1.zip"],
            groups.Select(x => x.ArchiveName)
        );
    }

    [Fact]
    public void Archive_of_a_record_without_an_archive_name_never_exists()
    {
        // until ModDB is asked, the record's download path is the downloads folder itself
        var record = Assert.Single(Create(EftAimRattleMetadata));
        _gamma.WriteFile(Path.Join("downloads", "other_addon.zip"), "keep me");

        Assert.Equal(_gamma.Join("downloads"), record.DownloadPath);
        Assert.False(record.ArchiveExists());
    }

    [Fact]
    public void Deleting_the_archive_of_a_record_without_an_archive_name_keeps_the_downloads_folder()
    {
        var record = Assert.Single(Create(EftAimRattleMetadata));
        var otherArchive = _gamma.WriteFile(Path.Join("downloads", "other_addon.zip"), "keep me");

        // the install wraps records like this, e.g. for --skip-extract-on-hash-match and --minimal
        _factory.CreateSkipExtractWhenNotDownloadedRecord(record).DeleteArchive();
        _factory.CreateSkippedRecord(record).DeleteArchive();
        record.DeleteArchive();

        Assert.True(File.Exists(otherArchive));
    }

    [Fact]
    public void Wrapped_records_see_an_archive_name_assigned_later()
    {
        var record = Assert.IsType<ModDbRecordGetMetadata>(
            Assert.Single(Create(EftAimRattleMetadata))
        );
        var skipped = _factory.CreateSkippedRecord(record);
        var skipExtract = _factory.CreateSkipExtractWhenNotDownloadedRecord(record);

        record.ArchiveName = "EFT_Aim_Rattle_1.2.zip";
        var archive = _gamma.WriteFile(Path.Join("downloads", "EFT_Aim_Rattle_1.2.zip"));

        Assert.Equal(archive, skipped.DownloadPath);
        Assert.Equal(archive, skipExtract.DownloadPath);
        Assert.True(skipped.ArchiveExists());
        Assert.Equal(((IDownloadableRecord)record).ResumeKey, skipExtract.ResumeKey);
    }

    [Fact]
    public void DeleteArchive_deletes_a_known_archive()
    {
        var record = Assert.Single(Create(MainMenuTheme));
        var archive = _gamma.WriteFile(Path.Join("downloads", "Groks_main_menu_theme.zip"));

        Assert.True(record.ArchiveExists());
        record.DeleteArchive();

        Assert.False(File.Exists(archive));
        Assert.True(Directory.Exists(_gamma.Join("downloads")));
    }

    [Fact]
    public void Resume_key_identifies_the_addon_and_its_archive()
    {
        var record = Assert.Single(Create(MainMenuTheme));

        Assert.Equal(
            "Main Menu Theme - Deathcard Cabin | Groks_main_menu_theme.zip",
            record.ResumeKey
        );
    }
}
