using Stalker.Gamma.Models;
using Stalker.Gamma.Proxies;
using Stalker.Gamma.Services;

namespace Stalker.Gamma.Factories;

public class NetworkServiceFactory(
    IHttpClientFactory hcf,
    PythonApiProxy pythonApiProxy,
    StalkerGammaSettings settings
)
{
    public NetworkService Create() =>
        new(hcf, pythonApiProxy, useCurl: string.IsNullOrWhiteSpace(settings.PythonApiUrl));
}
