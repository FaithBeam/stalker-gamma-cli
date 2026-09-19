using Stalker.Gamma.Services;

namespace Stalker.Gamma.Utilities;

public static class CreateSymbolicLinkUtility
{
    public static void Create(
        string path,
        string pathToTarget,
        PowerShellCmdBuilder powerShellCmdBuilder
    )
    {
        if (
            Directory.Exists(path)
            && !IsSymbolicLink(path)
            && !Directory.EnumerateFileSystemEntries(path).Any()
        )
        {
            Directory.Delete(path);
        }

        if (!Directory.Exists(path))
        {
            if (OperatingSystem.IsWindows())
            {
                powerShellCmdBuilder.WithCreateSymbolicLink(path, pathToTarget);
            }
            else
            {
                Directory.CreateSymbolicLink(path, pathToTarget);
            }
        }
    }

    private static bool IsSymbolicLink(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
