using System.Text.RegularExpressions;
using Polly;
using Polly.Retry;
using Stalker.Gamma.Factories;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.ModDb.Services;

public partial class ModDbService(
    ModDbMirrorService modDbMirrorService,
    NetworkServiceFactory networkServiceFactory,
    ModDbGetCdnLinkService modDbGetCdnLinkServiceSvc
)
{
    private static readonly SemaphoreSlim Lock = new(1);

    /// <param name="expectedMd5">
    /// The archive's MD5 if it's known. The download is checked against it, and a retry
    /// continues a partial download instead of starting over.
    /// </param>
    public async Task DownloadAddonAsync(
        string url,
        string output,
        Action<DownloadProgress> onProgress,
        string? expectedMd5 = null,
        CancellationToken cancellationToken = default
    )
    {
        List<string> visitedMirrors = [];
        await DownloadWithRetriesAsync(
            (invalidateCache, ct) => GetCdnLinkAsync(url, visitedMirrors, invalidateCache, ct),
            async (cdnLink, ct) =>
            {
                var parentPath = Directory.GetParent(output);
                if (parentPath is not null && !parentPath.Exists)
                {
                    parentPath.Create();
                }
                await networkServiceFactory
                    .Create()
                    .DownloadFileAsync(
                        cdnLink,
                        parentPath?.FullName ?? "./",
                        Path.GetFileName(output),
                        onProgress,
                        cancellationToken: ct,
                        expectedMd5: string.IsNullOrWhiteSpace(expectedMd5) ? null : expectedMd5
                    );
            },
            RetryDelay,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets the CDN link while holding <see cref="Lock"/>, retrying that on its own, then
    /// downloads from it. A failed download starts over with a new CDN link from a mirror that
    /// hasn't been tried, and the mirror list is fetched again in case it's stale. A Cloudflare
    /// challenge is never retried, since every request fails the same way until it's solved.
    /// </summary>
    /// <param name="getCdnLink">Gets the CDN link, refetching the mirror list when its argument is true</param>
    /// <param name="download">Downloads from the CDN link</param>
    internal static async Task DownloadWithRetriesAsync(
        Func<bool, CancellationToken, Task<string?>> getCdnLink,
        Func<string, CancellationToken, Task> download,
        TimeSpan retryDelay,
        CancellationToken cancellationToken
    )
    {
        var invalidateCache = false;
        // failed downloading from a mirror, likely couldn't connect to CDN for some reason
        var outerRetry = BuildRetry(retryDelay, onRetry: () => invalidateCache = true);
        await outerRetry.ExecuteAsync(
            async ct =>
            {
                string? cdnLink = null;
                // acquired outside the try so a cancelled wait doesn't release a lock it never held
                await Lock.WaitAsync(ct);
                try
                {
                    await BuildRetry(retryDelay, attemptTimeout: CdnLinkAttemptTimeout)
                        .ExecuteAsync(
                            async innerCt => cdnLink = await getCdnLink(invalidateCache, innerCt),
                            ct
                        );

                    // if bad mirror
                    if (string.IsNullOrWhiteSpace(cdnLink))
                    {
                        throw new ModDbUtilityException("Failed to get diabolical link");
                    }
                }
                finally
                {
                    Lock.Release();
                }

                await download(cdnLink, ct);
            },
            cancellationToken
        );
    }

    private static ResiliencePipeline BuildRetry(
        TimeSpan delay,
        Action? onRetry = null,
        TimeSpan? attemptTimeout = null
    )
    {
        var builder = new ResiliencePipelineBuilder().AddRetry(
            new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = delay,
                UseJitter = true,
                ShouldHandle = arguments =>
                    ValueTask.FromResult(
                        arguments.Outcome.Exception is not null and not CloudflareChallengeException
                    ),
                OnRetry = _ =>
                {
                    onRetry?.Invoke();
                    return ValueTask.CompletedTask;
                },
            }
        );
        if (attemptTimeout is not null)
        {
            builder.AddTimeout(attemptTimeout.Value);
        }
        return builder.Build();
    }

    /// <summary>
    /// Polly's default delay between attempts
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private async Task<string?> GetCdnLinkAsync(
        string url,
        List<string> mirrorsVisited,
        bool invalidateCache = false,
        CancellationToken ct = default
    )
    {
        var mirrorTask = modDbMirrorService.GetMirrorAsync(
            $"{url}/all",
            excludeMirrors: mirrorsVisited,
            invalidateCache: invalidateCache,
            cancellationToken: ct
        );
        var getContentTask = networkServiceFactory
            .Create()
            .GetStringAsync(url, cancellationToken: ct);
        var results = await Task.WhenAll(mirrorTask, getContentTask);

        var (mirror, content) = (results[0], results[1]);
        var downloadLink = BuildMirrorDownloadLink(url, content, mirror);

        mirrorsVisited.Add(mirror);

        return await modDbGetCdnLinkServiceSvc.ExecuteAsync(downloadLink, ct: ct);
    }

    /// <summary>
    /// Finds the download link in a ModDB download page and points it at <paramref name="mirror"/>
    /// </summary>
    /// <param name="url">The download page's url, for the error message</param>
    /// <param name="content">The download page's html</param>
    /// <param name="mirror">The mirror to download from</param>
    internal static string BuildMirrorDownloadLink(string url, string content, string mirror)
    {
        var match = WindowLocationRx().Match(content);
        // e.g. https://www.moddb.com/downloads/mirror/{id}/{mirror}/{hash}
        var linkSplit = match.Success ? match.Groups[1].Value.Split('/') : [];
        if (linkSplit.Length <= MirrorSegment)
        {
            throw new ModDbUtilityException(
                $"Unable to find the download link on {url}, ModDB may have changed its page or served an error page"
            );
        }

        linkSplit[MirrorSegment] = mirror;
        return string.Join("/", linkSplit);
    }

    /// <summary>
    /// The index of the mirror in the '/' split download link
    /// </summary>
    private const int MirrorSegment = 6;

    /// <summary>
    /// Finding a mirror and the CDN link happens while every other ModDB download waits on
    /// <see cref="Lock"/>, so an attempt that hangs is abandoned and retried instead of stalling them all
    /// </summary>
    private static readonly TimeSpan CdnLinkAttemptTimeout = TimeSpan.FromMinutes(2);

    [GeneratedRegex("""window.location.href="(.+)";""")]
    private static partial Regex WindowLocationRx();
}

public class ModDbUtilityException(string msg) : Exception(msg);
