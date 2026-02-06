using System.Buffers;

namespace Stalker.Gamma.Utilities;

public static class StreamChunkFast
{
    public static async Task<T> ChunkAsync<T>(
        Stream stream,
        Func<byte[], int, long, Task> chunkFunc,
        Func<T> onCompleted,
        CancellationToken cancellationToken = default
    )
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferLen);
        try
        {
            int bytesRead;
            long totalBytesRead = 0;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await chunkFunc(buffer, bytesRead, totalBytesRead += bytesRead);
            }

            return onCompleted();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private const int BufferLen = 1024 * 1024;
}
