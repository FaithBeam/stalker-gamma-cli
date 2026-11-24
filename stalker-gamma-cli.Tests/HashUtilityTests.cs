using System.IO.Compression;
using stalker_gamma_cli.Utilities;

namespace stalker_gamma_cli.Tests;

/// <summary>
/// debug hash zips a list of hashes of every installed file, to compare installs when
/// troubleshooting
/// </summary>
public class HashUtilityTests : IDisposable
{
    private readonly TempDir _dir = new();

    public HashUtilityTests()
    {
        _dir.WriteFile(Path.Join("anomaly", "bin", "AnomalyDX11.exe"), "exe");
        _dir.WriteFile(Path.Join("anomaly", "gamedata", "configs", "a.ltx"), "ltx");
        _dir.WriteFile(Path.Join("anomaly", "gamedata", "shaders", "cache.bin"), "shader");
        _dir.WriteFile(Path.Join("anomaly", "appdata", "savedgames", "save.scop"), "save");
        _dir.WriteFile(Path.Join("anomaly", "appdata", "logs", "xray.log"), "log");
        _dir.WriteFile(Path.Join("anomaly", "appdata", "user.ltx"), "user");
        _dir.WriteFile(Path.Join("gamma", "ModOrganizer.exe"), "mo2");
        _dir.WriteFile(Path.Join("gamma", "mods", "1- Addon", "gamedata", "a.script"), "script");
        _dir.WriteFile(Path.Join("gamma", "logs", "mo_interface.log"), "log");
        _dir.WriteFile(Path.Join("gamma", "overwrite", "a.txt"), "overwrite");
        _dir.WriteFile(Path.Join("gamma", "plugins", "basic_games", "a.py"), "py");
        _dir.WriteFile(Path.Join("gamma", "plugins", "__pycache__", "a.pyc"), "pyc");
    }

    public void Dispose() => _dir.Dispose();

    private async Task<string[]> HashAsync(string anomaly, string gamma, HashType hashType)
    {
        var zip = _dir.Join("hashes.zip");
        await HashUtility.Hash(zip, anomaly, gamma, _dir.Join("cache"), hashType);

        await using var archive = await ZipFile.OpenReadAsync(zip);
        var entry = Assert.Single(archive.Entries);
        Assert.Equal($"hashes-{hashType}-{Environment.UserName}.txt", entry.Name);
        using var reader = new StreamReader(await entry.OpenAsync());
        return (await reader.ReadToEndAsync()).Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
    }

    private static string[] Paths(string[] lines) =>
        lines.Select(x => x.Split('\t')[1].Replace('\\', '/')).ToArray();

    [Fact]
    public async Task Lists_installed_files_relative_to_their_folder()
    {
        var lines = await HashAsync(_dir.Join("anomaly"), _dir.Join("gamma"), HashType.Sha256);

        Assert.Equal(
            [
                "anomaly/appdata/user.ltx",
                "anomaly/bin/AnomalyDX11.exe",
                "anomaly/gamedata/configs/a.ltx",
                "gamma/ModOrganizer.exe",
                "gamma/mods/1- Addon/gamedata/a.script",
            ],
            Paths(lines).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public async Task Hashes_each_file()
    {
        var lines = await HashAsync(_dir.Join("anomaly"), _dir.Join("gamma"), HashType.Md5);

        var mo2 = lines.Single(x => x.EndsWith("ModOrganizer.exe", StringComparison.Ordinal));
        Assert.Equal(
            Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData("mo2"u8)),
            mo2.Split('\t')[0]
        );
    }

    [Fact]
    public async Task Folder_paths_with_a_trailing_separator_give_the_same_list()
    {
        var plain = await HashAsync(_dir.Join("anomaly"), _dir.Join("gamma"), HashType.Sha256);
        var trailing = await HashAsync(
            _dir.Join("anomaly") + Path.DirectorySeparatorChar,
            _dir.Join("gamma") + Path.DirectorySeparatorChar,
            HashType.Sha256
        );

        Assert.Equal(plain, trailing);
    }
}
