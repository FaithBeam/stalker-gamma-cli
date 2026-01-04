using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices;

internal static class CleanExtractPath
{
    internal static void Clean(string extractPath)
    {
        if (!Directory.Exists(extractPath))
        {
            return;
        }

        DirUtils.NormalizePermissions(extractPath);

        DirUtils.RecursivelyDeleteDirectory(extractPath, DoNotMatch);
    }

    private static readonly IReadOnlyList<string> DoNotMatch =
    [
        "gamedata",
        "appdata",
        "db",
        "fomod",
    ];
}
