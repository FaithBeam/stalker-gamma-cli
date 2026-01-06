using System.Text.RegularExpressions;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Utilities;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("mo2")]
public partial class Mo2Cmds(ILogger logger, CliSettings cliSettings, UtilitiesReady utilitiesReady)
{
    /// <summary>
    /// Retrieves the selected profile information from the ModOrganizer.ini file within the specified directory.
    /// </summary>
    [Command("config get selected-profile")]
    public async Task<int> GetProfile()
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
        
        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);

        var gamma = _cliSettings.ActiveProfile!.Gamma;
        var modOrganizerIniPath = Path.Join(gamma, "ModOrganizer.ini");
        if (!File.Exists(modOrganizerIniPath))
        {
            _logger.Error("ModOrganizer.ini not found");
            return 1;
        }

        var mo2Ini = await File.ReadAllTextAsync(modOrganizerIniPath);
        var match = SelectedProfileRx().Match(mo2Ini);
        if (!match.Success)
        {
            _logger.Error("Unable to find selected profile");
            return 1;
        }
        _logger.Information("{Profile}", match.Groups["profile"].Value);

        return 0;
    }

    /// <summary>
    /// Updates the selected profile in the ModOrganizer.ini file for the specified directory.
    /// </summary>
    /// <param name="profile">The name of the profile to be set as the selected profile.</param>
    [Command("config set selected-profile")]
    public async Task<int> SetProfile([Argument] string profile)
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
        
        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
        var gamma = _cliSettings.ActiveProfile!.Gamma;
        var modOrganizerIniPath = Path.Join(gamma, "ModOrganizer.ini");
        if (!File.Exists(modOrganizerIniPath))
        {
            _logger.Error("ModOrganizer.ini not found");
            return 1;
        }

        var profilePath = ProfileUtility.ValidateProfileExists(gamma);
        var profiles = new DirectoryInfo(profilePath).GetDirectories().Select(x => x.Name).ToList();
        if (!profiles.Contains(profile))
        {
            _logger.Error("Profile {Profile} not found", profile);
            _logger.Information("Available profiles:\n{Profiles}", string.Join("\n", profiles));
            return 1;
        }

        var mo2Ini = await File.ReadAllTextAsync(modOrganizerIniPath);
        mo2Ini = SelectedProfileRx().Replace(mo2Ini, $"selected_profile=@ByteArray({profile})");
        await File.WriteAllTextAsync(modOrganizerIniPath, mo2Ini);
        _logger.Information("Selected profile set to {Profile}", profile);
        return 0;
    }

    /// <summary>
    /// Lists all profiles in a gamma installation.
    /// </summary>
    [Command("profiles list")]
    public void ListProfiles()
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
        
        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
        var gamma = _cliSettings.ActiveProfile!.Gamma;
        var gammaProfilesPath = ProfileUtility.ValidateProfileExists(gamma);
        var profiles = new DirectoryInfo(gammaProfilesPath).GetDirectories();
        foreach (var profile in profiles)
        {
            _logger.Information("{Profile}", profile.Name);
        }
    }

    /// <summary>
    /// Lists all mods in a profile.
    /// </summary>
    /// <param name="profile">Profile name</param>
    [Command("profile list mods")]
    public async Task ListMods([Argument] string profile)
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
        
        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
        var gamma = _cliSettings.ActiveProfile!.Gamma;
        var gammaProfilesPath = ProfileUtility.ValidateProfileExists(gamma);
        var modListPath = Path.Join(gammaProfilesPath, profile, "modlist.txt");
        var modList = await ModListUtility.GetModListAsync(modListPath);
        foreach (var mod in modList)
        {
            _logger.Information("{Active} | {Mod}", mod.Status.ToString().PadRight(13), mod.Name);
        }
    }

    /// <summary>
    /// Deletes a specified profile from a Gamma installation.
    /// </summary>
    /// <param name="profile">Name of the profile to be deleted.</param>
    [Command("profile delete")]
    public void DeleteProfile([Argument] string profile)
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
        
        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
        var gamma = _cliSettings.ActiveProfile!.Gamma;
        var gammaProfilesPath = Path.Join(gamma, "profiles");
        if (!Directory.Exists(gammaProfilesPath))
        {
            throw new DirectoryNotFoundException($"Directory {gammaProfilesPath} doesn't exist");
        }
        var profilePath = Path.Join(gammaProfilesPath, profile);
        if (!Directory.Exists(profilePath))
        {
            throw new DirectoryNotFoundException($"Directory {profilePath} doesn't exist");
        }
        DirUtils.NormalizePermissions(profilePath);
        Directory.Delete(profilePath, true);
        _logger.Information("Profile {Profile} deleted", profile);
    }

    /// <summary>
    /// Show mod status info
    /// </summary>
    /// <param name="mod">Mod name</param>
    [Command("mod status")]
    public async Task Status([Argument] string mod)
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
        _logger.Information(
            "{Status} | {Name}",
            foundMod.Status.ToString().PadRight(13),
            foundMod.Name
        );
    }

    /// <summary>
    /// Enables a specified mod within a given profile.
    /// </summary>
    /// <param name="mod">The name of the mod to enable.</param>
    [Command("mod enable")]
    public async Task Enable([Argument] string mod)
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

        foundMod.Status = ModStatus.Enabled;
        await ModListUtility.SaveModListAsync(modListPath, modList);
        _logger.Information(
            "{Status} | {Name}",
            foundMod.Status.ToString().PadRight(13),
            foundMod.Name
        );
    }

    /// <summary>
    /// Disables a specified mod in the given profile
    /// </summary>
    /// <param name="mod">The name of the mod to be disabled.</param>
    [Command("mod disable")]
    public async Task Disable([Argument] string mod)
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

        foundMod.Status = ModStatus.Disabled;
        await ModListUtility.SaveModListAsync(modListPath, modList);
        _logger.Information(
            "{Status} | {Name}",
            foundMod.Status.ToString().PadRight(13),
            foundMod.Name
        );
    }

    /// <summary>
    /// Deletes a specified mod in the provided profile.
    /// </summary>
    /// <param name="mod">The name of the mod to delete.</param>
    [Command("mod delete")]
    public async Task Delete([Argument] string mod)
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
        modList.Remove(foundMod);
        await ModListUtility.SaveModListAsync(modListPath, modList);
        _logger.Information("Mod {Mod} deleted from profile {Profile}", mod, profile);
    }

    private readonly ILogger _logger = logger;
    private readonly CliSettings _cliSettings = cliSettings;

    [GeneratedRegex(@"selected_profile=@ByteArray\((?<profile>.+)\)")]
    private partial Regex SelectedProfileRx();
}
