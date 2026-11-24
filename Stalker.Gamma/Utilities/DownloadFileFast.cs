using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Stalker.Gamma.Utilities;

public static class DownloadFileFast
{
    /// <param name="resume">
    /// Continue an existing partial file at <paramref name="downloadPath"/>. Otherwise the file
    /// is downloaded next to it and only moved to <paramref name="downloadPath"/> once complete,
    /// so an interrupted download never leaves a truncated archive that looks finished.
    /// </param>
    /// <param name="expectedMd5">
    /// The archive's MD5, which is checked as it downloads. A mismatch throws
    /// <see cref="DownloadFileFastMd5MismatchException"/> and nothing is left behind. Since a bad
    /// file can't pass for a good one, an interrupted download keeps its partial file next to
    /// <paramref name="downloadPath"/> and the next call continues it instead of starting over.
    /// </param>
    public static async Task DownloadAsync(
        HttpClient hc,
        string url,
        string downloadPath,
        Action<DownloadProgress>? onProgress = null,
        CancellationToken cancellationToken = default,
        bool resume = false,
        string? expectedMd5 = null
    )
    {
        if (resume)
        {
            var md5 = await DownloadToAsync(
                hc,
                url,
                downloadPath,
                onProgress,
                resume,
                computeMd5: expectedMd5 is not null,
                cancellationToken
            );
            ThrowIfMd5Mismatch(url, downloadPath, expectedMd5, md5);
            return;
        }

        // without a hash a partial file from an older version of the archive can't be told apart
        // from one of this version, so it's only continued when the hash can catch it
        var resumePartial = expectedMd5 is not null;
        var partialPath = $"{downloadPath}.part";
        try
        {
            var md5 = await DownloadToAsync(
                hc,
                url,
                partialPath,
                onProgress,
                resumePartial,
                computeMd5: expectedMd5 is not null,
                cancellationToken
            );
            ThrowIfMd5Mismatch(url, partialPath, expectedMd5, md5);
            File.Move(partialPath, downloadPath, overwrite: true);
        }
        catch (Exception e) when (!resumePartial || e is DownloadFileFastMd5MismatchException)
        {
            TryDelete(partialPath);
            throw;
        }
    }

    private static void ThrowIfMd5Mismatch(
        string url,
        string path,
        string? expectedMd5,
        string? actualMd5
    )
    {
        if (
            expectedMd5 is null
            || string.Equals(expectedMd5, actualMd5, StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        TryDelete(path);
        throw new DownloadFileFastMd5MismatchException(
            $"""
            Downloaded file doesn't match its MD5
            Url: {url}
            Download Path: {path}
            Expected MD5: {expectedMd5}
            Actual MD5: {actualMd5}
            """
        );
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <returns>The file's lowercase hex MD5 when <paramref name="computeMd5"/>, else null</returns>
    private static async Task<string?> DownloadToAsync(
        HttpClient hc,
        string url,
        string downloadPath,
        Action<DownloadProgress>? onProgress,
        bool resume,
        bool computeMd5,
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
                return computeMd5
                    ? await HashUtils.HashFile(
                        downloadPath,
                        HashAlgorithmName.MD5,
                        cancellationToken: cancellationToken
                    )
                    : null;
            }
            response.EnsureSuccessStatusCode();

            // a server that ignores the range sends the whole file with 200 instead of 206
            var append = response.StatusCode == HttpStatusCode.PartialContent;
            var offset = append ? existingBytes : 0;

            using var md5 = computeMd5 ? IncrementalHash.CreateHash(HashAlgorithmName.MD5) : null;
            if (md5 is not null && append)
            {
                await AppendFileToHashAsync(md5, downloadPath, cancellationToken);
            }

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
                    md5?.AppendData(args.Buffer, 0, args.BytesRead);
                    bytesRead = offset + args.TotalBytesRead;
                    onProgress?.Invoke(new DownloadProgress(bytesRead, totalBytes));
                },
                cancellationToken: cancellationToken
            );

            // The size is known once the download finishes, so always report completion
            onProgress?.Invoke(new DownloadProgress(bytesRead, bytesRead));
            return md5 is null ? null : Convert.ToHexStringLower(md5.GetHashAndReset());
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

    private static async Task AppendFileToHashAsync(
        IncrementalHash hash,
        string path,
        CancellationToken cancellationToken
    )
    {
        await using var fs = File.OpenRead(path);
        await StreamChunkFast.ChunkAsync(
            fs,
            chunkFunc: args =>
            {
                hash.AppendData(args.Buffer, 0, args.BytesRead);
                return Task.CompletedTask;
            },
            cancellationToken: cancellationToken
        );
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

/// <summary>
/// A download finished, but its MD5 isn't the one expected, e.g. a mirror served a corrupt or
/// different file
/// </summary>
public class DownloadFileFastMd5MismatchException(string msg) : DownloadFileFastException(msg);
