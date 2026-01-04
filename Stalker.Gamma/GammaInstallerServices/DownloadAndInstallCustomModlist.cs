namespace Stalker.Gamma.GammaInstallerServices;

public class DownloadAndInstallCustomModlist(IHttpClientFactory hcf)
{
    public async Task ExecuteAsync(
        string url,
        string destination,
        CancellationToken cancellationToken
    )
    {
        var text = await _httpClient.GetStringAsync(url, cancellationToken);
        await File.WriteAllTextAsync(destination, text, cancellationToken);
    }

    private readonly HttpClient _httpClient = hcf.CreateClient();
}

public class DownloadAndInstallModlistException(string msg, Exception innerException)
    : Exception(msg, innerException);
