using System.Collections.Frozen;

namespace Stalker.Gamma.Utilities;

public class ArchiveUtility(
    SevenZipUtility sevenZipUtility,
    TarUtility tarUtility,
    UnzipUtility unzipUtility
)
{
    public async Task ExtractAsync(
        string archivePath,
        string destinationDir,
        Action<double> pct,
        CancellationToken ct
    )
    {
        if (OperatingSystem.IsWindows())
        {
            await sevenZipUtility.ExtractAsync(
                archivePath,
                destinationDir,
                pct,
                cancellationToken: ct
            );
        }
        else
        {
            await using var fs = File.OpenRead(archivePath);
            fs.Seek(0, SeekOrigin.Begin);
            if (_archiveMappings.TryGetValue(fs.ReadByte(), out var extractFunc))
            {
                try
                {
                    await extractFunc.Invoke(archivePath, destinationDir, pct, ct);
                }
                finally
                {
                    // Permissions are a pain in my ass
                    DirUtils.NormalizePermissions(destinationDir);
                }
            }
            else
            {
                throw new ArchiveUtilityException(
                    $"""
                    Unsupported archive type
                    Archive: {archivePath}
                    """
                );
            }
        }
    }

    private readonly FrozenDictionary<
        int,
        Func<string, string, Action<double>, CancellationToken, Task>
    > _archiveMappings = new Dictionary<
        int,
        Func<string, string, Action<double>, CancellationToken, Task>
    >
    {
        {
            0x37,
            async (archivePath, destinationDir, pct, ct) =>
                await sevenZipUtility.ExtractAsync(
                    archivePath,
                    destinationDir,
                    pct,
                    cancellationToken: ct
                )
        },
        {
            0x50,
            async (archivePath, destinationDir, pct, ct) =>
            {
                if (OperatingSystem.IsLinux())
                {
                    await unzipUtility.ExtractAsync(archivePath, destinationDir, pct, ct);
                }
                else
                {
                    await tarUtility.ExtractAsync(archivePath, destinationDir, pct, ct);
                }
            }
        },
        {
            0x52,
            async (archivePath, destinationDir, pct, ct) =>
                await sevenZipUtility.ExtractAsync(
                    archivePath,
                    destinationDir,
                    pct,
                    cancellationToken: ct
                )
        },
    }.ToFrozenDictionary();
}

public class ArchiveUtilityException(string message) : Exception(message);
