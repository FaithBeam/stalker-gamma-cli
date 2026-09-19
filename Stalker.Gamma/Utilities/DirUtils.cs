using System.Security.Cryptography;

namespace Stalker.Gamma.Utilities;

public static class DirUtils
{
    public static void NormalizePermissions(string dir)
    {
        var di = new DirectoryInfo(dir);
        if (!di.Exists)
        {
            return;
        }

        try
        {
            // 1. Normalize the directory itself
            di.Attributes &= ~FileAttributes.ReadOnly;
            if (!OperatingSystem.IsWindows())
            {
                di.UnixFileMode =
                    UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.GroupWrite
                    | UnixFileMode.OtherRead
                    | UnixFileMode.OtherExecute;
            }

            // 2. Normalize all files in this directory
            foreach (var fi in di.GetFiles())
            {
                try
                {
                    fi.IsReadOnly = false;
                    fi.Attributes &= ~FileAttributes.ReadOnly;
                }
                catch (UnauthorizedAccessException)
                { /* Skip files we can't touch */
                }
            }

            // 3. Recurse into subdirectories
            foreach (var subDir in di.GetDirectories())
            {
                NormalizePermissions(subDir.FullName);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Catching at this level allows the recursion to skip folders
            // it doesn't have permission to list
        }
    }

    public static void RecursivelyDeleteDirectory(string dir, IReadOnlyList<string> doNotMatch)
    {
        var dirInfo = new DirectoryInfo(dir);
        if (!dirInfo.Exists)
        {
            return;
        }

        foreach (var fi in dirInfo.GetFiles(".*", SearchOption.TopDirectoryOnly))
        {
            fi.Delete();
        }
        foreach (
            var d in dirInfo
                .GetDirectories()
                .Where(x => !doNotMatch.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
        )
        {
            TryDeleteDirectoryTree(d.FullName);
        }
    }

    // ponytail: retry/swallow leftover ENOTEMPTY so extract cleanup cannot abort full-install;
    // upgrade: log a non-fatal warning once the CLI has an extract-warning channel
    private static void TryDeleteDirectoryTree(string path)
    {
        const int maxAttempts = 8;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                DeleteDirectorySnapshot(path);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == maxAttempts)
                {
                    return;
                }

                NormalizePermissions(path);
                Thread.Sleep(25 * attempt);
            }
        }
    }

    private static void DeleteDirectorySnapshot(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        // Snapshot first: POSIX readdir+unlink can skip entries and then rmdir
        // throws "Directory not empty" (issue #8 on large leftover FOMOD trees).
        foreach (var file in Directory.GetFiles(path))
        {
            var fi = new FileInfo(file);
            fi.IsReadOnly = false;
            fi.Attributes &= ~FileAttributes.ReadOnly;
            fi.Delete();
        }

        foreach (var child in Directory.GetDirectories(path))
        {
            DeleteDirectorySnapshot(child);
        }

        Directory.Delete(path);
    }

    public static void CopyDirectory(
        string sourceDir,
        string destDir,
        bool overwrite = true,
        string? fileFilter = null,
        Action<double>? onProgress = null,
        Action<string>? txtProgress = null,
        bool moveFile = false,
        CancellationToken cancellationToken = default
    )
    {
        if (sourceDir.Contains(".git"))
        {
            return;
        }

        // Count total files first if progress callback is provided
        var totalFiles = 0;
        var copiedFiles = 0;

        if (onProgress != null)
        {
            totalFiles = CountFiles(sourceDir, fileFilter);
        }

        CopyDirectoryInternal(
            sourceDir,
            destDir,
            overwrite,
            fileFilter,
            onProgress,
            ref copiedFiles,
            totalFiles,
            txtProgress,
            moveFile,
            cancellationToken
        );
    }

    private static int CountFiles(string sourceDir, string? fileFilter)
    {
        if (sourceDir.Contains(".git"))
        {
            return 0;
        }

        var sourceDirInfo = new DirectoryInfo(sourceDir);
        int count = 0;

        foreach (var file in sourceDirInfo.GetFiles())
        {
            if (!string.IsNullOrWhiteSpace(fileFilter) && file.Name == fileFilter)
            {
                continue;
            }
            count++;
        }

        foreach (var subDir in sourceDirInfo.GetDirectories())
        {
            if (subDir.EnumerateFiles("*.*", SearchOption.AllDirectories).Any())
            {
                count += CountFiles(subDir.FullName, fileFilter);
            }
        }

        return count;
    }

    private static void CopyDirectoryInternal(
        string sourceDir,
        string destDir,
        bool overwrite,
        string? fileFilter,
        Action<double>? onProgress,
        ref int copiedFiles,
        int totalFiles,
        Action<string>? txtProgress = null,
        bool moveFile = false,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        txtProgress?.Invoke(
            $"""
            sourceDir = {sourceDir}
            destDir = {destDir}
            overwrite = {overwrite}
            fileFilter = {fileFilter}
            copiedFiles = {copiedFiles}
            totalFiles = {totalFiles}
            """
        );
        txtProgress?.Invoke($"Copying {sourceDir} to {destDir}");
        if (sourceDir.Contains(".git"))
        {
            txtProgress?.Invoke($"Skipping {sourceDir}, .git folder found");
            return;
        }

        Directory.CreateDirectory(destDir);
        txtProgress?.Invoke($"Created {destDir}");
        var sourceDirInfo = new DirectoryInfo(sourceDir);
        foreach (var file in sourceDirInfo.GetFiles())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            if (txtProgress is not null)
            {
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(file.FullName);
                var hashBytes = SHA256.HashData(stream);
                var hash = Convert.ToHexString(hashBytes);
                txtProgress.Invoke($"Copying {file.FullName}, SHA256 = {hash}");
            }
            if (!string.IsNullOrWhiteSpace(fileFilter) && file.Name == fileFilter)
            {
                txtProgress?.Invoke($"Skipping {file.FullName}");
                continue;
            }

            if (File.Exists(Path.Combine(destDir, file.Name)))
            {
                txtProgress?.Invoke($"File {Path.Combine(destDir, file.Name)} already exists");
                if (overwrite)
                {
                    txtProgress?.Invoke($"Overwriting {Path.Combine(destDir, file.Name)}");
                    if (moveFile)
                    {
                        file.MoveTo(Path.Combine(destDir, file.Name), overwrite);
                    }
                    else
                    {
                        file.CopyTo(Path.Combine(destDir, file.Name), overwrite);
                    }
                    copiedFiles++;
                    onProgress?.Invoke((double)copiedFiles / totalFiles);
                }
            }
            else
            {
                txtProgress?.Invoke(
                    $"Copying {file.FullName} to {Path.Combine(destDir, file.Name)}"
                );
                if (moveFile)
                {
                    file.MoveTo(Path.Combine(destDir, file.Name), overwrite);
                }
                else
                {
                    file.CopyTo(Path.Combine(destDir, file.Name), overwrite);
                }
                copiedFiles++;
                onProgress?.Invoke((double)copiedFiles / totalFiles);
            }
        }

        foreach (var subDir in sourceDirInfo.GetDirectories())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            txtProgress?.Invoke($"Copying {subDir.FullName}");
            // only copy directories if they're not empty
            if (subDir.EnumerateFiles("*.*", SearchOption.AllDirectories).Any())
            {
                CopyDirectoryInternal(
                    subDir.FullName,
                    Path.Combine(destDir, subDir.Name),
                    overwrite,
                    fileFilter,
                    onProgress,
                    ref copiedFiles,
                    totalFiles,
                    txtProgress,
                    moveFile
                );
            }
        }
    }
}
