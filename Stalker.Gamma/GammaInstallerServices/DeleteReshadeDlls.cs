namespace Stalker.Gamma.GammaInstallerServices;

internal static class DeleteReshadeDlls
{
    internal static void Delete(string anomalyBinPath)
    {
        var dxgiDll = Path.Join(anomalyBinPath, "dxgi.dll");
        var d3d9Dll = Path.Join(anomalyBinPath, "d3d9.dll");
        if (File.Exists(dxgiDll))
        {
            File.Delete(dxgiDll);
        }
        if (File.Exists(d3d9Dll))
        {
            File.Delete(d3d9Dll);
        }
    }
}
