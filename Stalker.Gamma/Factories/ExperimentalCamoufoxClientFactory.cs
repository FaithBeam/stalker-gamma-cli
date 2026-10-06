using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Stalker.Gamma.Proxies.ExperimentalCamoufoxClient;

namespace Stalker.Gamma.Factories;

public class ExperimentalCamoufoxClientFactory
{
    public ExperimentalCamoufoxClient Create(string baseUrl) =>
        new(
            new HttpClientRequestAdapter(new AnonymousAuthenticationProvider())
            {
                BaseUrl = baseUrl,
            }
        );
}
