using System.Net;
using System.Text;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Tests;

public class DownloadFileFastTests : IDisposable
{
    private const string Url = "https://example.invalid/addon.zip";
    private static readonly byte[] Content = Encoding.UTF8.GetBytes("0123456789abcdefghij");

    private readonly TempDir _dir = new();
    private string Destination => _dir.Join("downloads", "addon.zip");

    public DownloadFileFastTests() => Directory.CreateDirectory(_dir.Join("downloads"));

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task Downloads_to_the_path_and_leaves_no_partial_file()
    {
        using var hc = FakeServer.Respond(_ => FakeServer.Ok(Content));

        await DownloadFileFast.DownloadAsync(hc, Url, Destination);

        Assert.Equal(Content, await File.ReadAllBytesAsync(Destination));
        Assert.False(File.Exists($"{Destination}.part"));
    }

    [Fact]
    public async Task Interrupted_download_never_leaves_a_truncated_archive()
    {
        // the connection drops after 5 bytes
        using var hc = FakeServer.Respond(_ =>
            FakeServer.Ok(new FailingStream(Content[..5]), Content.Length)
        );

        await Assert.ThrowsAsync<DownloadFileFastException>(() =>
            DownloadFileFast.DownloadAsync(hc, Url, Destination)
        );

        // a truncated file at the archive's path would look like a finished download next time
        Assert.False(File.Exists(Destination));
        Assert.False(File.Exists($"{Destination}.part"));
    }

    [Fact]
    public async Task Interrupted_download_keeps_the_previous_archive()
    {
        await File.WriteAllTextAsync(Destination, "previous version");
        using var hc = FakeServer.Respond(_ =>
            FakeServer.Ok(new FailingStream(Content[..5]), Content.Length)
        );

        await Assert.ThrowsAsync<DownloadFileFastException>(() =>
            DownloadFileFast.DownloadAsync(hc, Url, Destination)
        );

        Assert.Equal("previous version", await File.ReadAllTextAsync(Destination));
    }

    [Fact]
    public async Task Http_error_is_reported_with_the_url()
    {
        using var hc = FakeServer.Respond(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var e = await Assert.ThrowsAsync<DownloadFileFastException>(() =>
            DownloadFileFast.DownloadAsync(hc, Url, Destination)
        );

        Assert.Contains(Url, e.Message);
        Assert.False(File.Exists(Destination));
    }

    [Fact]
    public async Task Cancelling_throws_and_cleans_up()
    {
        using var hc = FakeServer.Respond(_ => FakeServer.Ok(Content));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DownloadFileFast.DownloadAsync(hc, Url, Destination, cancellationToken: cts.Token)
        );

        Assert.False(File.Exists(Destination));
        Assert.False(File.Exists($"{Destination}.part"));
    }

    [Fact]
    public async Task Reports_progress_ending_at_complete()
    {
        using var hc = FakeServer.Respond(_ => FakeServer.Ok(Content));
        List<DownloadProgress> progress = [];

        await DownloadFileFast.DownloadAsync(hc, Url, Destination, onProgress: progress.Add);

        Assert.NotEmpty(progress);
        Assert.Equal(new DownloadProgress(Content.Length, Content.Length), progress[^1]);
        Assert.Equal(1.0, progress[^1].Percent);
    }

    [Fact]
    public async Task Progress_percent_is_unknown_without_a_content_length()
    {
        // GitHub streams archive zips without a Content-Length
        using var hc = FakeServer.Respond(_ =>
            FakeServer.Ok(new MemoryStream(Content), length: null)
        );
        List<DownloadProgress> progress = [];

        await DownloadFileFast.DownloadAsync(hc, Url, Destination, onProgress: progress.Add);

        Assert.Null(progress[0].Percent);
        Assert.Equal(1.0, progress[^1].Percent);
    }

    [Fact]
    public async Task Resume_asks_for_the_rest_and_appends_it()
    {
        await File.WriteAllBytesAsync(Destination, Content[..8]);
        HttpRequestMessage? sent = null;
        using var hc = FakeServer.Respond(req =>
        {
            sent = req;
            var response = FakeServer.Ok(Content[8..]);
            response.StatusCode = HttpStatusCode.PartialContent;
            return response;
        });

        await DownloadFileFast.DownloadAsync(hc, Url, Destination, resume: true);

        Assert.Equal("bytes=8-", sent!.Headers.Range!.ToString());
        Assert.Equal(Content, await File.ReadAllBytesAsync(Destination));
    }

    [Fact]
    public async Task Resume_starts_over_when_the_server_ignores_the_range()
    {
        await File.WriteAllBytesAsync(Destination, Content[..8]);
        using var hc = FakeServer.Respond(_ => FakeServer.Ok(Content));

        await DownloadFileFast.DownloadAsync(hc, Url, Destination, resume: true);

        Assert.Equal(Content, await File.ReadAllBytesAsync(Destination));
    }

    [Fact]
    public async Task Resume_of_a_complete_file_keeps_it()
    {
        await File.WriteAllBytesAsync(Destination, Content);
        using var hc = FakeServer.Respond(_ => new HttpResponseMessage(
            HttpStatusCode.RequestedRangeNotSatisfiable
        ));
        List<DownloadProgress> progress = [];

        await DownloadFileFast.DownloadAsync(
            hc,
            Url,
            Destination,
            onProgress: progress.Add,
            resume: true
        );

        Assert.Equal(Content, await File.ReadAllBytesAsync(Destination));
        Assert.Equal(1.0, Assert.Single(progress).Percent);
    }

    private static readonly string ContentMd5 = Convert.ToHexStringLower(
        System.Security.Cryptography.MD5.HashData(Content)
    );

    [Fact]
    public async Task Download_matching_its_md5_is_kept()
    {
        using var hc = FakeServer.Respond(_ => FakeServer.Ok(Content));

        await DownloadFileFast.DownloadAsync(
            hc,
            Url,
            Destination,
            expectedMd5: ContentMd5.ToUpperInvariant()
        );

        Assert.Equal(Content, await File.ReadAllBytesAsync(Destination));
        Assert.False(File.Exists($"{Destination}.part"));
    }

    [Fact]
    public async Task Download_not_matching_its_md5_throws_and_leaves_nothing()
    {
        using var hc = FakeServer.Respond(_ => FakeServer.Ok(Content));

        var e = await Assert.ThrowsAsync<DownloadFileFastMd5MismatchException>(() =>
            DownloadFileFast.DownloadAsync(hc, Url, Destination, expectedMd5: "deadbeef")
        );

        Assert.Contains(Url, e.Message);
        Assert.False(File.Exists(Destination));
        Assert.False(File.Exists($"{Destination}.part"));
    }

    [Fact]
    public async Task Interrupted_download_with_an_md5_keeps_its_partial_file()
    {
        using var hc = FakeServer.Respond(_ =>
            FakeServer.Ok(new FailingStream(Content[..5]), Content.Length)
        );

        await Assert.ThrowsAsync<DownloadFileFastException>(() =>
            DownloadFileFast.DownloadAsync(hc, Url, Destination, expectedMd5: ContentMd5)
        );

        Assert.False(File.Exists(Destination));
        Assert.Equal(Content[..5], await File.ReadAllBytesAsync($"{Destination}.part"));
    }

    [Fact]
    public async Task Download_with_an_md5_continues_its_partial_file()
    {
        await File.WriteAllBytesAsync($"{Destination}.part", Content[..8]);
        HttpRequestMessage? sent = null;
        using var hc = FakeServer.Respond(req =>
        {
            sent = req;
            var response = FakeServer.Ok(Content[8..]);
            response.StatusCode = HttpStatusCode.PartialContent;
            return response;
        });

        await DownloadFileFast.DownloadAsync(hc, Url, Destination, expectedMd5: ContentMd5);

        Assert.Equal("bytes=8-", sent!.Headers.Range!.ToString());
        Assert.Equal(Content, await File.ReadAllBytesAsync(Destination));
        Assert.False(File.Exists($"{Destination}.part"));
    }

    [Fact]
    public async Task Continued_partial_file_from_another_archive_fails_its_md5_and_is_deleted()
    {
        // left over from an older version of the archive with the same name
        await File.WriteAllBytesAsync($"{Destination}.part", "stale!!!"u8.ToArray());
        using var hc = FakeServer.Respond(_ =>
        {
            var response = FakeServer.Ok(Content[8..]);
            response.StatusCode = HttpStatusCode.PartialContent;
            return response;
        });

        await Assert.ThrowsAsync<DownloadFileFastMd5MismatchException>(() =>
            DownloadFileFast.DownloadAsync(hc, Url, Destination, expectedMd5: ContentMd5)
        );

        // so the retry starts over
        Assert.False(File.Exists($"{Destination}.part"));
        Assert.False(File.Exists(Destination));
    }

    [Fact]
    public async Task Complete_partial_file_matching_its_md5_is_moved_into_place()
    {
        await File.WriteAllBytesAsync($"{Destination}.part", Content);
        using var hc = FakeServer.Respond(_ => new HttpResponseMessage(
            HttpStatusCode.RequestedRangeNotSatisfiable
        ));

        await DownloadFileFast.DownloadAsync(hc, Url, Destination, expectedMd5: ContentMd5);

        Assert.Equal(Content, await File.ReadAllBytesAsync(Destination));
    }

    [Fact]
    public async Task Partial_file_is_not_continued_without_an_md5()
    {
        await File.WriteAllBytesAsync($"{Destination}.part", Content[..8]);
        HttpRequestMessage? sent = null;
        using var hc = FakeServer.Respond(req =>
        {
            sent = req;
            return FakeServer.Ok(Content);
        });

        await DownloadFileFast.DownloadAsync(hc, Url, Destination);

        Assert.Null(sent!.Headers.Range);
        Assert.Equal(Content, await File.ReadAllBytesAsync(Destination));
    }

    private static class FakeServer
    {
        public static HttpClient Respond(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            new(new Handler(respond));

        public static HttpResponseMessage Ok(byte[] content) =>
            Ok(new MemoryStream(content), content.Length);

        public static HttpResponseMessage Ok(Stream content, long? length)
        {
            var body = new StreamContent(content);
            body.Headers.ContentLength = length;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = body };
        }

        private class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            )
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(respond(request));
            }
        }
    }

    /// <summary>
    /// Returns some bytes, then fails like a dropped connection
    /// </summary>
    private class FailingStream(byte[] before) : MemoryStream(before)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, count);
            return read > 0 ? read : throw new IOException("Connection reset");
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            var read = base.Read(buffer.Span);
            return read > 0
                ? ValueTask.FromResult(read)
                : ValueTask.FromException<int>(new IOException("Connection reset"));
        }
    }
}
