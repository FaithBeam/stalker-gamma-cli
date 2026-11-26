using System.Net;
using LibCurlImpersonate;
using Stalker.Gamma.ModDb.Services;
using Stalker.Gamma.Proxies;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Services;

public class NetworkService(
    IHttpClientFactory hcf,
    StalkerGammaServerProxy stalkerGammaServerProxy,
    bool useCurl
)
{
    public async Task<Dictionary<string, string>> GetHeadersAsync(
        string url,
        CancellationToken cancellationToken = default
    ) =>
        useCurl
            ? await Task.Run(
                () => CurlHttp.GetHeaders(url, http3: true, ct: cancellationToken),
                cancellationToken
            )
            : (await stalkerGammaServerProxy.GetHeadersAsync(url, cancellationToken)).ToDictionary(
                x => x.Key,
                x => x.Value.ToString()!
            );

    public async Task DownloadFileAsync(
        string url,
        string pathToDownloads,
        string fileName,
        Action<DownloadProgress>? onProgress = null,
        CancellationToken cancellationToken = default,
        string? expectedMd5 = null
    )
    {
        await DownloadFileFast.DownloadAsync(
            _dlAddonHc,
            url,
            Path.Join(pathToDownloads, fileName),
            onProgress,
            cancellationToken,
            expectedMd5: expectedMd5
        );
    }

    public async Task<string> GetStringAsync(
        string url,
        CancellationToken cancellationToken = default
    )
    {
        if (!useCurl)
        {
            return CloudflareChallenge.ThrowIfChallenged(
                await stalkerGammaServerProxy.GetStringAsync(url, cancellationToken),
                url
            );
        }

        var response = await Task.Run(
            () => CurlHttp.FetchResponse(url, http3: true, ct: cancellationToken),
            cancellationToken
        );
        // Cloudflare serves its challenge with an error status, so check for it first to report it as one
        CloudflareChallenge.ThrowIfChallenged(response.Body, url);
        if (response.StatusCode >= 400)
        {
            throw new HttpRequestException(
                $"Request to {url} failed with HTTP status {response.StatusCode}",
                null,
                (HttpStatusCode)response.StatusCode
            );
        }
        return response.Body;
    }

    private readonly HttpClient _dlAddonHc = hcf.CreateClient("dlAddon");
}
