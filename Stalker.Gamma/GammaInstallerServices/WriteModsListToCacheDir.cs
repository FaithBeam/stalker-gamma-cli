namespace Stalker.Gamma.GammaInstallerServices;

public static class WriteModsListToCacheDir
{
    public static async Task WriteAsync(string cacheDir, string modsList, CancellationToken ct)
    {
        var modsPath = Path.Join(cacheDir, "mods.txt");
        await File.WriteAllTextAsync(modsPath, modsList, ct);
    }
}
