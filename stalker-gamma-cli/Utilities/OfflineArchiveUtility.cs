using LibSevenZip;

namespace stalker_gamma_cli.Utilities;

/// <summary>
/// Extracts an offline cache archive, as made by the Create Offline Install wiki page, so it can
/// be installed with full-install --offline-archive-path. The archive holds a cache folder plus
/// modpack_maker_list.txt and modlist.txt.
/// </summary>
public static class OfflineArchiveUtility
{
    public const string ModPackMakerListFileName = "modpack_maker_list.txt";
    public const string ModListFileName = "modlist.txt";

    /// <summary>
    /// Extracts the cache folder in <paramref name="archivePath"/> into <paramref name="cache"/>
    /// and its modpack_maker_list.txt and modlist.txt into <paramref name="listsDir"/>.
    /// Cache files that already exist with the same size and modified time are skipped, so
    /// re-running after a failure doesn't extract everything again.
    /// </summary>
    /// <param name="progress">Fraction complete, 0 to 1.</param>
    public static Task<OfflineArchiveResult> ExtractAsync(
        string archivePath,
        string cache,
        string listsDir,
        Action<double>? progress,
        CancellationToken ct
    ) => Task.Run(() => Extract(archivePath, cache, listsDir, progress, ct), ct);

    private static OfflineArchiveResult Extract(
        string archivePath,
        string cache,
        string listsDir,
        Action<double>? progress,
        CancellationToken ct
    )
    {
        if (!File.Exists(archivePath))
        {
            throw new OfflineArchiveException($"Offline archive not found: {archivePath}");
        }

        using var archive = SevenZipArchive.Open(archivePath, ct);
        var files = archive
            .Entries.Where(x => !x.IsDirectory && !x.IsAltStream)
            .Select(x => (Entry: x, Path: Normalize(x.Path)))
            .Where(x => x.Path.Length > 0 && !IsMacOsMetadata(x.Path))
            .ToList();

        var modPackMakerList = FindTopMost(files, ModPackMakerListFileName);
        var modList = FindTopMost(files, ModListFileName);
        string cachePrefix;
        while (true)
        {
            var cacheFiles = files
                .Where(x =>
                    x.Entry.Index != modPackMakerList?.Index && x.Entry.Index != modList?.Index
                )
                .Select(x => x.Path)
                .ToList();
            if (cacheFiles.Count == 0)
            {
                throw new OfflineArchiveException(
                    $"{archivePath} does not contain any cache files"
                );
            }

            // The cache folder is the deepest folder that holds every cache file, e.g. cache/
            // for archives made with `tar -cvf archive.tar ./cache/ ...`
            cachePrefix = CommonDirectoryPrefix(cacheFiles);

            // A list inside the cache folder belongs to an addon, not the archive
            var prefix = cachePrefix;
            bool InCache(ArchiveEntry? list) =>
                list is not null && IsInsideCacheFolder(Normalize(list.Path), prefix);
            if (!InCache(modPackMakerList) && !InCache(modList))
            {
                break;
            }

            modPackMakerList = InCache(modPackMakerList) ? null : modPackMakerList;
            modList = InCache(modList) ? null : modList;
        }

        var cacheRoot = Path.GetFullPath(cache);
        var listsRoot = Path.GetFullPath(listsDir);
        Directory.CreateDirectory(cacheRoot);
        Directory.CreateDirectory(listsRoot);

        var modPackMakerListPath = modPackMakerList is null
            ? null
            : Path.Join(listsRoot, ModPackMakerListFileName);
        var modListPath = modList is null ? null : Path.Join(listsRoot, ModListFileName);

        var extracted = 0;
        var skipped = 0;
        archive.ExtractTo(
            entry =>
            {
                if (entry.Index == modPackMakerList?.Index)
                {
                    return modPackMakerListPath;
                }

                if (entry.Index == modList?.Index)
                {
                    return modListPath;
                }

                if (entry.IsAltStream)
                {
                    return null;
                }

                var path = Normalize(entry.Path);
                if (
                    IsMacOsMetadata(path) || !path.StartsWith(cachePrefix, StringComparison.Ordinal)
                )
                {
                    return null;
                }

                var destination = SevenZipArchive.ResolveUnder(
                    cacheRoot,
                    path[cachePrefix.Length..]
                );
                if (!entry.IsDirectory && IsUpToDate(entry, destination))
                {
                    skipped++;
                    return null;
                }

                if (!entry.IsDirectory)
                {
                    extracted++;
                }
                return destination;
            },
            progress,
            ct
        );

        return new OfflineArchiveResult(modPackMakerListPath, modListPath, extracted, skipped);
    }

    /// <summary>
    /// Forward slashes, no leading ./ or /
    /// </summary>
    private static string Normalize(string path)
    {
        var normalized = path.Replace('\\', '/');
        while (true)
        {
            if (normalized.StartsWith("./", StringComparison.Ordinal))
            {
                normalized = normalized[2..];
            }
            else if (normalized.StartsWith('/'))
            {
                normalized = normalized[1..];
            }
            else
            {
                return normalized;
            }
        }
    }

    /// <summary>
    /// AppleDouble ._ files and __MACOSX folders that macOS tar and zip add to hold extended
    /// attributes. 7-Zip extracts them as regular files.
    /// </summary>
    private static bool IsMacOsMetadata(string path) =>
        path.StartsWith("__MACOSX/", StringComparison.Ordinal)
        || path[(path.LastIndexOf('/') + 1)..].StartsWith("._", StringComparison.Ordinal);

    /// <summary>
    /// The least nested file named <paramref name="fileName"/>, so a copy inside an addon's
    /// folder in the cache isn't mistaken for the archive's list.
    /// </summary>
    private static ArchiveEntry? FindTopMost(
        IEnumerable<(ArchiveEntry Entry, string Path)> files,
        string fileName
    ) =>
        files
            .Where(x =>
                string.Equals(
                    x.Path[(x.Path.LastIndexOf('/') + 1)..],
                    fileName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .OrderBy(x => x.Path.Count(c => c == '/'))
            .Select(x => x.Entry)
            .FirstOrDefault();

    /// <summary>
    /// Whether a list file is inside the cache folder. Lists at the top of the archive are
    /// never inside it, even when the cache files are also at the top.
    /// </summary>
    private static bool IsInsideCacheFolder(string listPath, string cachePrefix)
    {
        var listDir = listPath[..(listPath.LastIndexOf('/') + 1)];
        return listDir.Length > 0 && listDir.StartsWith(cachePrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// The longest folder path, ending in /, that every path is inside of. Empty if none.
    /// </summary>
    private static string CommonDirectoryPrefix(IList<string> paths)
    {
        var prefix = paths[0][..(paths[0].LastIndexOf('/') + 1)];
        foreach (var path in paths.Skip(1))
        {
            while (prefix.Length > 0 && !path.StartsWith(prefix, StringComparison.Ordinal))
            {
                prefix = prefix[..(prefix.LastIndexOf('/', prefix.Length - 2) + 1)];
            }
        }

        return prefix;
    }

    private static bool IsUpToDate(ArchiveEntry entry, string destination)
    {
        var fi = new FileInfo(destination);
        return fi.Exists
            && entry.Size is { } size
            && (ulong)fi.Length == size
            && entry.LastWriteTimeUtc is { } mtime
            && (fi.LastWriteTimeUtc - mtime).Duration() < TimeSpan.FromSeconds(2);
    }
}

/// <param name="ModPackMakerListPath">Extracted modpack_maker_list.txt, or null if the archive doesn't have one</param>
/// <param name="ModListPath">Extracted modlist.txt, or null if the archive doesn't have one</param>
/// <param name="Extracted">Number of cache files extracted</param>
/// <param name="Skipped">Number of cache files skipped because they were already extracted</param>
public sealed record OfflineArchiveResult(
    string? ModPackMakerListPath,
    string? ModListPath,
    int Extracted,
    int Skipped
);

public class OfflineArchiveException(string message) : Exception(message);
