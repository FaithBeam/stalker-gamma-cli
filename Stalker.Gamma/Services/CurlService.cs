using LibCurlImpersonate;

namespace Stalker.Gamma.Services;

public class CurlService
{
    public Task<Dictionary<string, string>> GetHeadersAsync(
        string url,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () => CurlHttp.GetHeaders(url, http3: true, ct: cancellationToken),
            cancellationToken
        );

    public Task DownloadFileAsync(
        string url,
        string pathToDownloads,
        string fileName,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () =>
                CurlHttp.DownloadFile(
                    url,
                    Path.Join(pathToDownloads, fileName),
                    overwrite: true,
                    http3: true,
                    onProgress: onProgress,
                    ct: cancellationToken
                ),
            cancellationToken
        );

    public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) =>
        Task.Run(() => CurlHttp.Fetch(url, http3: true, ct: cancellationToken), cancellationToken);

    /// <summary>
    /// Whether curl service found curl-impersonate-win.exe and can execute.
    /// </summary>
    public bool Ready => true;
}

public class ModDbBotDetectedException(string msg) : Exception(msg);

public class CurlServiceException(string message) : Exception(message);

/// <summary>
/// Exit code 35
/// </summary>
/// <param name="message"></param>
public class CurlTlsConnectErrorException(string message) : Exception(message);
