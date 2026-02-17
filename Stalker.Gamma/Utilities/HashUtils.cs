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

        await StreamChunkFast.ChunkAsync(
            stream,
            chunkFunc: args =>
            {
                hasher.AppendData(args.Buffer, 0, args.BytesRead);
                onProgress?.Invoke((double)args.TotalBytesRead / fileSize);
                return Task.CompletedTask;
            },
            cancellationToken: cancellationToken
        );

        return Convert.ToHexStringLower(hasher.GetHashAndReset());
    }
}
