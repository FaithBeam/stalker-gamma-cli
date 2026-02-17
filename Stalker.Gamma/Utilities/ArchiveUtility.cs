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
                    await extractFunc.Invoke(
                        new ArchiveMappingArgs(archivePath, destinationDir, pct, ct)
                    );
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

    private record ArchiveMappingArgs(
        string ArchivePath,
        string DestinationDir,
        Action<double> Pct,
        CancellationToken Ct = default
    );

    private readonly Dictionary<int, Func<ArchiveMappingArgs, Task>> _archiveMappings = new()
    {
        {
            0x37,
            async args =>
                await sevenZipUtility.ExtractAsync(
                    args.ArchivePath,
                    args.DestinationDir,
                    args.Pct,
                    cancellationToken: args.Ct
                )
        },
        {
            0x50,
            async args =>
            {
                if (OperatingSystem.IsLinux())
                {
                    await unzipUtility.ExtractAsync(
                        args.ArchivePath,
                        args.DestinationDir,
                        args.Pct,
                        args.Ct
                    );
                }
                else
                {
                    await tarUtility.ExtractAsync(
                        args.ArchivePath,
                        args.DestinationDir,
                        args.Pct,
                        args.Ct
                    );
                }
            }
        },
        {
            0x52,
            async args =>
                await sevenZipUtility.ExtractAsync(
                    args.ArchivePath,
                    args.DestinationDir,
                    args.Pct,
                    cancellationToken: args.Ct
                )
        },
    };
}

public class ArchiveUtilityException(string message) : Exception(message);
