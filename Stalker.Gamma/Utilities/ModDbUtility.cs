using System.Text.RegularExpressions;

namespace Stalker.Gamma.Utilities;

public partial class ModDbUtility(MirrorUtility mirrorUtility, CurlUtility curlUtility)
{
    /// <summary>
    /// Downloads from ModDB using curl.
    /// </summary>
    public async Task<string?> GetModDbLinkCurl(
        string url,
        string output,
        Action<double> onProgress,
        CancellationToken? cancellationToken = null,
        bool invalidateMirrorCache = false,
        int retryCount = 0,
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

        cancellationToken ??= CancellationToken.None;
        try
        {
            var mirrorTask = Task.Run(
                () =>
                    mirrorUtility.GetMirrorAsync(
                        $"{url}/all",
                        invalidateMirrorCache,
                        excludeMirrors: excludeMirrors ?? []
                    ),
                (CancellationToken)cancellationToken
            );
            var getContentTask = Task.Run(
                () => curlUtility.GetStringAsync(url, cancellationToken),
                (CancellationToken)cancellationToken
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

            await curlUtility.DownloadFileAsync(
                downloadLink,
                parentPath?.FullName ?? "./",
                Path.GetFileName(output),
                onProgress
            );

            if (await IsBadMirrorAsync(output))
            {
                // retry download with a different mirror
                await GetModDbLinkCurl(
                    url,
                    output,
                    onProgress,
                    cancellationToken,
                    invalidateMirrorCache,
                    retryCount + 1,
                    excludeMirrors: [.. excludeMirrors ?? [], mirror]
                );
            }

            return mirror;
        }
        catch (Exception e)
        {
            throw new ModDbUtilityException("Error downloading from ModDB", e);
        }
    }

    /// <summary>
    /// Read the first byte of the file to determine if it's a bad mirror
    /// </summary>
    /// <param name="pathToArchive"></param>
    /// <returns></returns>
    private static async Task<bool> IsBadMirrorAsync(string pathToArchive)
    {
        // hex for character <
        // <!DOCTYPE html>
        const int badByte = 0x3C;
        await using var fs = File.OpenRead(pathToArchive);
        // the download size for a bad mirror is an HTML page ~5kb, so we know it's bad.
        // check if the size is less than 10kb to give us wiggle room
        if (fs.Length > 10000)
        {
            return false;
        }
        fs.Seek(0, SeekOrigin.Begin);
        var readByte = fs.ReadByte();
        if (readByte == badByte)
        {
            fs.Seek(0, SeekOrigin.Begin);
            using var sr = new StreamReader(fs);
            var contents = await sr.ReadToEndAsync();
            if (contents.Contains("An error has occurred loading the file mirror"))
            {
                return true;
            }
        }
        return false;
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
