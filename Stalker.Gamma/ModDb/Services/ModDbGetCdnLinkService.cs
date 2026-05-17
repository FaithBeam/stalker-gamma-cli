using System.Text.RegularExpressions;
using Stalker.Gamma.Utilities;
using CurlService = Stalker.Gamma.Services.CurlService;

namespace Stalker.Gamma.ModDb.Services;

public partial class ModDbGetCdnLinkService(CurlService curlService)
{
    public async Task<string?> ExecuteAsync(string moddbMirrorUrl, CancellationToken ct)
    {
        var headers = await curlService.GetHeadersAsync(moddbMirrorUrl, ct);
        var location = LocationRx().Match(headers.StdOut).Groups["location"].Value;
        return location;
    }

    [GeneratedRegex("^location: (?<location>.*)$", RegexOptions.Multiline)]
    private partial Regex LocationRx();
}
