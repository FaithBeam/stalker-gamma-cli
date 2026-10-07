using Stalker.Gamma.Models;
using Stalker.Gamma.Proxies;
using Stalker.Gamma.Services;

namespace Stalker.Gamma.Factories;

public class NetworkServiceFactory(
    IHttpClientFactory hcf,
    StalkerGammaCliServerProxy stalkerGammaCliServerProxy,
    StalkerGammaSettings settings
)
{
    public NetworkService Create() =>
        new(
            hcf,
            stalkerGammaCliServerProxy,
            useCurl: settings.StalkerGammaCliServerUrl is null
                || settings.StalkerGammaCliServerUrl.Length == 0
        );
}
