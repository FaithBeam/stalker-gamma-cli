using Stalker.Gamma.Factories;

namespace Stalker.Gamma.ModDb.Services;

public class ModDbGetCdnLinkService(NetworkServiceFactory networkServiceFactory)
{
    public async Task<string?> ExecuteAsync(string moddbMirrorUrl, CancellationToken ct = default)
    {
        var headers = await networkServiceFactory
            .Create()
            .GetHeadersAsync(moddbMirrorUrl, cancellationToken: ct);
        if (headers.TryGetValue("location", out var location)) { }
        return location;
    }
}
