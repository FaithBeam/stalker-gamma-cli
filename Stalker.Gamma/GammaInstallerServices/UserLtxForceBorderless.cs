namespace Stalker.Gamma.GammaInstallerServices;

public static class UserLtxForceBorderless
{
    public static async Task ForceBorderless(string anomalyPath)
    {
        var appDataPath = Path.Join(anomalyPath, "appdata");
        var userLtxPath = Path.Join(appDataPath, "user.ltx");
        if (File.Exists(userLtxPath))
        {
            var text = await File.ReadAllTextAsync(userLtxPath);
            if (text.Contains("rs_screenmode fullscreen"))
            {
                await File.WriteAllTextAsync(
                    userLtxPath,
                    text.Replace("rs_screenmode fullscreen", "rs_screenmode borderless")
                );
            }
        }
    }
}
