using WindowsShortcutFactory;

namespace Stalker.Gamma.Shortcut;

public static class CreateShortcutWindows
{
    public static void Create(string exePath, string iconPath, string shortCutName)
    {
        var savePath = Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            shortCutName
        );
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
        }
        using var shortcut = new WindowsShortcut();
        shortcut.Path = exePath;
        shortcut.IconLocation = iconPath;
        shortcut.WorkingDirectory =
            Directory.GetParent(exePath)?.FullName
            ?? throw new CreateShortcutException(
                $"Unable to get parent directory of path: {exePath} for working directory shortcut."
            );
        shortcut.Save(savePath);
    }
}

public class CreateShortcutException(string message) : Exception(message);
