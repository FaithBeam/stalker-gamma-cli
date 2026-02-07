using System.IO.Compression;
using System.IO.Enumeration;
using System.Security.Cryptography;
using Stalker.Gamma.Utilities;

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
            .Where(x => !x.folderPath.Contains("shaders"))
            .Concat(GetFiles(gamma, nameof(gamma)))
            .Where(x =>
                !x.folderPath.Contains("basic_games") && !x.folderPath.Contains("__pycache__")
            )
            .OrderBy(x => x.folderPath)
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

    private static IEnumerable<(FileSystemInfo fsi, string folderPath)> GetFiles(
        string path,
        string folderPathReplacement
    ) =>
        new FileSystemEnumerable<FileSystemInfo>(
            path,
            transform: (ref entry) => entry.ToFileSystemInfo(),
            new EnumerationOptions { RecurseSubdirectories = true }
        )
            .Where(x =>
                string.IsNullOrWhiteSpace(x.LinkTarget)
                && !x.Attributes.HasFlag(FileAttributes.Directory)
            )
            .Select(x => (x, folderName: x.FullName.Replace(path, folderPathReplacement)));

    private static IAsyncEnumerable<Task<KeyValuePair<string, string>>> GenerateFileHashesAsync(
        IEnumerable<(FileSystemInfo fsi, string folderPath)> paths,
        HashType hashType,
        CancellationToken cancellationToken
    ) =>
        paths
            .ToAsyncEnumerable()
            .Select(async x => new KeyValuePair<string, string>(
                hashType switch
                {
                    HashType.Sha256 => await HashUtils.HashFile(
                        x.fsi.FullName,
                        HashAlgorithmName.SHA256,
                        cancellationToken: cancellationToken
                    ),
                    HashType.Md5 => await HashUtils.HashFile(
                        x.fsi.FullName,
                        HashAlgorithmName.MD5,
                        cancellationToken: cancellationToken
                    ),
                    _ => throw new ArgumentOutOfRangeException(nameof(hashType), hashType, null),
                },
                x.folderPath
            ));
}
