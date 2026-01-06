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
    /// Create settings file
    /// </summary>
    /// <param name="name">The name of the profile to create</param>
    /// <param name="anomaly">The path to anomaly install</param>
    /// <param name="gamma">The path to gamma install</param>
    /// <param name="cache">The path to put cache</param>
    /// <param name="mo2Profile">The ModOrganizer profile to operate on</param>
    /// <param name="modPackMakerUrl">The modpack_maker_list definition url</param>
    /// <param name="modListUrl">The modlist definition url</param>
    /// <param name="downloadThreads"></param>
    public async Task Create(
        string anomaly,
        string gamma,
        string cache,
        string name = "gamma",
        string mo2Profile = "G.A.M.M.A",
        string modPackMakerUrl = "https://stalker-gamma.com/api/list",
        string modListUrl =
            "https://raw.githubusercontent.com/Grokitach/Stalker_GAMMA/refs/heads/main/G.A.M.M.A/modpack_data/modlist.txt",
        [Range(1, 6)] int downloadThreads = 2
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
