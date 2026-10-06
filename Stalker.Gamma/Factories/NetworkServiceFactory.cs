using Stalker.Gamma.Models;
using Stalker.Gamma.Proxies;
using Stalker.Gamma.Services;

namespace Stalker.Gamma.Factories;

public class NetworkServiceFactory(
    IHttpClientFactory hcf,
    ExperimentalCamoufoxProxy experimentalCamoufoxProxy,
    StalkerGammaSettings settings
)
{
    public NetworkService Create() =>
        new(
            hcf,
            experimentalCamoufoxProxy,
            useCurl: settings.experimentalCamoufoxServerUrl is null
                || settings.experimentalCamoufoxServerUrl.Length == 0
        );
}
