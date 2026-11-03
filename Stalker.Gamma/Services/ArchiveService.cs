using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Services;

public class ArchiveService(SevenZipService sevenZipService)
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
            await sevenZipService.ExtractAsync(
                archivePath,
                destinationDir,
                pct,
                cancellationToken: ct
            );
            return;
        }

        try
        {
            await sevenZipService.ExtractAsync(
                archivePath,
                destinationDir,
                pct,
                cancellationToken: ct
            );
            // zips built on windows can use backslashes as separators, which 7z on
            // unix keeps as literal characters in the file name
            NormalizeBackslashPaths(destinationDir);
        }
        finally
        {
            // Permissions are a pain in my ass
            DirUtils.NormalizePermissions(destinationDir);
        }
    }

    /// <summary>
    /// Turns files named like "gamedata\scripts\a.script" into gamedata/scripts/a.script.
    /// </summary>
    private static void NormalizeBackslashPaths(string destinationDir)
    {
        var root = Path.GetFullPath(destinationDir);
        var rootWithSep = Path.EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;

        var files = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => Path.GetRelativePath(root, f).Contains('\\'))
            .ToList();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
            var destPath = Path.GetFullPath(Path.Join(root, relativePath));
            if (!destPath.StartsWith(rootWithSep, StringComparison.Ordinal))
            {
                throw new ArchiveUtilityException(
                    $"""
                    Zip entry would extract outside of destination
                    Entry: {relativePath}
                    """
                );
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Move(file, destPath, overwrite: true);
        }

        // directories whose own name contains a backslash are now empty leftovers
        var badDirs = Directory
            .EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Where(d => Path.GetFileName(d).Contains('\\'))
            .OrderByDescending(d => d.Length)
            .ToList();
        foreach (var dir in badDirs)
        {
            var relativePath = Path.GetRelativePath(root, dir).Replace('\\', '/');
            Directory.CreateDirectory(Path.Join(root, relativePath));
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
    }
}

public class ArchiveUtilityException(string message) : Exception(message);
