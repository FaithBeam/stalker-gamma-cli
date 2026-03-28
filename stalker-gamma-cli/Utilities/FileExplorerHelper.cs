using System.Diagnostics;
using System.Runtime.InteropServices;

namespace stalker_gamma_cli.Utilities;

public static class FileExplorerHelper
{
    public static void OpenFolderInExplorer(string path)
    {
        if (!Directory.Exists(path))
        {
            Console.WriteLine($"Directory not found: {path}");
            return;
        }

        // Normalize path separators
        path = $"\"{Path.GetFullPath(path)}\"";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start("explorer", path);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", path);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", path);
        }
        else
        {
            Console.WriteLine("Unsupported operating system.");
        }
    }
}
