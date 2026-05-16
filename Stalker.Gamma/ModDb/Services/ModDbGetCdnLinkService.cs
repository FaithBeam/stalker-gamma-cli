using System.Text.RegularExpressions;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.ModDb.Services;

public partial class ModDbGetCdnLinkService(CurlUtility curlUtility)
{
    public async Task<string?> GetDbolicalUrlAsync(string moddbMirrorUrl, CancellationToken ct)
    {
        var headers = await curlUtility.GetHeadersAsync(moddbMirrorUrl, ct);
        var location = LocationRx().Match(headers.StdOut).Groups["location"].Value;
        return location;
    }

    [GeneratedRegex("^location: (?<location>.*)$", RegexOptions.Multiline)]
    private partial Regex LocationRx();
}
