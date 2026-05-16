using System.Text.RegularExpressions;
using Polly;
using Polly.Retry;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.ModDb.Services;

public partial class ModDbService(
    ModDbMirrorService modDbMirrorService,
    CurlUtility curlUtility,
    ModDbGetCdnLinkService modDbGetCdnLinkServiceSvc
)
{
    public async Task GetModDbLinkCurl(
        string url,
        string output,
        Action<double> onProgress,
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

                var parentPath = Directory.GetParent(output);
                if (parentPath is not null && !parentPath.Exists)
                {
                    parentPath.Create();
                }

                return await curlUtility.DownloadFileAsync(
                    diabolicalLink,
                    parentPath?.FullName ?? "./",
                    Path.GetFileName(output),
                    onProgress,
                    cancellationToken: ct
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
                            not null => ValueTask.FromResult(true),
                            _ => ValueTask.FromResult(false),
                        },
                    OnRetry = args =>
                    {
                        return ValueTask.CompletedTask;
                    },
                }
            )
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
        var getContentTask = curlUtility.GetStringAsync(url, ct);
        var results = await Task.WhenAll(mirrorTask, getContentTask);

        var (mirror, content) = (results[0], results[1]);
        var link = WindowLocationRx().Match(content).Groups[1].Value;
        var linkSplit = link.Split('/');

        linkSplit[6] = mirror;

        var downloadLink = string.Join("/", linkSplit);

        mirrorsVisited.Add(mirror);

        return await modDbGetCdnLinkServiceSvc.GetDbolicalUrlAsync(downloadLink, ct);
    }

    [GeneratedRegex("""window.location.href="(.+)";""")]
    private static partial Regex WindowLocationRx();
}

public class ModDbUtilityException : Exception
{
    public ModDbUtilityException(string msg)
        : base(msg) { }

    public ModDbUtilityException(string msg, Exception innerException)
        : base(msg, innerException) { }
}
