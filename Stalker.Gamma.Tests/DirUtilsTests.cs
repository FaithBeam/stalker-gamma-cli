using Stalker.Gamma.Utilities;
using Xunit;

namespace Stalker.Gamma.Tests;

public class DirUtilsTests
{
    [Fact]
    public void RecursivelyDeleteDirectory_DoesNotAbort_WhenNestedLeftoverTreeIsNotEmpty()
    {
        using var fixture = new TempDir();
        var trees = Path.Combine(
            fixture.Path,
            "242- Gardener of the Zone Textures - YuriVernadsky",
            "GoTZ_6.2_fomod",
            "05. Anthology Support",
            "gamedata",
            "textures",
            "trees"
        );
        Directory.CreateDirectory(trees);
        File.WriteAllText(Path.Combine(trees, "t_oak.dds"), "dds");

        // Reproduce Linux Directory.Delete(true) -> IOException "Directory not empty":
        // a leftover nested file cannot be unlinked until the parent is writable again.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(trees, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        var thrown = Record.Exception(() =>
            DirUtils.RecursivelyDeleteDirectory(fixture.Path, doNotMatch: [])
        );

        Assert.Null(thrown);
        Assert.False(
            Directory.Exists(
                Path.Combine(
                    fixture.Path,
                    "242- Gardener of the Zone Textures - YuriVernadsky",
                    "GoTZ_6.2_fomod"
                )
            )
        );
    }

    [Fact]
    public void RecursivelyDeleteDirectory_DoesNotAbort_WhenNestedFilesAppearAfterFirstPass()
    {
        using var fixture = new TempDir();
        var trees = Path.Combine(fixture.Path, "GoTZ_6.2_fomod", "gamedata", "textures", "trees");
        Directory.CreateDirectory(trees);
        for (var i = 0; i < 64; i++)
        {
            File.WriteAllText(Path.Combine(trees, $"leaf_{i}.dds"), "dds");
        }

        using var watcher = new FileSystemWatcher(trees)
        {
            Filter = "*.dds",
            NotifyFilter = NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        var recreated = 0;
        watcher.Deleted += (_, _) =>
        {
            if (Interlocked.Exchange(ref recreated, 1) != 0)
            {
                return;
            }

            try
            {
                File.WriteAllText(Path.Combine(trees, "late.dds"), "late");
            }
            catch (IOException)
            {
                // Directory already removed.
            }
            catch (UnauthorizedAccessException) { }
        };

        var thrown = Record.Exception(() =>
            DirUtils.RecursivelyDeleteDirectory(fixture.Path, doNotMatch: [])
        );

        Assert.Null(thrown);
        Assert.False(Directory.Exists(Path.Combine(fixture.Path, "GoTZ_6.2_fomod")));
    }

    [Fact]
    public void RecursivelyDeleteDirectory_PreservesDoNotMatchDirectories()
    {
        using var fixture = new TempDir();
        Directory.CreateDirectory(Path.Combine(fixture.Path, "gamedata", "configs"));
        File.WriteAllText(Path.Combine(fixture.Path, "gamedata", "configs", "keep.ltx"), "ok");
        var junk = Path.Combine(fixture.Path, "GoTZ_6.2_fomod", "docs");
        Directory.CreateDirectory(junk);
        File.WriteAllText(Path.Combine(junk, "readme.txt"), "x");

        DirUtils.RecursivelyDeleteDirectory(fixture.Path, ["gamedata", "appdata", "db", "fomod"]);

        Assert.True(File.Exists(Path.Combine(fixture.Path, "gamedata", "configs", "keep.ltx")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Path, "GoTZ_6.2_fomod")));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "gamma-cli-8-" + Guid.NewGuid().ToString("N")
            );

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (!Directory.Exists(Path))
                {
                    return;
                }

                if (!OperatingSystem.IsWindows())
                {
                    DirUtils.NormalizePermissions(Path);
                }

                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort fixture cleanup.
            }
        }
    }
}
