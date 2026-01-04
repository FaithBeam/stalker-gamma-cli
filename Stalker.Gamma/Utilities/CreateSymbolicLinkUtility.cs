namespace Stalker.Gamma.Utilities;

public static class CreateSymbolicLinkUtility
{
    public static void Create(string path, string pathToTarget)
    {
        if (!Directory.Exists(path))
        {
            // windows requires elevation for symbolic links
            if (OperatingSystem.IsWindows())
            {
                PowerShellUtility.Execute(
                    $"New-Item -ItemType SymbolicLink -Path {path} -Value {pathToTarget}"
                );
            }
            else
            {
                Directory.CreateSymbolicLink(path, pathToTarget);
            }
        }
    }
}
