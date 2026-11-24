using Stalker.Gamma.Factories;
using static Stalker.Gamma.Tests.ModPackMakerLines;

namespace Stalker.Gamma.Tests;

public class ModPackMakerRecordFactoryTests
{
    private readonly ModPackMakerRecordFactory _factory = new();

    [Fact]
    public void Parses_every_column_and_trims_them()
    {
        var record = Assert.Single(_factory.Create(MainMenuTheme));

        Assert.Equal("https://www.moddb.com/addons/start/222467", record.DlLink);
        Assert.Equal("0", record.Instructions);
        Assert.Equal("- Grokitach", record.Patch);
        Assert.Equal("Main Menu Theme - Deathcard Cabin", record.AddonName);
        Assert.Equal(
            "https://www.moddb.com/mods/stalker-anomaly/addons/groks-main-menu-theme-deathcard-cabin",
            record.ModDbUrl
        );
        Assert.Equal("Groks_main_menu_theme.zip", record.ZipName);
        Assert.Equal("0f4256c9545fbbeda475f82796274d98", record.Md5ModDb);
    }

    [Fact]
    public void Missing_columns_are_null()
    {
        var record = Assert.Single(_factory.Create(PoweredExos));

        Assert.Equal("Powered Exos", record.AddonName);
        Assert.Null(record.ModDbUrl);
        Assert.Null(record.ZipName);
        Assert.Null(record.Md5ModDb);
    }

    [Fact]
    public void Separator_lines_only_have_a_link()
    {
        var record = Assert.Single(_factory.Create(AudioSeparator));

        Assert.Equal("Audio", record.DlLink);
        Assert.Null(record.Instructions);
        Assert.Null(record.AddonName);
    }

    [Fact]
    public void Numbers_records_by_line_from_one()
    {
        var records = _factory.Create(
            string.Join('\n', AudioSeparator, MainMenuTheme, PoweredExos)
        );

        Assert.Equal([1, 2, 3], records.Select(x => x.Counter));
    }

    [Fact]
    public void Blank_lines_and_windows_line_endings_are_ignored()
    {
        var records = _factory.Create(
            $"{AudioSeparator}\r\n\r\n   \r\n{MainMenuTheme}\r\n{PoweredExos}\r\n"
        );

        Assert.Equal(3, records.Count);
        // blank lines don't take a number, so addon folder names stay the same
        Assert.Equal([1, 2, 3], records.Select(x => x.Counter));
        Assert.Equal("0f4256c9545fbbeda475f82796274d98", records[1].Md5ModDb);
    }
}
