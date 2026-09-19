using System.Net;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Models;
using Xunit;

namespace Stalker.Gamma.Tests;

public sealed class GetStalkerModsFromApiTests
{
    private const string GitHubListBody = """
         Audio
        https://www.moddb.com/addons/start/222467	0	 - Grokitach	 Main Menu Theme - Deathcard Cabin	https://www.moddb.com/mods/stalker-anomaly/addons/groks-main-menu-theme-deathcard-cabin
        """;

    [Theory]
    [InlineData(
        "https://stalker-gamma.com/api/client/v1/mods/list",
        HttpStatusCode.InternalServerError
    )]
    [InlineData("https://stalker-gamma.com/api/list", HttpStatusCode.NotFound)]
    public async Task FallsBackToGitHubListWhenConfiguredUrlIsUnavailable(
        string configuredUrl,
        HttpStatusCode statusCode
    )
    {
        using var handler = new ScriptedHandler(request =>
        {
            if (IsObsoleteOfficialList(request.RequestUri))
            {
                return new HttpResponseMessage(statusCode);
            }

            if (request.RequestUri!.AbsoluteUri == ModPackMakerUrls.Default)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GitHubListBody),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var api = new GetStalkerModsFromApi(
            new StalkerGammaSettings { ModpackMakerList = configuredUrl },
            new HandlerHttpClientFactory(handler)
        );

        var result = await api.GetModsAsync(configuredUrl, TestContext.Current.CancellationToken);

        Assert.Equal(GitHubListBody, result);
    }

    private static bool IsObsoleteOfficialList(Uri? uri) =>
        uri is not null
        && uri.Host.Equals("stalker-gamma.com", StringComparison.OrdinalIgnoreCase)
        && (
            uri.AbsolutePath.Equals("/api/client/v1/mods/list", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Equals("/api/list", StringComparison.OrdinalIgnoreCase)
        );

    private sealed class HandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(respond(request));
    }
}
