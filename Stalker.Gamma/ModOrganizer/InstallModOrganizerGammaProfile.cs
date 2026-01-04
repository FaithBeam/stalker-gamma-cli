namespace Stalker.Gamma.ModOrganizer;

public static class InstallModOrganizerGammaProfile
{
    public static async Task InstallAsync(
        string stalkerGammaRepoPath,
        string gammaPath,
        string? profileName = "G.A.M.M.A"
    )
    {
        var gammaProfilesPath = Path.Join(gammaPath, "profiles", profileName);
        var settingsPath = Path.Join(gammaProfilesPath, "settings.txt");
        Directory.CreateDirectory(gammaProfilesPath);
        File.Copy(
            Path.Join(stalkerGammaRepoPath, "G.A.M.M.A", "modpack_data", "modlist.txt"),
            Path.Join(gammaProfilesPath, "modlist.txt"),
            true
        );
        await File.WriteAllTextAsync(
            settingsPath,
            """
            [General]
            LocalSaves=false
            LocalSettings=true
            AutomaticArchiveInvalidation=false
            """
        );
    }
}
