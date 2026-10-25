using System.ComponentModel.DataAnnotations;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Utilities;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("config")]
public class Config(ILogger logger, CliSettings cliSettings)
{
    /// <summary>
    /// Print the currently active profile settings.
    /// </summary>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    public Task<int> Info(bool json = false) =>
        Run(
            "config info",
            json,
            () =>
            {
                var foundProfile = cliSettings.Profiles.FirstOrDefault(x => x.Active);
                if (foundProfile is null)
                {
                    return Task.FromResult<string?>(LogError("No active profile found"));
                }

                if (json)
                {
                    JsonOutputUtility.WriteProfile(CliSettings.SettingsPath, foundProfile);
                }
                else
                {
                    _logger.Information("Settings Path: {Path}", CliSettings.SettingsPath);
                    _logger.Information("{Profile}", foundProfile.ToString());
                }
                return Task.FromResult<string?>(null);
            }
        );

    /// <summary>
    /// Edit a setting in the currently active profile.
    /// </summary>
    /// <param name="setting">The name of the setting to edit</param>
    /// <param name="value">The value of the setting to set</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    public Task<int> Set([Argument] string setting, [Argument] string value, bool json = false) =>
        Run(
            "config set",
            json,
            async () =>
            {
                var foundProfile = cliSettings.Profiles.FirstOrDefault(x => x.Active);
                if (foundProfile is null)
                {
                    return LogError("No active profile found");
                }

                if (!foundProfile.TrySet(setting, value, out var error))
                {
                    return LogError(error!);
                }

                await cliSettings.SaveAsync();
                _logger.Information(
                    "Profile {Profile} updated with {Setting}={Value}",
                    foundProfile.ProfileName,
                    setting,
                    value
                );
                if (json)
                {
                    JsonOutputUtility.WriteProfile(CliSettings.SettingsPath, foundProfile);
                }
                return null;
            }
        );

    /// <summary>
    /// Create the settings file
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
    /// <param name="pythonApiUrl">Url to the host python api</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    public Task<int> Create(
        string anomaly,
        string gamma,
        string cache,
        string name = "gamma",
        string mo2Profile = "G.A.M.M.A",
        string modPackMakerUrl = "https://stalker-gamma.com/api/client/v1/mods/list",
        string modListUrl =
            "https://raw.githubusercontent.com/Grokitach/Stalker_GAMMA/refs/heads/main/G.A.M.M.A/modpack_data/modlist.txt",
        [Range(1, 20)] int downloadThreads = 4,
        string gammaSetupRepoUrl = "https://github.com/Grokitach/gamma_setup",
        string gammaSetupRepoBranch = "main",
        string stalkerGammaRepoUrl = "https://github.com/Grokitach/Stalker_GAMMA",
        string stalkerGammaRepoBranch = "main",
        string gammaLargeFilesRepoUrl = "https://github.com/Grokitach/gamma_large_files_v2",
        string gammaLargeFilesRepoBranch = "main",
        string teivazAnomalyGunslingerRepoUrl =
            "https://github.com/Grokitach/teivaz_anomaly_gunslinger",
        string teivazAnomalyGunslingerRepoBranch = "main",
        string pythonApiUrl = "http://localhost:8000",
        bool json = false
    ) =>
        Run(
            "config create",
            json,
            async () =>
            {
                await CreateProfile(
                    anomaly,
                    gamma,
                    cache,
                    name,
                    mo2Profile,
                    modPackMakerUrl,
                    modListUrl,
                    downloadThreads,
                    gammaSetupRepoUrl,
                    gammaSetupRepoBranch,
                    stalkerGammaRepoUrl,
                    stalkerGammaRepoBranch,
                    gammaLargeFilesRepoUrl,
                    gammaLargeFilesRepoBranch,
                    teivazAnomalyGunslingerRepoUrl,
                    teivazAnomalyGunslingerRepoBranch,
                    pythonApiUrl
                );
                WriteProfiles(json);
                return null;
            }
        );

    private async Task CreateProfile(
        string anomaly,
        string gamma,
        string cache,
        string name,
        string mo2Profile,
        string modPackMakerUrl,
        string modListUrl,
        int downloadThreads,
        string gammaSetupRepoUrl,
        string gammaSetupRepoBranch,
        string stalkerGammaRepoUrl,
        string stalkerGammaRepoBranch,
        string gammaLargeFilesRepoUrl,
        string gammaLargeFilesRepoBranch,
        string teivazAnomalyGunslingerRepoUrl,
        string teivazAnomalyGunslingerRepoBranch,
        string pythonApiUrl
    )
    {
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
                PythonApiUrl = pythonApiUrl,
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
            foundProfile.PythonApiUrl = pythonApiUrl;
            await foundProfile.SetActiveAsync();
        }
        await cliSettings.SaveAsync();
    }

    /// <summary>
    /// List profiles
    /// </summary>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    public Task<int> List(bool json = false) =>
        Run(
            "config list",
            json,
            () =>
            {
                WriteProfiles(json);
                return Task.FromResult<string?>(null);
            }
        );

    /// <summary>
    /// Get the currently active profile
    /// </summary>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    [Command("")]
    public Task<int> GetActive(bool json = false) =>
        Run(
            "config",
            json,
            () =>
            {
                var foundProfile = cliSettings.Profiles.FirstOrDefault(x => x.Active);
                if (foundProfile is null)
                {
                    return Task.FromResult<string?>(LogError("No active profile found"));
                }

                if (json)
                {
                    JsonOutputUtility.WriteProfile(CliSettings.SettingsPath, foundProfile);
                }
                else
                {
                    _logger.Information("{Profile}", foundProfile.ProfileName);
                }
                return Task.FromResult<string?>(null);
            }
        );

    /// <summary>
    /// Delete a profile. If this profile was active, you should set another to be active with config use
    /// </summary>
    /// <param name="name">Name of the profile to delete</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    public Task<int> Delete([Argument] string name, bool json = false) =>
        Run(
            "config delete",
            json,
            async () =>
            {
                var foundProfile = cliSettings.Profiles.FirstOrDefault(x => x.ProfileName == name);
                if (foundProfile is null)
                {
                    return LogError($"Profile {name} not found");
                }

                cliSettings.Profiles.Remove(foundProfile);
                await cliSettings.SaveAsync();
                WriteProfiles(json);
                return null;
            }
        );

    /// <summary>
    /// Set a profile as active.
    /// </summary>
    /// <param name="name">The name of the profile to set as active</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    public Task<int> Use([Argument] string name, bool json = false) =>
        Run(
            "config use",
            json,
            async () =>
            {
                var foundProfile = cliSettings.Profiles.FirstOrDefault(x => x.ProfileName == name);
                if (foundProfile is null)
                {
                    return LogError($"{name} not found");
                }

                foreach (var profile in cliSettings.Profiles)
                {
                    profile.Active = false;
                }

                await foundProfile.SetActiveAsync();

                await cliSettings.SaveAsync();

                WriteProfiles(json);
                return null;
            }
        );

    private Task<int> Run(string command, bool json, Func<Task<string?>> action) =>
        JsonOutputUtility.RunAsync(_logger, command, json, action);

    private string LogError(string error) => JsonOutputUtility.LogError(_logger, error);

    private void WriteProfiles(bool json)
    {
        if (json)
        {
            JsonOutputUtility.WriteProfiles(cliSettings.Profiles);
            return;
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

    private readonly ILogger _logger = logger;
}
