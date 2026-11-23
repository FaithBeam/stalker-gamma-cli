namespace LibSevenZip.Tests;

public class ResolveUnderTests
{
    private static readonly string Root = Path.GetFullPath(
        Path.Join(Path.GetTempPath(), "resolve-under-root")
    );

    private static string Under(params string[] segments) => Path.Join([Root, .. segments]);

    public static TheoryData<string, string> Allowed =>
        new()
        {
            // #58: files starting with '~' were rejected on Windows.
            {
                @"Dark signal mutant audio\gamedata\sounds\monsters\controller\~controller_attack_2.ogg",
                Under(
                    "Dark signal mutant audio",
                    "gamedata",
                    "sounds",
                    "monsters",
                    "controller",
                    "~controller_attack_2.ogg"
                )
            },
            {
                @"SCO3 Addon - Anomaly Sounds\gamedata\sounds\anomaly\~gravi_idle01.ogg",
                Under(
                    "SCO3 Addon - Anomaly Sounds",
                    "gamedata",
                    "sounds",
                    "anomaly",
                    "~gravi_idle01.ogg"
                )
            },
            { "~", Under("~") },
            { "a/b/c.txt", Under("a", "b", "c.txt") },
            { @"a\b\c.txt", Under("a", "b", "c.txt") },
            { @"a\\b", Under("a", "b") },
            { "/a//b/", Under("a", "b") },
            { "a/./b", Under("a", "b") },
            { "..foo/bar..", Under("..foo", "bar..") },
            { ".hidden", Under(".hidden") },
            { "./", Root },
            { "", Root },
        };

    [Theory]
    [MemberData(nameof(Allowed))]
    public void Joins_items_inside_the_root(string itemPath, string expected) =>
        Assert.Equal(expected, SevenZipArchive.ResolveUnder(Root, itemPath));

    [Theory]
    [InlineData("..")]
    [InlineData("../x")]
    [InlineData("a/../../x")]
    [InlineData(@"a\..\..\x")]
    [InlineData("a/../b")]
    [InlineData("a/.../x")]
    [InlineData("a/.. /x")]
    [InlineData("a/ /x")]
    [InlineData("C:foo")]
    [InlineData("C:/x")]
    [InlineData("file.txt:stream")]
    public void Rejects_items_that_could_escape_the_root(string itemPath) =>
        Assert.Throws<SevenZipException>(() => SevenZipArchive.ResolveUnder(Root, itemPath));

    [Fact]
    public void Accepts_a_root_with_a_trailing_separator() =>
        Assert.Equal(
            Under("a"),
            SevenZipArchive.ResolveUnder(Root + Path.DirectorySeparatorChar, "a")
        );
}
