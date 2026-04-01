using Stalker.Gamma.Models;

namespace stalker_gamma_cli.Services;

public class SetupUtilitiesService(StalkerGammaSettings settings)
{
    public void Setup()
    {
        settings.PathToCurl = Path.Join(
            ResourcesPath,
            OperatingSystem.IsWindows() ? "curl.exe" : "curl-impersonate"
        );
        settings.PathTo7Z = Path.Join(
            ResourcesPath,
            OperatingSystem.IsWindows() ? "7zz.exe" : "7zz"
        );
        settings.PathToGit = OperatingSystem.IsWindows()
            ? Path.Join(ResourcesPath, "git", "cmd", "git.exe")
            : "git";
    }

    private static readonly string ResourcesPath = Path.Join(
        Path.GetDirectoryName(AppContext.BaseDirectory),
        "resources"
    );
}
