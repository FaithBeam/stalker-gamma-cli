using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Stalker.Gamma.Models;
using Stalker.Gamma.Proxies.StalkerGammaCliServerClient;

namespace Stalker.Gamma.Factories;

public class StalkerGammaCliServerClientFactory(StalkerGammaSettings settings)
{
    public StalkerGammaCliServerClient Create() =>
        new(
            new HttpClientRequestAdapter(new AnonymousAuthenticationProvider())
            {
                BaseUrl = settings.StalkerGammaCliServerUrl,
            }
        );
}
