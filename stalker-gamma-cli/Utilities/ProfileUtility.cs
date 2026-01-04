namespace stalker_gamma_cli.Utilities;

public static class ProfileUtility
{
    public static string ValidateProfileExists(string gamma)
    {
        if (!Directory.Exists(gamma))
        {
            throw new DirectoryNotFoundException($"Directory {gamma} doesn't exist");
        }

        var gammaProfilesPath = Path.Join(gamma, "profiles");
        if (!Directory.Exists(gammaProfilesPath))
        {
            throw new DirectoryNotFoundException($"Directory {gammaProfilesPath} doesn't exist");
        }

        return gammaProfilesPath;
    }
}
