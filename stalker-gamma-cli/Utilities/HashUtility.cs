using System.Buffers;
using System.IO.Compression;
using System.IO.Enumeration;
using System.Security.Cryptography;
using Blake3;

namespace stalker_gamma_cli.Utilities;

public enum HashType
{
    Blake3,
    Sha256,
    Md5,
}

public static class HashUtility
{
    public static async Task Hash(
        string destinationArchive,
        string anomaly,
        string cache,
        string gamma,
        HashType hashType = HashType.Blake3,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var zaS = File.Create(destinationArchive);
        await using var za = await ZipArchive.CreateAsync(
            zaS,
            ZipArchiveMode.Create,
            cancellationToken: cancellationToken,
            leaveOpen: false,
            entryNameEncoding: null
        );
        var entry = za.CreateEntry(
            $"hashes-{hashType}-{Environment.UserName}.txt",
            CompressionLevel.SmallestSize
        );
        await using var entryStream = await entry.OpenAsync(cancellationToken);
        await using var fs = new StreamWriter(entryStream);
        var files = GetFiles(anomaly).Concat(GetFiles(cache)).Concat(GetFiles(gamma)).ToList();
        var total = files.Count;
        var kvpHash = GenerateFileHashesAsync(files, hashType, cancellationToken);
        var completed = 0;
        await foreach (var kvp in kvpHash)
        {
            (await kvp).Deconstruct(out var hash, out var path);
            await fs.WriteLineAsync($"{hash} {path}");
            onProgress?.Invoke(++completed / (double)total);
        }
    }

    private static IEnumerable<FileSystemInfo> GetFiles(string path) =>
        new FileSystemEnumerable<FileSystemInfo>(
            path,
            transform: (ref entry) => entry.ToFileSystemInfo(),
            new EnumerationOptions { RecurseSubdirectories = true }
        ).Where(x =>
            string.IsNullOrWhiteSpace(x.LinkTarget)
            && !x.Attributes.HasFlag(FileAttributes.Directory)
        );

    public static IAsyncEnumerable<Task<KeyValuePair<string, string>>> GenerateFileHashesAsync(
        IEnumerable<FileSystemInfo> paths,
        HashType hashType,
        CancellationToken cancellationToken
    ) =>
        paths
            .Select(x => x.FullName)
            .Order()
            .ToAsyncEnumerable()
            .Select(async x => new KeyValuePair<string, string>(
                hashType switch
                {
                    HashType.Blake3 => await Blake3HashFile(x, cancellationToken),
                    HashType.Sha256 => await Sha256HashFile(x, cancellationToken),
                    HashType.Md5 => await Md5HashFile(x, cancellationToken),
                    _ => throw new ArgumentOutOfRangeException(nameof(hashType), hashType, null),
                },
                x
            ));

    public static async Task<string> Sha256HashFile(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(path);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferLen);
        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            }

            sha256.TransformFinalBlock([], 0, 0);
            return Convert.ToHexStringLower(sha256.Hash!);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task<string> Md5HashFile(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        using var md5 = MD5.Create();
        await using var stream = File.OpenRead(path);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferLen);
        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
            }

            md5.TransformFinalBlock([], 0, 0);
            return Convert.ToHexStringLower(md5.Hash!);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task<string> Blake3HashFile(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        using var blake3 = Hasher.New();
        await using var stream = File.OpenRead(path);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferLen);
        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                blake3.Update(buffer.AsSpan(0, bytesRead));
            }

            return Convert.ToHexStringLower(blake3.Finalize().AsSpan());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private const int BufferLen = 1024 * 1024;
}
