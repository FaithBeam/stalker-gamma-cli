using Serilog;
using stalker_gamma_cli.Models;

namespace stalker_gamma_cli.Utilities;

public static class ValidateActiveProfile
{
    public static void Validate(ILogger logger, CliProfile? profile)
    {
        if (profile is not null)
        {
            return;
        }
        logger.Error(
            "No active profile, create one with 'config create' or select one with 'config use'"
        );
        Environment.Exit(1);
    }

    /// <summary>
    /// Exit if the profile's downloadThreads is out of range, e.g. from a hand-edited settings file.
    /// It's used as the download parallelism, which throws on 0 and is unlimited at -1.
    /// </summary>
    public static void ValidateDownloadThreads(ILogger logger, CliProfile profile)
    {
        if (
            profile.DownloadThreads
            is >= CliProfile.MinDownloadThreads
                and <= CliProfile.MaxDownloadThreads
        )
        {
            return;
        }
        logger.Error(
            "downloadThreads in profile {Profile} is {DownloadThreads}, it must be from {Min} to {Max}. Fix it with 'config set downloadThreads <threads>'",
            profile.ProfileName,
            profile.DownloadThreads,
            CliProfile.MinDownloadThreads,
            CliProfile.MaxDownloadThreads
        );
        Environment.Exit(1);
    }
}
