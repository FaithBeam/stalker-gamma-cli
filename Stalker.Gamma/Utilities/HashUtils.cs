using System.Buffers;
using System.Security.Cryptography;

namespace Stalker.Gamma.Utilities;

public static class HashUtils
{
    public static async Task<string> HashFile(
        string path,
        HashAlgorithmName hashAlgorithmName,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        using var hasher = IncrementalHash.CreateHash(hashAlgorithmName);
        await using var stream = File.OpenRead(path);
        var fileSize = stream.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(BufferLen);
        try
        {
            long totalBytesRead = 0;
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hasher.AppendData(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                onProgress?.Invoke((double)totalBytesRead / fileSize);
            }

            return Convert.ToHexStringLower(hasher.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private const int BufferLen = 1024 * 1024;
}
