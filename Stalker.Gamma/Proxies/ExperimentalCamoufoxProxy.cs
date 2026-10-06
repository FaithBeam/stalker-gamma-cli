using Stalker.Gamma.Factories;
using Stalker.Gamma.Models;
using Stalker.Gamma.Proxies.ExperimentalCamoufoxClient.Models;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Proxies;

public class ExperimentalCamoufoxProxy(
    ExperimentalCamoufoxClientFactory experimentalCamoufoxClientFactory,
    IHttpClientFactory hcf,
    StalkerGammaSettings settings
)
{
    public async Task<IDictionary<string, object>> GetHeadersAsync(
        string url,
        CancellationToken cancellationToken = default
    )
    {
        var response = await experimentalCamoufoxClientFactory
            .Create(PythonApiUrl)
            .Navigate.PostAsync(
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
        var response = await experimentalCamoufoxClientFactory
            .Create(PythonApiUrl)
            .Navigate.PostAsync(
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
            return await experimentalCamoufoxClientFactory.Create(PythonApiUrl).Readyz.GetAsync()
                is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private readonly HttpClient _diabolicalClient = hcf.CreateClient("dlArchive");
    private string PythonApiUrl => settings.experimentalCamoufoxServerUrl!;
}

public class ModDbBotDetectedException(string msg) : Exception(msg);

public class CurlServiceException(string message) : Exception(message);
