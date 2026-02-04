using System.Buffers;
using System.IO.Compression;
using System.IO.Enumeration;
using System.Security.Cryptography;

namespace stalker_gamma_cli.Utilities;

public enum HashType
{
    Sha256,
    Md5,
}

public static class HashUtility
{
    public static async Task Hash(
        string destinationArchive,
        string anomaly,
        string gamma,
        string cache,
        HashType hashType = HashType.Sha256,
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
        var files = GetFiles(anomaly, nameof(anomaly))
            .Concat(GetFiles(cache, nameof(cache)))
            .Concat(GetFiles(gamma, nameof(gamma)))
            .ToList();
        var total = files.Count;
        var kvpHash = GenerateFileHashesAsync(files, hashType, cancellationToken);
        var completed = 0;
        await foreach (var kvp in kvpHash)
        {
            (await kvp).Deconstruct(out var hash, out var path);
            await fs.WriteLineAsync($"{hash}\t{path}");
            onProgress?.Invoke(++completed / (double)total);
        }
    }

    private static IEnumerable<(
        FileSystemInfo fsi,
        string folderPath,
        string folderPathReplacement
    )> GetFiles(string path, string folderPathReplacement) =>
        new FileSystemEnumerable<FileSystemInfo>(
            path,
            transform: (ref entry) => entry.ToFileSystemInfo(),
            new EnumerationOptions { RecurseSubdirectories = true }
        )
            .Where(x =>
                string.IsNullOrWhiteSpace(x.LinkTarget)
                && !x.Attributes.HasFlag(FileAttributes.Directory)
            )
            .Select(x => (x, folderName: path, folderPathReplacement));

    private static IAsyncEnumerable<Task<KeyValuePair<string, string>>> GenerateFileHashesAsync(
        IEnumerable<(FileSystemInfo fsi, string folderPath, string folderPathReplacement)> paths,
        HashType hashType,
        CancellationToken cancellationToken
    ) =>
        paths
            .ToAsyncEnumerable()
            .Select(async x => new KeyValuePair<string, string>(
                hashType switch
                {
                    HashType.Sha256 => await Sha256HashFile(x.fsi.FullName, cancellationToken),
                    HashType.Md5 => await Md5HashFile(x.fsi.FullName, cancellationToken),
                    _ => throw new ArgumentOutOfRangeException(nameof(hashType), hashType, null),
                },
                x.fsi.FullName.Replace(
                        x.folderPath,
                        x.folderPathReplacement,
                        StringComparison.OrdinalIgnoreCase
                    )
                    .Replace("\\", "/")
            ));

    private static async Task<string> Sha256HashFile(
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

    private const int BufferLen = 1024 * 1024;
}
