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

        using var response = await hc.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        await using var fs = new FileStream(
            downloadPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: bufferSize
        );
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await StreamChunkFast.ChunkAsync(
            contentStream,
            chunkFunc: async (buffer, bytesRead, totalBytesRead) =>
            {
                await fs.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                var progressPercentage = (double)totalBytesRead / totalBytes!.Value;
                onProgress?.Invoke(progressPercentage);
            },
            onCompleted: () => Task.CompletedTask,
            cancellationToken: cancellationToken
        );
    }
}
