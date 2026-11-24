using LibSevenZip;

namespace Stalker.Gamma.IntegrationTests;

/// <summary>
/// A test that extracts archives with the native 7-Zip library. It's skipped when the library
/// can't be found, unless STALKER_GAMMA_REQUIRE_7Z=1, which CI sets so a missing library fails
/// the run instead of skipping it.
/// </summary>
public sealed class SevenZipFactAttribute : FactAttribute
{
    public SevenZipFactAttribute()
    {
        if (NativeSevenZip.Path is null && !NativeSevenZip.Required)
        {
            Skip =
                $"The native 7-Zip library wasn't found. Build it, or set {NativeSevenZip.PathVariable} to it";
        }
    }
}

/// <summary>
/// Finds the native 7-Zip library the CLI ships with: from STALKER_GAMMA_7Z_PATH, or else
/// where the build scripts and a local debug build leave it
/// </summary>
public static class NativeSevenZip
{
    public const string PathVariable = "STALKER_GAMMA_7Z_PATH";
    private const string RequiredVariable = "STALKER_GAMMA_REQUIRE_7Z";

    public static bool Required => Environment.GetEnvironmentVariable(RequiredVariable) == "1";

    public static string? Path { get; } = Find();

    private static readonly Lazy<bool> Loaded = new(() =>
    {
        SevenZipLibrary.LibraryPath =
            Path
            ?? throw new InvalidOperationException(
                $"{RequiredVariable}=1 but the native 7-Zip library wasn't found. Set {PathVariable} to it"
            );
        SevenZipLibrary.Load();
        return true;
    });

    /// <summary>
    /// Points the library at the native one. The library can only be loaded once per process.
    /// </summary>
    public static void EnsureLoaded() => _ = Loaded.Value;

    private static string? Find()
    {
        if (Environment.GetEnvironmentVariable(PathVariable) is { Length: > 0 } configured)
        {
            return File.Exists(configured) ? System.IO.Path.GetFullPath(configured) : null;
        }

        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            return null;
        }

        var fileName = OperatingSystem.IsWindows() ? "7z.dll" : "7z.so";
        var platform =
            OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsMacOS() ? "mac"
            : "linux";
        string[] candidates =
        [
            System.IO.Path.Join(
                repoRoot,
                "build",
                platform,
                "build",
                "stalker-gamma-cli",
                "resources",
                fileName
            ),
            System.IO.Path.Join(
                repoRoot,
                "stalker-gamma-cli",
                "bin",
                "Debug",
                "net10.0",
                "resources",
                fileName
            ),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindRepoRoot()
    {
        for (
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent
        )
        {
            if (File.Exists(System.IO.Path.Join(dir.FullName, "stalker-gamma-cli.sln")))
            {
                return dir.FullName;
            }
        }
        return null;
    }
}
