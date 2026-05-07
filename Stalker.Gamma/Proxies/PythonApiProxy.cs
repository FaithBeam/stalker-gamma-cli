using Stalker.Gamma.Factories;
using Stalker.Gamma.Models;
using Stalker.Gamma.Proxies.PythonApiClient.Models;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Proxies;

public class PythonApiProxy(
    PythonApiClientFactory pythonApiClientFactory,
    IHttpClientFactory hcf,
    StalkerGammaSettings settings
)
{
    public async Task<IDictionary<string, object>> GetHeadersAsync(
        string url,
        CancellationToken cancellationToken = default
    )
    {
        var response = await _pythonApiClient.Navigate.PostAsync(
            new NavigateRequestDto { Url = url, FollowRedirects = false },
            cancellationToken: cancellationToken
        );
        if (response?.StatusCode is not 302)
        {
            throw new CurlServiceException("Failed to get response body");
        }

        return response.Headers?.AdditionalData!;
    }

    public async Task DownloadFileAsync(
        string url,
        string pathToDownloads,
        string fileName,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        var downloadPath = Path.Join(pathToDownloads, fileName);
        await DownloadFileFast.DownloadAsync(
            _diabolicalClient,
            url,
            downloadPath,
            onProgress,
            cancellationToken
        );
    }

    public async Task<string> GetStringAsync(
        string url,
        CancellationToken cancellationToken = default
    )
    {
        var response = await _pythonApiClient.Navigate.PostAsync(
            new NavigateRequestDto { Url = url, FollowRedirects = true },
            cancellationToken: cancellationToken
        );

        if (response?.StatusCode != 200)
        {
            throw new CurlServiceException("Failed to get response body");
        }

        if (response.Content!.Contains("It appears you are a bot"))
        {
            throw new ModDbBotDetectedException(
                "ModDb temporarily blocked you. Try again in 1 hour."
            );
        }
        return response.Content!;
    }

    public async Task<bool> Ready()
    {
        try
        {
            return await _pythonApiClient.Readyz.GetAsReadyzGetResponseAsync() is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private readonly PythonApiClient.PythonApiClient _pythonApiClient =
        pythonApiClientFactory.Create(settings.PythonApiUrl);
    private readonly StalkerGammaSettings _settings = settings;
    private readonly HttpClient _diabolicalClient = hcf.CreateClient("dlArchive");
}

public class ModDbBotDetectedException(string msg) : Exception(msg);

public class CurlServiceException : Exception
{
    public CurlServiceException(string message)
        : base(message) { }

    public CurlServiceException(string message, Exception innerException)
        : base(message, innerException) { }
}
