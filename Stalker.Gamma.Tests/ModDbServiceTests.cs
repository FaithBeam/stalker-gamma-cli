using Stalker.Gamma.ModDb.Services;

namespace Stalker.Gamma.Tests;

public class ModDbServiceTests
{
    private const string Url = "https://www.moddb.com/downloads/start/123";

    [Fact]
    public void Download_link_points_at_the_mirror()
    {
        const string page = """
            <script>window.location.href="https://www.moddb.com/downloads/mirror/123/101/abcdef";</script>
            """;

        var link = ModDbService.BuildMirrorDownloadLink(Url, page, "202");

        Assert.Equal("https://www.moddb.com/downloads/mirror/123/202/abcdef", link);
    }

    [Theory]
    // an error page
    [InlineData("<html><body>Not Found</body></html>")]
    // a link without a mirror segment
    [InlineData("""window.location.href="https://www.moddb.com/downloads";""")]
    public void Page_without_a_download_link_throws_a_clear_error(string page)
    {
        var e = Assert.Throws<ModDbUtilityException>(() =>
            ModDbService.BuildMirrorDownloadLink(Url, page, "202")
        );

        Assert.Contains(Url, e.Message);
    }
}
