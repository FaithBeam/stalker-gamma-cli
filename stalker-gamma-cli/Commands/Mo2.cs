using System.Text.RegularExpressions;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Utilities;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("mo2")]
public partial class Mo2Cmds(ILogger logger, CliSettings cliSettings)
{
    /// <summary>
    /// Retrieves the selected profile information from the ModOrganizer.ini file within the specified directory.
    /// </summary>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    [Command("config get selected-profile")]
    public Task<int> GetProfile(bool json = false) =>
        Run(
            "mo2 config get selected-profile",
            json,
            async () =>
            {
                ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);

                var gamma = _cliSettings.ActiveProfile!.Gamma;
                var modOrganizerIniPath = Path.Join(gamma, "ModOrganizer.ini");
                if (!File.Exists(modOrganizerIniPath))
                {
                    return LogError("ModOrganizer.ini not found");
                }

                var mo2Ini = await File.ReadAllTextAsync(modOrganizerIniPath);
                var match = SelectedProfileRx().Match(mo2Ini);
                if (!match.Success)
                {
                    return LogError("Unable to find selected profile");
                }

                var profile = match.Groups["profile"].Value;
                if (json)
                {
                    JsonOutputUtility.WriteMo2SelectedProfile(profile);
                }
                else
                {
                    _logger.Information("{Profile}", profile);
                }
                return null;
            }
        );

    /// <summary>
    /// Updates the selected profile in the ModOrganizer.ini file for the specified directory.
    /// </summary>
    /// <param name="profile">The name of the profile to be set as the selected profile.</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    [Command("config set selected-profile")]
    public Task<int> SetProfile([Argument] string profile, bool json = false) =>
        Run(
            "mo2 config set selected-profile",
            json,
            async () =>
            {
                ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
                var gamma = _cliSettings.ActiveProfile!.Gamma;
                var modOrganizerIniPath = Path.Join(gamma, "ModOrganizer.ini");
                if (!File.Exists(modOrganizerIniPath))
                {
                    return LogError("ModOrganizer.ini not found");
                }

                var profilePath = ProfileUtility.ValidateProfileExists(gamma);
                var profiles = new DirectoryInfo(profilePath)
                    .GetDirectories()
                    .Select(x => x.Name)
                    .ToList();
                if (!profiles.Contains(profile))
                {
                    var error = LogError($"Profile {profile} not found");
                    if (json)
                    {
                        JsonOutputUtility.WriteMo2Profiles(profiles);
                    }
                    else
                    {
                        _logger.Information(
                            "Available profiles:\n{Profiles}",
                            string.Join("\n", profiles)
                        );
                    }
                    return error;
                }

                var mo2Ini = await File.ReadAllTextAsync(modOrganizerIniPath);
                mo2Ini = SelectedProfileRx()
                    .Replace(mo2Ini, $"selected_profile=@ByteArray({profile})");
                await File.WriteAllTextAsync(modOrganizerIniPath, mo2Ini);
                _logger.Information("Selected profile set to {Profile}", profile);
                if (json)
                {
                    JsonOutputUtility.WriteMo2SelectedProfile(profile);
                }
                return null;
            }
        );

    /// <summary>
    /// Lists all profiles in a gamma installation.
    /// </summary>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    [Command("profiles list")]
    public Task<int> ListProfiles(bool json = false) =>
        Run(
            "mo2 profiles list",
            json,
            () =>
            {
                ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
                var gamma = _cliSettings.ActiveProfile!.Gamma;
                var gammaProfilesPath = ProfileUtility.ValidateProfileExists(gamma);
                var profiles = new DirectoryInfo(gammaProfilesPath)
                    .GetDirectories()
                    .Select(x => x.Name)
                    .ToList();
                if (json)
                {
                    JsonOutputUtility.WriteMo2Profiles(profiles);
                }
                else
                {
                    foreach (var profile in profiles)
                    {
                        _logger.Information("{Profile}", profile);
                    }
                }
                return Task.FromResult<string?>(null);
            }
        );

    /// <summary>
    /// Lists all mods in a profile.
    /// </summary>
    /// <param name="profile">Profile name</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    [Command("profile list mods")]
    public Task<int> ListMods([Argument] string profile, bool json = false) =>
        Run(
            "mo2 profile list mods",
            json,
            async () =>
            {
                ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
                var gamma = _cliSettings.ActiveProfile!.Gamma;
                var gammaProfilesPath = ProfileUtility.ValidateProfileExists(gamma);
                var modListPath = Path.Join(gammaProfilesPath, profile, "modlist.txt");
                var modList = await ModListUtility.GetModListAsync(modListPath);
                if (json)
                {
                    JsonOutputUtility.WriteMods(profile, modList);
                }
                else
                {
                    foreach (var mod in modList)
                    {
                        _logger.Information(
                            "{Active} | {Mod}",
                            mod.Status.ToString().PadRight(13),
                            mod.Name
                        );
                    }
                }
                return null;
            }
        );

    /// <summary>
    /// Deletes a specified profile from a Gamma installation.
    /// </summary>
    /// <param name="profile">Name of the profile to be deleted.</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    [Command("profile delete")]
    public Task<int> DeleteProfile([Argument] string profile, bool json = false) =>
        Run(
            "mo2 profile delete",
            json,
            () =>
            {
                ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
                var gamma = _cliSettings.ActiveProfile!.Gamma;
                var gammaProfilesPath = Path.Join(gamma, "profiles");
                if (!Directory.Exists(gammaProfilesPath))
                {
                    throw new DirectoryNotFoundException(
                        $"Directory {gammaProfilesPath} doesn't exist"
                    );
                }
                var profilePath = Path.Join(gammaProfilesPath, profile);
                if (!Directory.Exists(profilePath))
                {
                    throw new DirectoryNotFoundException($"Directory {profilePath} doesn't exist");
                }
                DirUtils.NormalizePermissions(profilePath);
                Directory.Delete(profilePath, true);
                _logger.Information("Profile {Profile} deleted", profile);
                return Task.FromResult<string?>(null);
            }
        );

    /// <summary>
    /// Show mod status info
    /// </summary>
    /// <param name="mod">Mod name</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    [Command("mod status")]
    public Task<int> Status([Argument] string mod, bool json = false) =>
        Run(
            "mo2 mod status",
            json,
            async () =>
            {
                var (profile, _, _, foundMod) = await FindModAsync(mod);
                WriteMod(json, profile, foundMod);
                return null;
            }
        );

    /// <summary>
    /// Enables a specified mod within a given profile.
    /// </summary>
    /// <param name="mod">The name of the mod to enable.</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    [Command("mod enable")]
    public Task<int> Enable([Argument] string mod, bool json = false) =>
        Run(
            "mo2 mod enable",
            json,
            async () =>
            {
                var (profile, modListPath, modList, foundMod) = await FindModAsync(mod);
                foundMod.Status = ModStatus.Enabled;
                await ModListUtility.SaveModListAsync(modListPath, modList);
                WriteMod(json, profile, foundMod);
                return null;
            }
        );

    /// <summary>
    /// Disables a specified mod in the given profile
    /// </summary>
    /// <param name="mod">The name of the mod to be disabled.</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    [Command("mod disable")]
    public Task<int> Disable([Argument] string mod, bool json = false) =>
        Run(
            "mo2 mod disable",
            json,
            async () =>
            {
                var (profile, modListPath, modList, foundMod) = await FindModAsync(mod);
                foundMod.Status = ModStatus.Disabled;
                await ModListUtility.SaveModListAsync(modListPath, modList);
                WriteMod(json, profile, foundMod);
                return null;
            }
        );

    /// <summary>
    /// Deletes a specified mod in the provided profile.
    /// </summary>
    /// <param name="mod">The name of the mod to delete.</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    [Command("mod delete")]
    public Task<int> Delete([Argument] string mod, bool json = false) =>
        Run(
            "mo2 mod delete",
            json,
            async () =>
            {
                var (profile, modListPath, modList, foundMod) = await FindModAsync(mod);
                modList.Remove(foundMod);
                await ModListUtility.SaveModListAsync(modListPath, modList);
                _logger.Information("Mod {Mod} deleted from profile {Profile}", mod, profile);
                return null;
            }
        );

    /// <summary>
    /// Finds a mod in the active MO2 profile's modlist.txt.
    /// </summary>
    private async Task<(
        string Profile,
        string ModListPath,
        List<ModListRecord> ModList,
        ModListRecord Mod
    )> FindModAsync(string mod)
    {
        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
        var gamma = _cliSettings.ActiveProfile!.Gamma;
        var profile = _cliSettings.ActiveProfile!.Mo2Profile;
        var gammaProfilesPath = ProfileUtility.ValidateProfileExists(gamma);
        var modListPath = Path.Join(gammaProfilesPath, profile, "modlist.txt");
        var modList = await ModListUtility.GetModListAsync(modListPath);
        var foundMod = modList.FirstOrDefault(x => x.Name == mod);
        if (foundMod is null)
        {
            throw new FileNotFoundException($"Mod {mod} not found in profile {profile}");
        }
        return (profile, modListPath, modList, foundMod);
    }

    private void WriteMod(bool json, string profile, ModListRecord mod)
    {
        if (json)
        {
            JsonOutputUtility.WriteMod(profile, mod);
        }
        else
        {
            _logger.Information("{Status} | {Name}", mod.Status.ToString().PadRight(13), mod.Name);
        }
    }

    private Task<int> Run(string command, bool json, Func<Task<string?>> action) =>
        JsonOutputUtility.RunAsync(_logger, command, json, action);

    private string LogError(string error) => JsonOutputUtility.LogError(_logger, error);

    private readonly ILogger _logger = logger;
    private readonly CliSettings _cliSettings = cliSettings;

    [GeneratedRegex(@"selected_profile=@ByteArray\((?<profile>.+)\)")]
    private partial Regex SelectedProfileRx();
}
