using CurlService = Stalker.Gamma.Services.CurlService;

namespace Stalker.Gamma.ModDb.Services;

public class ModDbGetCdnLinkService(CurlService curlService)
{
    public async Task<string?> ExecuteAsync(
        string moddbMirrorUrl,
        bool useCurl = true,
        CancellationToken ct = default
    )
    {
        var headers = await curlService.GetHeadersAsync(
            moddbMirrorUrl,
            useCurl: useCurl,
            cancellationToken: ct
        );
        if (headers.TryGetValue("location", out var location)) { }
        return location;
    }
}
