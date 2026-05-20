using Stalker.Gamma.Models;

namespace stalker_gamma_cli.Services;

public class SetupUtilitiesService(StalkerGammaSettings settings)
{
    public void Setup()
    {
        settings.PathTo7Z = Path.Join(
            ResourcesPath,
            OperatingSystem.IsWindows() ? "7zz.exe" : "7zz"
        );
    }

    private static readonly string ResourcesPath = Path.Join(
        Path.GetDirectoryName(AppContext.BaseDirectory),
        "resources"
    );
}
