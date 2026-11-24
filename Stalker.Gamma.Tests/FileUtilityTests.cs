using System.Security.Cryptography;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Tests;

public class DirUtilsTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void CopyDirectory_copies_files_and_folders()
    {
        _dir.WriteFile(Path.Join("src", "a.txt"), "a");
        _dir.WriteFile(Path.Join("src", "gamedata", "scripts", "b.script"), "b");
        List<double> progress = [];

        DirUtils.CopyDirectory(_dir.Join("src"), _dir.Join("dest"), onProgress: progress.Add);

        Assert.Equal("a", File.ReadAllText(_dir.Join("dest", "a.txt")));
        Assert.Equal("b", File.ReadAllText(_dir.Join("dest", "gamedata", "scripts", "b.script")));
        Assert.True(File.Exists(_dir.Join("src", "a.txt")));
        Assert.Equal(1.0, progress[^1]);
    }

    [Fact]
    public void CopyDirectory_moves_files_when_asked()
    {
        _dir.WriteFile(Path.Join("src", "a.txt"), "a");

        DirUtils.CopyDirectory(_dir.Join("src"), _dir.Join("dest"), moveFile: true);

        Assert.True(File.Exists(_dir.Join("dest", "a.txt")));
        Assert.False(File.Exists(_dir.Join("src", "a.txt")));
    }

    [Fact]
    public void CopyDirectory_skips_git_folders()
    {
        _dir.WriteFile(Path.Join("src", ".git", "HEAD"), "ref");
        _dir.WriteFile(Path.Join("src", ".github", "workflows", "ci.yml"), "ci");
        _dir.WriteFile(Path.Join("src", "gamedata", "a.txt"), "a");

        DirUtils.CopyDirectory(_dir.Join("src"), _dir.Join("dest"));

        Assert.False(Directory.Exists(_dir.Join("dest", ".git")));
        Assert.False(Directory.Exists(_dir.Join("dest", ".github")));
        Assert.True(File.Exists(_dir.Join("dest", "gamedata", "a.txt")));
    }

    [Fact]
    public void CopyDirectory_copies_a_folder_with_git_in_its_name()
    {
        // only a folder that is itself git metadata is skipped, not one whose parent's name
        // contains ".git"
        var src = _dir.Join("owner.github.io", "addon");
        _dir.WriteFile(Path.Join("owner.github.io", "addon", "gamedata", "a.txt"), "a");

        DirUtils.CopyDirectory(src, _dir.Join("dest"));

        Assert.True(File.Exists(_dir.Join("dest", "gamedata", "a.txt")));
    }

    [Fact]
    public void CopyDirectory_skips_empty_folders()
    {
        Directory.CreateDirectory(_dir.Join("src", "empty", "nested"));
        _dir.WriteFile(Path.Join("src", "a.txt"));

        DirUtils.CopyDirectory(_dir.Join("src"), _dir.Join("dest"));

        Assert.False(Directory.Exists(_dir.Join("dest", "empty")));
    }

    [Fact]
    public void CopyDirectory_skips_the_filtered_file_name()
    {
        _dir.WriteFile(Path.Join("src", "meta.ini"), "src");
        _dir.WriteFile(Path.Join("src", "sub", "meta.ini"), "src");
        _dir.WriteFile(Path.Join("src", "a.txt"), "a");

        DirUtils.CopyDirectory(_dir.Join("src"), _dir.Join("dest"), fileFilter: "meta.ini");

        Assert.False(File.Exists(_dir.Join("dest", "meta.ini")));
        Assert.False(File.Exists(_dir.Join("dest", "sub", "meta.ini")));
        Assert.True(File.Exists(_dir.Join("dest", "a.txt")));
    }

    [Fact]
    public void CopyDirectory_keeps_existing_files_without_overwrite()
    {
        _dir.WriteFile(Path.Join("src", "a.txt"), "new");
        _dir.WriteFile(Path.Join("dest", "a.txt"), "old");

        DirUtils.CopyDirectory(_dir.Join("src"), _dir.Join("dest"), overwrite: false);

        Assert.Equal("old", File.ReadAllText(_dir.Join("dest", "a.txt")));
    }

    [Fact]
    public void CopyDirectory_overwrites_existing_files_by_default()
    {
        _dir.WriteFile(Path.Join("src", "a.txt"), "new");
        _dir.WriteFile(Path.Join("dest", "a.txt"), "old");

        DirUtils.CopyDirectory(_dir.Join("src"), _dir.Join("dest"));

        Assert.Equal("new", File.ReadAllText(_dir.Join("dest", "a.txt")));
    }

    [Fact]
    public void CopyDirectory_stops_when_cancelled()
    {
        _dir.WriteFile(Path.Join("src", "a.txt"));

        Assert.Throws<OperationCanceledException>(() =>
            DirUtils.CopyDirectory(
                _dir.Join("src"),
                _dir.Join("dest"),
                cancellationToken: new CancellationToken(true)
            )
        );
    }

    [Fact]
    public void NormalizePermissions_makes_read_only_files_deletable()
    {
        var file = _dir.WriteFile(Path.Join("addon", "gamedata", "a.txt"));
        File.SetAttributes(file, FileAttributes.ReadOnly);

        DirUtils.NormalizePermissions(_dir.Join("addon"));

        Assert.False(new FileInfo(file).IsReadOnly);
        Directory.Delete(_dir.Join("addon"), true);
    }

    [Fact]
    public void NormalizePermissions_ignores_a_missing_folder() =>
        DirUtils.NormalizePermissions(_dir.Join("missing"));
}

public class ProcessInstructionsTests : IDisposable
{
    private readonly TempDir _addon = new();

    public void Dispose() => _addon.Dispose();

    [Fact]
    public void Instruction_folder_with_gamedata_is_moved_to_the_addon_root()
    {
        // e.g. "00. Grok's Stash Overhaul" holds gamedata/
        _addon.WriteFile(Path.Join("00. Main", "gamedata", "scripts", "a.script"), "a");

        ProcessInstructions.Process(_addon.Path, ["00. Main"], CancellationToken.None);

        Assert.Equal("a", File.ReadAllText(_addon.Join("gamedata", "scripts", "a.script")));
    }

    [Fact]
    public void Instruction_folder_without_gamedata_is_moved_into_gamedata()
    {
        // e.g. a patch folder that holds configs/ and scripts/ directly
        _addon.WriteFile(Path.Join("patch", "configs", "a.ltx"), "a");

        ProcessInstructions.Process(_addon.Path, ["patch"], CancellationToken.None);

        Assert.Equal("a", File.ReadAllText(_addon.Join("gamedata", "configs", "a.ltx")));
    }

    [Fact]
    public void Later_instructions_overwrite_earlier_ones()
    {
        _addon.WriteFile(Path.Join("main", "gamedata", "a.ltx"), "main");
        _addon.WriteFile(Path.Join("optional", "gamedata", "a.ltx"), "optional");

        ProcessInstructions.Process(_addon.Path, ["main", "optional"], CancellationToken.None);

        Assert.Equal("optional", File.ReadAllText(_addon.Join("gamedata", "a.ltx")));
    }

    [Fact]
    public void Missing_instruction_folder_is_skipped()
    {
        ProcessInstructions.Process(_addon.Path, ["missing"], CancellationToken.None);

        Assert.True(Directory.Exists(_addon.Join("gamedata")));
    }
}

public class CleanExtractPathTests : IDisposable
{
    private readonly TempDir _addon = new();

    public void Dispose() => _addon.Dispose();

    [Fact]
    public void Keeps_game_folders_and_deletes_the_rest()
    {
        foreach (var kept in new[] { "gamedata", "appdata", "db", "fomod" })
        {
            _addon.WriteFile(Path.Join(kept, "a.txt"));
        }
        _addon.WriteFile(Path.Join("00. Main", "gamedata", "a.txt"));
        _addon.WriteFile(Path.Join("Gamedata.old", "a.txt"));
        _addon.WriteFile(".gitattributes");

        CleanExtractPath.Clean(_addon.Path);

        Assert.Equal(
            ["appdata", "db", "fomod", "gamedata"],
            Directory.GetDirectories(_addon.Path).Select(Path.GetFileName).Order()
        );
        Assert.False(File.Exists(_addon.Join(".gitattributes")));
    }

    [Fact]
    public void Protected_folder_names_ignore_case()
    {
        _addon.WriteFile(Path.Join("GameData", "a.txt"));

        CleanExtractPath.Clean(_addon.Path);

        Assert.True(File.Exists(_addon.Join("GameData", "a.txt")));
    }

    [Fact]
    public void Missing_folder_is_ignored() => CleanExtractPath.Clean(_addon.Join("missing"));
}

public class HashUtilsTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Theory]
    [InlineData("MD5", "5d41402abc4b2a76b9719d911017c592")]
    [InlineData("SHA256", "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824")]
    public async Task Hashes_a_file_as_lowercase_hex(string algorithm, string expected)
    {
        var file = _dir.WriteFile("hello.txt", "hello");
        List<double> progress = [];

        var hash = await HashUtils.HashFile(file, new HashAlgorithmName(algorithm), progress.Add);

        Assert.Equal(expected, hash);
        Assert.Equal(1.0, progress[^1]);
    }

    [Fact]
    public async Task Hashes_a_file_larger_than_one_chunk()
    {
        // the file is read 1 MiB at a time
        var bytes = new byte[(1024 * 1024 * 2) + 123];
        new Random(42).NextBytes(bytes);
        var file = _dir.Join("big.bin");
        await File.WriteAllBytesAsync(file, bytes);

        var hash = await HashUtils.HashFile(file, HashAlgorithmName.MD5);

        Assert.Equal(Convert.ToHexStringLower(MD5.HashData(bytes)), hash);
    }
}

public class WriteAddonMetaIniTests : IDisposable
{
    private readonly TempDir _addon = new();

    public void Dispose() => _addon.Dispose();

    [Fact]
    public void Points_mo2_at_the_archive_and_page()
    {
        WriteAddonMetaIni.Write(
            _addon.Path,
            "Groks_main_menu_theme.zip",
            "https://www.moddb.com/mods/stalker-anomaly/addons/groks-main-menu-theme-deathcard-cabin"
        );

        var lines = File.ReadAllText(_addon.Join("meta.ini")).Split("\r\n");
        Assert.Contains("installationFile=Groks_main_menu_theme.zip", lines);
        Assert.Contains(
            "url=https://www.moddb.com/mods/stalker-anomaly/addons/groks-main-menu-theme-deathcard-cabin",
            lines
        );
        Assert.DoesNotContain(lines, l => l.Contains('\n'));
    }
}
