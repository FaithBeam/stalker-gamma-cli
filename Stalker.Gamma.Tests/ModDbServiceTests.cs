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

    private static readonly TimeSpan NoDelay = TimeSpan.Zero;

    [Fact]
    public async Task Failed_download_retries_with_a_fresh_mirror_list()
    {
        List<bool> invalidateCacheArgs = [];
        List<string> downloadedFrom = [];
        var attempt = 0;

        await ModDbService.DownloadWithRetriesAsync(
            (invalidateCache, _) =>
            {
                invalidateCacheArgs.Add(invalidateCache);
                return Task.FromResult<string?>($"https://cdn/{++attempt}");
            },
            (link, _) =>
            {
                downloadedFrom.Add(link);
                return link == "https://cdn/1"
                    ? Task.FromException(new IOException("connection reset"))
                    : Task.CompletedTask;
            },
            NoDelay,
            default
        );

        Assert.Equal([false, true], invalidateCacheArgs);
        Assert.Equal(["https://cdn/1", "https://cdn/2"], downloadedFrom);
    }

    [Fact]
    public async Task Getting_the_cdn_link_is_retried_before_downloading()
    {
        var attempts = 0;
        string? downloadedFrom = null;

        await ModDbService.DownloadWithRetriesAsync(
            (_, _) =>
                ++attempts < 3
                    ? Task.FromException<string?>(new HttpRequestException("mirror down"))
                    : Task.FromResult<string?>("https://cdn/ok"),
            (link, _) =>
            {
                downloadedFrom = link;
                return Task.CompletedTask;
            },
            NoDelay,
            default
        );

        Assert.Equal(3, attempts);
        Assert.Equal("https://cdn/ok", downloadedFrom);
    }

    [Fact]
    public async Task Cloudflare_challenge_is_not_retried()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<CloudflareChallengeException>(() =>
            ModDbService.DownloadWithRetriesAsync(
                (_, _) =>
                {
                    attempts++;
                    return Task.FromException<string?>(
                        new CloudflareChallengeException("challenged")
                    );
                },
                (_, _) => Task.CompletedTask,
                NoDelay,
                default
            )
        );

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Download_that_keeps_failing_gives_up_with_its_error()
    {
        var downloads = 0;

        await Assert.ThrowsAsync<IOException>(() =>
            ModDbService.DownloadWithRetriesAsync(
                (_, _) => Task.FromResult<string?>("https://cdn/1"),
                (_, _) =>
                {
                    downloads++;
                    return Task.FromException(new IOException("connection reset"));
                },
                NoDelay,
                default
            )
        );

        // the first attempt and 3 retries
        Assert.Equal(4, downloads);
    }

    [Fact]
    public async Task Missing_cdn_link_fails_with_a_clear_error()
    {
        await Assert.ThrowsAsync<ModDbUtilityException>(() =>
            ModDbService.DownloadWithRetriesAsync(
                (_, _) => Task.FromResult<string?>(null),
                (_, _) => Task.CompletedTask,
                NoDelay,
                default
            )
        );
    }
}
