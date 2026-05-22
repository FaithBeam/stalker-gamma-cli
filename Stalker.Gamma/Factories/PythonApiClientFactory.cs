using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Stalker.Gamma.Proxies.PythonApiClient;

namespace Stalker.Gamma.Factories;

public class PythonApiClientFactory
{
    public PythonApiClient Create(string baseUrl) =>
        new(
            new HttpClientRequestAdapter(new AnonymousAuthenticationProvider())
            {
                BaseUrl = baseUrl,
            }
        );
}
