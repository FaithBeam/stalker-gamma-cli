using System.Buffers;

namespace Stalker.Gamma.Utilities;

public static class StreamChunkFast
{
    public record ChunkFuncArgs(byte[] Buffer, int BytesRead, long TotalBytesRead);

    public static async Task ChunkAsync(
        Stream stream,
        Func<ChunkFuncArgs, Task> chunkFunc,
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
                await chunkFunc(new ChunkFuncArgs(buffer, bytesRead, totalBytesRead += bytesRead));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private const int BufferLen = 1024 * 1024;
}
