using LibSevenZip;

namespace stalker_gamma_cli.Services;

public class SetupUtilitiesService
{
    public void Setup()
    {
        SevenZipLibrary.LibraryPath = Path.Join(
            ResourcesPath,
            OperatingSystem.IsWindows() ? "7z.dll" : "7z.so"
        );
    }

    private static readonly string ResourcesPath = Path.Join(
        Path.GetDirectoryName(AppContext.BaseDirectory),
        "resources"
    );
}
