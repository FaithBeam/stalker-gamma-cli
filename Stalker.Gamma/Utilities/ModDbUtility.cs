using System.Text.RegularExpressions;
using Stalker.Gamma.GammaInstallerServices;

namespace Stalker.Gamma.Utilities;

public partial class ModDbUtility(
    MirrorUtility mirrorUtility,
    CurlUtility curlUtility,
    GetDbolicalUrl getDbolicalUrlSvc
)
{
    /// <summary>
    /// Downloads from ModDB using curl.
    /// </summary>
    public async Task<string?> GetModDbLinkCurl(
        string url,
        string output,
        Action<double> onProgress,
        bool invalidateMirrorCache = false,
        int retryCount = 0,
        CancellationToken cancellationToken = default,
        params string[]? excludeMirrors
    )
    {
        if (retryCount > 3)
        {
            throw new ModDbUtilityException(
                $"""
                Too many retries
                {url}
                Mirrors tried: {string.Join(", ", excludeMirrors ?? [])}
                """
            );
        }

        try
        {
            var mirrorTask = Task.Run(
                () =>
                    mirrorUtility.GetMirrorAsync(
                        $"{url}/all",
                        invalidateMirrorCache,
                        excludeMirrors: excludeMirrors ?? [],
                        cancellationToken: cancellationToken
                    ),
                cancellationToken
            );
            var getContentTask = Task.Run(
                () => curlUtility.GetStringAsync(url, cancellationToken),
                cancellationToken
            );
            var results = await Task.WhenAll(mirrorTask, getContentTask);

            var (mirror, content) = (results[0], results[1]);
            var link = WindowLocationRx().Match(content).Groups[1].Value;
            var linkSplit = link.Split('/');

            linkSplit[6] = mirror;

            var downloadLink = string.Join("/", linkSplit);
            var parentPath = Directory.GetParent(output);
            if (parentPath is not null && !parentPath.Exists)
            {
                parentPath.Create();
            }

            var dbolicalLink = await getDbolicalUrlSvc.GetDbolicalUrlAsync(
                downloadLink,
                cancellationToken
            );

            // if bad mirror
            if (string.IsNullOrWhiteSpace(dbolicalLink))
            {
                await GetModDbLinkCurl(
                    url,
                    output,
                    onProgress,
                    invalidateMirrorCache,
                    retryCount + 1,
                    cancellationToken,
                    excludeMirrors: [.. excludeMirrors ?? [], mirror]
                );
            }
            else
            {
                await curlUtility.DownloadFileAsync(
                    dbolicalLink,
                    parentPath?.FullName ?? "./",
                    Path.GetFileName(output),
                    onProgress,
                    cancellationToken: cancellationToken
                );
            }

            return mirror;
        }
        catch (Exception e)
        {
            throw new ModDbUtilityException("Error downloading from ModDB", e);
        }
    }

    [GeneratedRegex("""window.location.href="(.+)";""")]
    private static partial Regex WindowLocationRx();
}

public class ModDbUtilityException : Exception
{
    public ModDbUtilityException(string msg)
        : base(msg) { }

    public ModDbUtilityException(string msg, Exception innerException)
        : base(msg, innerException) { }
}
