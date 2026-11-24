using System.Net;
using System.Net.Http.Headers;

namespace Stalker.Gamma.Utilities;

public static class DownloadFileFast
{
    /// <param name="resume">
    /// Continue an existing partial file at <paramref name="downloadPath"/>. Otherwise the file
    /// is downloaded next to it and only moved to <paramref name="downloadPath"/> once complete,
    /// so an interrupted download never leaves a truncated archive that looks finished.
    /// </param>
    public static async Task DownloadAsync(
        HttpClient hc,
        string url,
        string downloadPath,
        Action<DownloadProgress>? onProgress = null,
        CancellationToken cancellationToken = default,
        bool resume = false
    )
    {
        if (resume)
        {
            await DownloadToAsync(hc, url, downloadPath, onProgress, resume, cancellationToken);
            return;
        }

        var partialPath = $"{downloadPath}.part";
        try
        {
            await DownloadToAsync(hc, url, partialPath, onProgress, resume, cancellationToken);
            File.Move(partialPath, downloadPath, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(partialPath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    private static async Task DownloadToAsync(
        HttpClient hc,
        string url,
        string downloadPath,
        Action<DownloadProgress>? onProgress,
        bool resume,
        CancellationToken cancellationToken
    )
    {
        const int bufferSize = 1024 * 1024;

        try
        {
            var existingBytes =
                resume && File.Exists(downloadPath) ? new FileInfo(downloadPath).Length : 0;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (existingBytes > 0)
            {
                request.Headers.Range = new RangeHeaderValue(existingBytes, null);
            }
            using var response = await hc.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            // nothing exists past the bytes already downloaded, so the file is complete
            if (
                existingBytes > 0
                && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable
            )
            {
                onProgress?.Invoke(new DownloadProgress(existingBytes, existingBytes));
                return;
            }
            response.EnsureSuccessStatusCode();

            // a server that ignores the range sends the whole file with 200 instead of 206
            var append = response.StatusCode == HttpStatusCode.PartialContent;
            var offset = append ? existingBytes : 0;

            // null when the server doesn't send Content-Length, e.g. github archive zips which are
            // generated on the fly and streamed with chunked transfer encoding
            var totalBytes = response.Content.Headers.ContentLength + offset;

            await using var fs = new FileStream(
                downloadPath,
                append ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: bufferSize
            );
            await using var contentStream = await response.Content.ReadAsStreamAsync(
                cancellationToken
            );

            long bytesRead = offset;
            await StreamChunkFast.ChunkAsync(
                contentStream,
                chunkFunc: async args =>
                {
                    await fs.WriteAsync(args.Buffer.AsMemory(0, args.BytesRead), cancellationToken);
                    bytesRead = offset + args.TotalBytesRead;
                    onProgress?.Invoke(new DownloadProgress(bytesRead, totalBytes));
                },
                cancellationToken: cancellationToken
            );

            // The size is known once the download finishes, so always report completion
            onProgress?.Invoke(new DownloadProgress(bytesRead, bytesRead));
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
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

/// <summary>
/// Progress of a download.
/// </summary>
/// <param name="BytesRead">Bytes downloaded so far</param>
/// <param name="TotalBytes">Total size of the download, or null if the server didn't report it</param>
public readonly record struct DownloadProgress(long BytesRead, long? TotalBytes)
{
    /// <summary>
    /// Fraction downloaded from 0 to 1, or null if the total size is unknown.
    /// </summary>
    public double? Percent => TotalBytes > 0 ? (double)BytesRead / TotalBytes.Value : null;
}

public class DownloadFileFastException : Exception
{
    public DownloadFileFastException(string msg)
        : base(msg) { }

    public DownloadFileFastException(string msg, Exception innerException)
        : base(msg, innerException) { }
}
