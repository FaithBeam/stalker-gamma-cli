using System.ComponentModel.DataAnnotations;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Utilities;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("config")]
public class Config(ILogger logger, CliSettings cliSettings, UtilitiesReady utilitiesReady)
{
    /// <summary>
    /// Print the currently active profile settings.
    /// </summary>
    public void Info()
    {
        if (!utilitiesReady.IsReady)
        {
            _logger.Error(
                """
                Dependency not found:
                {Message}
                """,
                utilitiesReady.NotReadyReason
            );
            Environment.Exit(1);
        }
        var foundProfile = cliSettings.Profiles.FirstOrDefault(x => x.Active);
        if (foundProfile is null)
        {
            _logger.Error("No active profile found");
            return;
        }

        _logger.Information("{Profile}", foundProfile.ToString());
    }

    /// <summary>
    /// Edit a setting in the currently active profile.
    /// </summary>
    /// <param name="setting">The name of the setting to edit</param>
    /// <param name="value">The value of the setting to set</param>
    public async Task Set([Argument] string setting, [Argument] string value)
    {
        if (!utilitiesReady.IsReady)
        {
            _logger.Error(
                """
                Dependency not found:
                {Message}
                """,
                utilitiesReady.NotReadyReason
            );
            Environment.Exit(1);
        }
        var foundProfile = cliSettings.Profiles.FirstOrDefault(x => x.Active);
        if (foundProfile is null)
        {
            _logger.Error("No active profile found");
            Environment.Exit(1);
        }

        if (
            !foundProfile.TrySet(setting, value, out var error) && !string.IsNullOrWhiteSpace(error)
        )
        {
            _logger.Error("{Error}", error);
            Environment.Exit(1);
        }
        await cliSettings.SaveAsync();
        _logger.Information(
            "Profile {Profile} updated with {Setting}={Value}",
            foundProfile.ProfileName,
            setting,
            value
        );
    }

    /// <summary>
    /// Create settings file
    /// </summary>
    /// <param name="name">The name of the profile to create</param>
    /// <param name="anomaly">The path to anomaly install</param>
    /// <param name="gamma">The path to gamma install</param>
    /// <param name="cache">The path to put cache</param>
    /// <param name="mo2Profile">The ModOrganizer profile to operate on</param>
    /// <param name="modPackMakerUrl">The modpack_maker_list definition url</param>
    /// <param name="modListUrl">The modlist definition url</param>
    /// <param name="downloadThreads">The number of threads that can download an extract at the same time</param>
    /// <param name="gammaSetupRepoUrl">The gamma_setup repo url</param>
    /// <param name="gammaSetupRepoBranch">The gamma_setup repo branch or commit sha</param>
    /// <param name="stalkerGammaRepoUrl">The Stalker_GAMMA repo url</param>
    /// <param name="stalkerGammaRepoBranch">The Stalker_GAMMA repo branch or commit sha</param>
    /// <param name="gammaLargeFilesRepoUrl">The gamma_large_files repo url</param>
    /// <param name="gammaLargeFilesRepoBranch">The gamma_large_files repo branch or commit sha</param>
    /// <param name="teivazAnomalyGunslingerRepoUrl">The teivaz_anomaly_gunslinger repo url</param>
    /// <param name="teivazAnomalyGunslingerRepoBranch">The teivaz_anomaly_gunslinger repo branch or commit sha</param>
    public async Task Create(
        string anomaly,
        string gamma,
        string cache,
        string name = "gamma",
        string mo2Profile = "G.A.M.M.A",
        string modPackMakerUrl = "https://stalker-gamma.com/api/client/v1/mods/list",
        string modListUrl =
            "https://raw.githubusercontent.com/Grokitach/Stalker_GAMMA/refs/heads/main/G.A.M.M.A/modpack_data/modlist.txt",
        [Range(1, 20)] int downloadThreads = 2,
        string gammaSetupRepoUrl = "https://github.com/Grokitach/gamma_setup",
        string gammaSetupRepoBranch = "main",
        string stalkerGammaRepoUrl = "https://github.com/Grokitach/Stalker_GAMMA",
        string stalkerGammaRepoBranch = "main",
        string gammaLargeFilesRepoUrl = "https://github.com/Grokitach/gamma_large_files_v2",
        string gammaLargeFilesRepoBranch = "main",
        string teivazAnomalyGunslingerRepoUrl =
            "https://github.com/Grokitach/teivaz_anomaly_gunslinger",
        string teivazAnomalyGunslingerRepoBranch = "main"
    )
    {
        if (!utilitiesReady.IsReady)
        {
            _logger.Error(
                """
                Dependency not found:
                {Message}
                """,
                utilitiesReady.NotReadyReason
            );
            Environment.Exit(1);
        }

        foreach (var profile in cliSettings.Profiles)
        {
            profile.Active = false;
        }

        cache = Path.GetFullPath(cache);
        anomaly = Path.GetFullPath(anomaly);
        gamma = Path.GetFullPath(gamma);

        var foundProfile = cliSettings.Profiles.FirstOrDefault(x => x.ProfileName == name);
        if (foundProfile is null)
        {
            var newProfile = new CliProfile
            {
                ProfileName = name,
                DownloadThreads = downloadThreads,
                ModPackMakerUrl = modPackMakerUrl,
                ModListUrl = modListUrl,
                Cache = cache,
                Anomaly = anomaly,
                Gamma = gamma,
                Mo2Profile = mo2Profile,
                GammaSetupRepoUrl = gammaSetupRepoUrl,
                GammaSetupRepoBranch = gammaSetupRepoBranch,
                StalkerGammaRepoUrl = stalkerGammaRepoUrl,
                StalkerGammaRepoBranch = stalkerGammaRepoBranch,
                GammaLargeFilesRepoUrl = gammaLargeFilesRepoUrl,
                GammaLargeFilesRepoBranch = gammaLargeFilesRepoBranch,
                TeivazAnomalyGunslingerRepoUrl = teivazAnomalyGunslingerRepoUrl,
                TeivazAnomalyGunslingerRepoBranch = teivazAnomalyGunslingerRepoBranch,
            };
            await newProfile.SetActiveAsync();
            cliSettings.Profiles.Add(newProfile);
        }
        else
        {
            foundProfile.ProfileName = name;
            foundProfile.Anomaly = anomaly;
            foundProfile.Gamma = gamma;
            foundProfile.Cache = cache;
            foundProfile.Mo2Profile = mo2Profile;
            foundProfile.DownloadThreads = downloadThreads;
            foundProfile.ModPackMakerUrl = modPackMakerUrl;
            foundProfile.ModListUrl = modListUrl;
            foundProfile.GammaSetupRepoUrl = gammaSetupRepoUrl;
            foundProfile.GammaSetupRepoBranch = gammaSetupRepoBranch;
            foundProfile.StalkerGammaRepoUrl = stalkerGammaRepoUrl;
            foundProfile.StalkerGammaRepoBranch = stalkerGammaRepoBranch;
            foundProfile.GammaLargeFilesRepoUrl = gammaLargeFilesRepoUrl;
            foundProfile.GammaLargeFilesRepoBranch = gammaLargeFilesRepoBranch;
            foundProfile.TeivazAnomalyGunslingerRepoUrl = teivazAnomalyGunslingerRepoUrl;
            foundProfile.TeivazAnomalyGunslingerRepoBranch = teivazAnomalyGunslingerRepoBranch;
            await foundProfile.SetActiveAsync();
        }
        await cliSettings.SaveAsync();
        foreach (var profile in cliSettings.Profiles)
        {
            _logger.Information(
                "{Active}{Profile}",
                $"{(profile.Active ? "-> " : "")}",
                profile.ProfileName
            );
        }
    }

    /// <summary>
    /// List profiles
    /// </summary>
    public void List()
    {
        if (!utilitiesReady.IsReady)
        {
            _logger.Error(
                """
                Dependency not found:
                {Message}
                """,
                utilitiesReady.NotReadyReason
            );
            Environment.Exit(1);
        }

        foreach (var profile in cliSettings.Profiles)
        {
            _logger.Information(
                "{Active}{Profile}",
                $"{(profile.Active ? "-> " : "")}",
                profile.ProfileName
            );
        }
    }

    /// <summary>
    /// Get the currently active profile
    /// </summary>
    [Command("")]
    public void GetActive()
    {
        if (!utilitiesReady.IsReady)
        {
            _logger.Error(
                """
                Dependency not found:
                {Message}
                """,
                utilitiesReady.NotReadyReason
            );
            Environment.Exit(1);
        }

        var foundProfile = cliSettings.Profiles.FirstOrDefault(x => x.Active);
        if (foundProfile is null)
        {
            _logger.Error("No active profile found");
        }
        else
        {
            _logger.Information("{Profile}", foundProfile.ProfileName);
        }
    }

    /// <summary>
    /// Delete a profile. If this profile was active, you should set another to be active with config use
    /// </summary>
    /// <param name="name">Name of the profile to delete</param>
    public async Task Delete([Argument] string name)
    {
        if (!utilitiesReady.IsReady)
        {
            _logger.Error(
                """
                Dependency not found:
                {Message}
                """,
                utilitiesReady.NotReadyReason
            );
            Environment.Exit(1);
        }

        var foundProfile = cliSettings.Profiles.FirstOrDefault(x => x.ProfileName == name);
        if (foundProfile is null)
        {
            _logger.Error("Profile {Profile} not found", name);
        }
        else
        {
            cliSettings.Profiles.Remove(foundProfile);
            await cliSettings.SaveAsync();
            foreach (var profile in cliSettings.Profiles)
            {
                _logger.Information(
                    "{Active}{Profile}",
                    $"{(profile.Active ? "-> " : "")}",
                    profile.ProfileName
                );
            }
        }
    }

    /// <summary>
    /// Set a profile as active.
    /// </summary>
    /// <param name="name">The name of the profile to set as active</param>
    public async Task Use([Argument] string name)
    {
        if (!utilitiesReady.IsReady)
        {
            _logger.Error(
                """
                Dependency not found:
                {Message}
                """,
                utilitiesReady.NotReadyReason
            );
            Environment.Exit(1);
        }

        var foundProfile = cliSettings.Profiles.FirstOrDefault(x => x.ProfileName == name);
        if (foundProfile is null)
        {
            _logger.Error("{Profile} not found", name);
        }
        else
        {
            foreach (var profile in cliSettings.Profiles)
            {
                profile.Active = false;
            }

            await foundProfile.SetActiveAsync();

            await cliSettings.SaveAsync();

            foreach (var profile in cliSettings.Profiles)
            {
                _logger.Information(
                    "{Active}{Profile}",
                    $"{(profile.Active ? "-> " : "")}",
                    profile.ProfileName
                );
            }
        }
    }

    private readonly ILogger _logger = logger;
}
