using System.Buffers;

namespace Stalker.Gamma.Utilities;

public static class StreamChunkFast
{
    public record ChunkFuncArgs(byte[] Buffer, int BytesRead, long TotalBytesRead);

    /// <param name="readTimeout">
    /// How long a single read may wait for data before it fails with a
    /// <see cref="TimeoutException"/>, or null to wait as long as it takes. A network stream whose
    /// connection died without being closed never returns from a read, so without this a stalled
    /// download would hang forever.
    /// </param>
    public static async Task ChunkAsync(
        Stream stream,
        Func<ChunkFuncArgs, Task> chunkFunc,
        CancellationToken cancellationToken = default,
        TimeSpan? readTimeout = null
    )
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferLen);
        using var readCts = readTimeout is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            int bytesRead;
            long totalBytesRead = 0;
            while ((bytesRead = await ReadAsync(stream, buffer, readCts, readTimeout)) > 0)
            {
                await chunkFunc(new ChunkFuncArgs(buffer, bytesRead, totalBytesRead += bytesRead));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        async ValueTask<int> ReadAsync(
            Stream source,
            byte[] destination,
            CancellationTokenSource? timeoutCts,
            TimeSpan? timeout
        )
        {
            if (timeoutCts is null || timeout is null)
            {
                return await source.ReadAsync(destination, cancellationToken);
            }

            // the timer restarts for each read, and stops while the chunk is processed
            timeoutCts.CancelAfter(timeout.Value);
            try
            {
                return await source.ReadAsync(destination, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"No data was received for {timeout.Value.TotalSeconds:0.###} seconds"
                );
            }
            finally
            {
                timeoutCts.CancelAfter(Timeout.InfiniteTimeSpan);
            }
        }
    }

    private const int BufferLen = 1024 * 1024;
}
