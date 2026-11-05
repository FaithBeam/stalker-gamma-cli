using LibSevenZip;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Services;

/// <summary>
/// Extracts archives in-process with the 7-Zip library (7z.dll / 7z.so). Backslash
/// separators in zips made on Windows are handled during extraction.
/// </summary>
public class ArchiveService
{
    public async Task ExtractAsync(
        string archivePath,
        string destinationDir,
        Action<double> pct,
        CancellationToken ct
    )
    {
        try
        {
            await SevenZipArchive.ExtractAsync(archivePath, destinationDir, pct, ct);
        }
        catch (SevenZipException e)
        {
            throw new ArchiveUtilityException(
                $"""
                {e.Message}
                Archive: {archivePath}
                Destination: {destinationDir}
                """,
                e
            );
        }
        finally
        {
            if (!OperatingSystem.IsWindows())
            {
                // Permissions are a pain in my ass
                DirUtils.NormalizePermissions(destinationDir);
            }
        }
    }
}

public class ArchiveUtilityException(string message, Exception? innerException = null)
    : Exception(message, innerException);
