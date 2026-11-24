using System.Text.Json;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Models;
using static Stalker.Gamma.Tests.ModPackMakerLines;

namespace Stalker.Gamma.Tests;

/// <summary>
/// The list an install was made from is saved as modpack_maker_list.json in the MO2 profile, and
/// update check diffs the online list against it
/// </summary>
public class InstalledModListTests : IDisposable
{
    private readonly TempDir _gamma = new();
    private const string Profile = "G.A.M.M.A";

    public void Dispose() => _gamma.Dispose();

    private static string Serialize(List<ModPackMakerRecord> records) =>
        JsonSerializer.Serialize(records, ModPackMakerCtx.Default.ListModPackMakerRecord);

    private static List<ModPackMakerRecord> Deserialize(string json) =>
        JsonSerializer.Deserialize(json, ModPackMakerCtx.Default.ListModPackMakerRecord)!;

    [Fact]
    public void Instructions_are_stored_as_an_array()
    {
        var record = new ModPackMakerRecord
        {
            DlLink = "https://www.moddb.com/addons/start/1",
            Instructions = @"main:optional\patch",
        };

        var json = Serialize([record]);

        Assert.Contains(@"""instructions"": [", json);
        Assert.Equal(record.Instructions, Assert.Single(Deserialize(json)).Instructions);
    }

    [Fact]
    public void No_instructions_round_trip_as_zero()
    {
        var json = Serialize([new ModPackMakerRecord { DlLink = "x", Instructions = "0" }]);

        Assert.Contains(@"""instructions"": []", json);
        Assert.Equal("0", Assert.Single(Deserialize(json)).Instructions);
    }

    [Fact]
    public void Patch_is_stored_without_its_dash()
    {
        var json = Serialize([new ModPackMakerRecord { DlLink = "x", Patch = "- arti" }]);

        Assert.Contains(@"""patch"": ""arti""", json);
        Assert.Equal("- arti", Assert.Single(Deserialize(json)).Patch);
    }

    [Fact]
    public void Counter_is_not_stored()
    {
        var json = Serialize([new ModPackMakerRecord { DlLink = "x", Counter = 5 }]);

        Assert.DoesNotContain("counter", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_list_explains_how_to_create_it()
    {
        var e = await Assert.ThrowsAsync<GetStalkerModsFromLocalException>(() =>
            new GetStalkerModsFromLocal().GetMods(_gamma.Path, Profile)
        );

        Assert.Contains("full-install", e.Message);
    }

    [Fact]
    public async Task Read_records_are_numbered_like_the_online_list()
    {
        var online = new ModPackMakerRecordFactory().Create(
            string.Join('\n', AudioSeparator, MainMenuTheme, StashOverhaul, PoweredExos)
        );
        _gamma.WriteFile(
            Path.Join("profiles", Profile, "modpack_maker_list.json"),
            Serialize(online)
        );

        var installed = await new GetStalkerModsFromLocal().GetMods(_gamma.Path, Profile);

        Assert.Equal(online.Select(x => x.Counter), installed.Select(x => x.Counter));
        Assert.Equal(online, installed);
    }

    [Fact]
    public void Separators_are_records_with_only_a_link()
    {
        var records = new ModPackMakerRecordFactory().Create(
            string.Join('\n', AudioSeparator, MainMenuTheme, " Visual", PoweredExos)
        );

        var separators = new SeparatorsFactory().Create(records);

        Assert.Equal(["Audio Separator", "Visual Separator"], separators.Select(x => x.Name));
        // numbered by line like addons, so MO2 sorts them in between
        Assert.Equal(
            ["1- Audio_separator", "3- Visual_separator"],
            separators.Select(x => x.FolderName)
        );
    }

    [Fact]
    public async Task Separator_writes_a_meta_ini_with_windows_line_endings()
    {
        var separator = new Separator
        {
            Name = "Audio Separator",
            FolderName = "1- Audio_separator",
        };

        await separator.WriteAsync(_gamma.Path);

        var metaIni = await File.ReadAllTextAsync(
            _gamma.Join("mods", "1- Audio_separator", "meta.ini")
        );
        Assert.StartsWith("[General]\r\n", metaIni);
        Assert.DoesNotContain("\r\r", metaIni);
        Assert.DoesNotContain(metaIni.Replace("\r\n", ""), c => c == '\n');
    }
}
