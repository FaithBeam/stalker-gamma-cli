using System.Security.Cryptography;

namespace Stalker.Gamma.Utilities;

public static class CachedAddonArchive
{
    public static string Resolve(
        string archiveName,
        string downloadsDirectory,
        string? cacheDirectory
    )
    {
        var destination = Path.Join(downloadsDirectory, archiveName);
        if (string.IsNullOrWhiteSpace(archiveName))
        {
            return destination;
        }

        foreach (var directory in CandidateDirectories(downloadsDirectory, cacheDirectory))
        {
            var candidate = Path.Join(directory, archiveName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return destination;
    }

    public static async Task<bool> NeedsDownloadAsync(
        string path,
        string? expectedMd5,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(path))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(expectedMd5))
        {
            return false;
        }

        var actual = await HashUtils.HashFile(
            path,
            HashAlgorithmName.MD5,
            onProgress,
            cancellationToken
        );
        return !actual.Equals(expectedMd5, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> CandidateDirectories(
        string downloadsDirectory,
        string? cacheDirectory
    )
    {
        if (!string.IsNullOrWhiteSpace(downloadsDirectory))
        {
            yield return downloadsDirectory;
        }

        if (
            !string.IsNullOrWhiteSpace(cacheDirectory)
            && !PathsEqual(downloadsDirectory, cacheDirectory)
        )
        {
            yield return cacheDirectory;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal
            );
        }
        catch (Exception)
        {
            return false;
        }
    }
}
