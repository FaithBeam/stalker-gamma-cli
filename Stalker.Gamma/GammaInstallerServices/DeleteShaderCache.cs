using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices;

internal static class DeleteShaderCache
{
    internal static void Delete(string anomalyPath)
    {
        var appDataPath = Path.Join(anomalyPath, "appdata");
        var shaderCachePath = Path.Join(appDataPath, "shaders_cache");
        if (Directory.Exists(shaderCachePath))
        {
            DirUtils.NormalizePermissions(shaderCachePath);
            DirUtils.RecursivelyDeleteDirectory(shaderCachePath, []);
        }
    }
}
