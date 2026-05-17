using CurlService = Stalker.Gamma.Services.CurlService;

namespace Stalker.Gamma.ModDb.Services;

public class ModDbGetCdnLinkService(CurlService curlService)
{
    public async Task<string?> ExecuteAsync(string moddbMirrorUrl, CancellationToken ct)
    {
        var headers = await curlService.GetHeadersAsync(moddbMirrorUrl, ct);
        if (headers.TryGetValue("location", out var location)) { }
        return location;
    }
}
