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

        string? diabolicalLink = null;
        var invalidateCache = false;

        var outerRetry = new ResiliencePipelineBuilder()
            .AddRetry(
                new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    UseJitter = true,
                    ShouldHandle = arguments =>
                        arguments.Outcome.Exception switch
                        {
                            CloudflareChallengeException => ValueTask.FromResult(false),
                            not null => ValueTask.FromResult(true),
                            _ => ValueTask.FromResult(false),
                        },
                    OnRetry = args =>
                    {
                        // failed downloading from a mirror, likely couldn't connect to CDN for some reason
                        // invalidate the mirror cache and try again
                        invalidateCache = true;
                        return ValueTask.CompletedTask;
                    },
                }
            )
            .Build();
        await outerRetry.ExecuteAsync(
            async ct =>
            {
                DirectoryInfo? parentPath;

                // acquired outside the try so a cancelled wait doesn't release a lock it never held
                await Lock.WaitAsync(ct);
                try
                {
                    var diabolicalResilience = BuildRetry();
                    await diabolicalResilience.ExecuteAsync(
                        async innertCt =>
                            diabolicalLink = await GetCdnLinkAsync(
                                url,
                                visitedMirrors,
                                invalidateCache,
                                ct: innertCt
                            ),
                        ct
                    );

                    // if bad mirror
                    if (string.IsNullOrWhiteSpace(diabolicalLink))
                    {
                        throw new ModDbUtilityException("Failed to get diabolical link");
                    }

                    parentPath = Directory.GetParent(output);
                    if (parentPath is not null && !parentPath.Exists)
                    {
                        parentPath.Create();
                    }
                }
                finally
                {
                    Lock.Release();
                }

                await networkServiceFactory
                    .Create()
                    .DownloadFileAsync(
                        diabolicalLink,
                        parentPath?.FullName ?? "./",
                        Path.GetFileName(output),
                        onProgress,
                        cancellationToken: ct,
                        expectedMd5: string.IsNullOrWhiteSpace(expectedMd5) ? null : expectedMd5
                    );
            },
            cancellationToken
        );
    }

    private static ResiliencePipeline BuildRetry()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(
                new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    UseJitter = true,
                    ShouldHandle = arguments =>
                        arguments.Outcome.Exception switch
                        {
                            CloudflareChallengeException => ValueTask.FromResult(false),
                            not null => ValueTask.FromResult(true),
                            _ => ValueTask.FromResult(false),
                        },
                    OnRetry = args => ValueTask.CompletedTask,
                }
            )
            .AddTimeout(CdnLinkAttemptTimeout)
            .Build();
    }

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
