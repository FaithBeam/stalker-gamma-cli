using Stalker.Gamma.Proxies;

namespace Stalker.Gamma.GammaInstallerServices;

public class GetDbolicalUrl(PythonApiProxy pythonApiProxy)
{
    public async Task<string?> GetDiabolicalUrlAsync(string moddbMirrorUrl, CancellationToken ct)
    {
        var headers = await pythonApiProxy.GetHeadersAsync(moddbMirrorUrl, ct);
        return headers.TryGetValue("location", out var locationValue)
            ? locationValue.ToString()
            : null;
    }
}
