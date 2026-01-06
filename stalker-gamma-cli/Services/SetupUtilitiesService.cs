using Stalker.Gamma.Models;

namespace stalker_gamma_cli.Services;

public class SetupUtilitiesService(StalkerGammaSettings settings)
{
    public void Setup()
    {
        settings.PathToCurl = Path.Join(
            _resourcesPath,
            OperatingSystem.IsWindows() ? "curl.exe" : "curl-impersonate"
        );
        settings.PathTo7Z = Path.Join(
            _resourcesPath,
            OperatingSystem.IsWindows() ? "7zz.exe" : "7zz"
        );
        settings.PathToGit = OperatingSystem.IsWindows()
            ? Path.Join(_resourcesPath, "git", "cmd", "git.exe")
            : "git";
    }

    private static readonly string _resourcesPath = Path.Join(
        Path.GetDirectoryName(AppContext.BaseDirectory),
        "resources"
    );
}
