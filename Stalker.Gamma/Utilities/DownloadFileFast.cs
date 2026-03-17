namespace Stalker.Gamma.Utilities;

public static class DownloadFileFast
{
    public static async Task DownloadAsync(
        HttpClient hc,
        string url,
        string downloadPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        const int bufferSize = 1024 * 1024;

        try
        {
            using var response = await hc.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            if (totalBytes is null)
            {
                throw new DownloadFileFastException(
                    $"""
                    Content-Length header not found
                    Url: {url}
                    Download Path: {downloadPath}
                    """
                );
            }

            await using var fs = new FileStream(
                downloadPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: bufferSize
            );
            await using var contentStream = await response.Content.ReadAsStreamAsync(
                cancellationToken
            );

            await StreamChunkFast.ChunkAsync(
                contentStream,
                chunkFunc: async args =>
                {
                    await fs.WriteAsync(args.Buffer.AsMemory(0, args.BytesRead), cancellationToken);
                    var progressPercentage = (double)args.TotalBytesRead / totalBytes!.Value;
                    onProgress?.Invoke(progressPercentage);
                },
                cancellationToken: cancellationToken
            );
        }
        catch (Exception e)
        {
            throw new DownloadFileFastException(
                $"""
                Error downloading file
                Url: {url}
                Download Path: {downloadPath}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }
}

public class DownloadFileFastException : Exception
{
    public DownloadFileFastException(string msg)
        : base(msg) { }

    public DownloadFileFastException(string msg, Exception innerException)
        : base(msg, innerException) { }
}
