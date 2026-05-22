using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Stalker.Gamma.OpenApi.GithubClient;

namespace Stalker.Gamma.Factories;

public class GithubClientFactory
{
    public GithubClient Create() =>
        new(new HttpClientRequestAdapter(new AnonymousAuthenticationProvider()));
}
