using System.Text.RegularExpressions;
using Polly;
using Polly.Retry;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Proxies;

namespace Stalker.Gamma.Utilities;

public partial class ModDbUtility(
    MirrorUtility mirrorUtility,
    PythonApiProxy pythonApiProxy,
    GetDbolicalUrl getDbolicalUrlSvc
)
{
    /// <summary>
    /// Downloads from ModDB using curl.
    /// </summary>
    public async Task GetModDbLinkCurl(
        string url,
        string output,
        Action<double> onProgress,
        CancellationToken cancellationToken = default
    )
    {
        List<string> visitedMirrors = [];

        var diabolicalResilience = new ResiliencePipelineBuilder()
            .AddRetry(
                new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    UseJitter = true,
                    OnRetry = args =>
                    {
                        return ValueTask.CompletedTask;
                    },
                }
            )
            .Build();

        string? diabolicalLink = null;
        await diabolicalResilience.ExecuteAsync(
            async ct => diabolicalLink = await GetCdnLinkAsync(url, visitedMirrors, ct: ct),
            cancellationToken
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

        await pythonApiProxy.DownloadFileAsync(
            diabolicalLink,
            parentPath?.FullName ?? "./",
            Path.GetFileName(output),
            onProgress,
            cancellationToken: cancellationToken
        );
    }

    private async Task<string?> GetCdnLinkAsync(
        string url,
        List<string> mirrorsVisited,
        CancellationToken ct = default
    )
    {
        var mirrorTask = mirrorUtility.GetMirrorAsync(
            $"{url}/all",
            excludeMirrors: mirrorsVisited,
            cancellationToken: ct
        );
        var getContentTask = pythonApiProxy.GetStringAsync(url, ct);
        var results = await Task.WhenAll(mirrorTask, getContentTask);

        var (mirror, content) = (results[0], results[1]);
        var link = WindowLocationRx().Match(content).Groups[1].Value;
        var linkSplit = link.Split('/');

        linkSplit[6] = mirror;

        var downloadLink = string.Join("/", linkSplit);

        mirrorsVisited.Add(mirror);

        return await getDbolicalUrlSvc.GetDiabolicalUrlAsync(downloadLink, ct);
    }

    [GeneratedRegex("""window.location.href="(.+)";""")]
    private static partial Regex WindowLocationRx();
}

public class ModDbUtilityException(string msg) : Exception(msg);
