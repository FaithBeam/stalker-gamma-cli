using System.Buffers;
using System.Security.Cryptography;

namespace Stalker.Gamma.Utilities;

public static class Md5Utility
{
    public static async Task<string> CalculateFileMd5Async(
        string filePath,
        Action<double> onProgress,
        CancellationToken cancellationToken = default
    )
    {
        using var md5 = MD5.Create();
        await using var stream = File.OpenRead(filePath);

        var fileSize = stream.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(BufferLen);

        try
        {
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                totalBytesRead += bytesRead;
                onProgress.Invoke((double)totalBytesRead / fileSize);
            }

            // Finalize the hash computation
            md5.TransformFinalBlock([], 0, 0);

            return Convert.ToHexStringLower(md5.Hash!);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private const int BufferLen = 1024 * 1024;
}
