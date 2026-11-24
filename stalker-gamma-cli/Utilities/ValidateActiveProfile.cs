using stalker_gamma_cli.Models;

namespace stalker_gamma_cli.Utilities;

public static class ValidateActiveProfile
{
    /// <returns>The active profile</returns>
    /// <exception cref="CommandValidationException">There's no active profile</exception>
    public static CliProfile Validate(CliProfile? profile) =>
        profile
        ?? throw new CommandValidationException(
            "No active profile, create one with 'config create' or select one with 'config use'"
        );

    /// <summary>
    /// Fail if the profile's downloadThreads is out of range, e.g. from a hand-edited settings file.
    /// It's used as the download parallelism, which throws on 0 and is unlimited at -1.
    /// </summary>
    /// <exception cref="CommandValidationException">downloadThreads is out of range</exception>
    public static void ValidateDownloadThreads(CliProfile profile)
    {
        if (
            profile.DownloadThreads
            is >= CliProfile.MinDownloadThreads
                and <= CliProfile.MaxDownloadThreads
        )
        {
            return;
        }
        throw new CommandValidationException(
            $"downloadThreads in profile {profile.ProfileName} is {profile.DownloadThreads}, it must be from {CliProfile.MinDownloadThreads} to {CliProfile.MaxDownloadThreads}. Fix it with 'config set downloadThreads <threads>'"
        );
    }
}
