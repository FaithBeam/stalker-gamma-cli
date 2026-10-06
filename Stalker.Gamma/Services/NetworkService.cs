using LibCurlImpersonate;
using Stalker.Gamma.Proxies;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Services;

public class NetworkService(
    IHttpClientFactory hcf,
    ExperimentalCamoufoxProxy experimentalCamoufoxProxy,
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
            : (
                await experimentalCamoufoxProxy.GetHeadersAsync(url, cancellationToken)
            ).ToDictionary(x => x.Key, x => x.Value.ToString()!);

    public async Task DownloadFileAsync(
        string url,
        string pathToDownloads,
        string fileName,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        await DownloadFileFast.DownloadAsync(
            _dlAddonHc,
            url,
            Path.Join(pathToDownloads, fileName),
            onProgress,
            cancellationToken
        );
    }

    public async Task<string> GetStringAsync(
        string url,
        CancellationToken cancellationToken = default
    ) =>
        useCurl
            ? await Task.Run(
                () => CurlHttp.Fetch(url, http3: true, ct: cancellationToken),
                cancellationToken
            )
            : await experimentalCamoufoxProxy.GetStringAsync(url, cancellationToken);

    /// <summary>
    /// Whether curl service found curl-impersonate-win.exe and can execute.
    /// </summary>
    public bool Ready => true;

    private readonly HttpClient _dlAddonHc = hcf.CreateClient("dlAddon");
}

public class ModDbBotDetectedException(string msg) : Exception(msg);
