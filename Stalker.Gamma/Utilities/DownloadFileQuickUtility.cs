using System.Buffers;

namespace Stalker.Gamma.Utilities;

public static class DownloadFileQuickUtility
{
    public static async Task DownloadAsync(HttpClient hc, string url, string downloadPath, Action<double>? onProgress = null, CancellationToken cancellationToken = default)
    {
        const int bufferSize = 1024 * 1024;

        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
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
            await using var contentStream = await response.Content.ReadAsStreamAsync(
                cancellationToken
            );

            long totalBytesRead = 0;
            int bytesRead;

            while (
                (
                    bytesRead = await contentStream.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken
                    )
                ) > 0
            )
            {
                await fs.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                if (totalBytes.HasValue)
                {
                    onProgress?.Invoke((double)totalBytesRead / totalBytes.Value);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}