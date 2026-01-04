namespace Stalker.Gamma.Shortcut;

public class Shortcut
{
    public void Create(string dir, string modPackPath)
    {
        if (OperatingSystem.IsWindows())
        {
            CreateShortcutWindows.Create(
                Path.Join(dir, "stalker-gamma-gui.exe"),
                Path.Join(modPackPath, "modpack_data", "modpack_icon.ico"),
                "G.A.M.M.A. Launcher.lnk"
            );
        }
    }
}
