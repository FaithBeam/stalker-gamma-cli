using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using stalker_gamma_cli.Utilities;

namespace stalker_gamma_cli.Models;

public partial class CliProfile
{
    public bool Active { get; set; }
    public string ProfileName { get; set; } = "Gamma";
    public string Anomaly { get; set; } = Path.Join("gamma", "anomaly");
    public string Gamma { get; set; } = Path.Join("gamma", "gamma");
    public string Cache { get; set; } = Path.Join("gamma", "cache");
    public string Mo2Profile { get; set; } = "G.A.M.M.A";
    public int DownloadThreads { get; set; } = 2;
    public string ModPackMakerUrl { get; set; } = "https://stalker-gamma.com/api/list";
    public string ModListUrl { get; set; } =
        "https://raw.githubusercontent.com/Grokitach/Stalker_GAMMA/refs/heads/main/G.A.M.M.A/modpack_data/modlist.txt";
    public string GammaSetupRepoUrl { get; set; } = "https://github.com/Grokitach/gamma_setup";
    public string GammaSetupRepoBranch { get; set; } = "main";
    public string StalkerGammaRepoUrl { get; set; } = "https://github.com/Grokitach/Stalker_GAMMA";
    public string StalkerGammaRepoBranch { get; set; } = "main";
    public string GammaLargeFilesRepoUrl { get; set; } =
        "https://github.com/Grokitach/gamma_large_files_v2";
    public string GammaLargeFilesRepoBranch { get; set; } = "main";
    public string TeivazAnomalyGunslingerRepoUrl { get; set; } =
        "https://github.com/Grokitach/teivaz_anomaly_gunslinger";
    public string TeivazAnomalyGunslingerRepoBranch { get; set; } = "main";

    public override string ToString() =>
        $"""
            ProfileName: {ProfileName}
            Anomaly: {Anomaly}
            Gamma: {Gamma}
            Cache: {Cache}
            Mo2Profile: {Mo2Profile}
            DownloadThreads: {DownloadThreads}
            ModPackMakerUrl: {ModPackMakerUrl}
            GammaSetupRepoUrl: {GammaSetupRepoUrl}
            GammaSetupRepoBranch: {GammaSetupRepoBranch}
            StalkerGammaRepoUrl: {StalkerGammaRepoUrl}
            StalkerGammaRepoBranch: {StalkerGammaRepoBranch}
            GammaLargeFilesRepoUrl: {GammaLargeFilesRepoUrl}
            GammaLargeFilesRepoBranch: {GammaLargeFilesRepoBranch}
            TeivazAnomalyGunslingerRepoUrl: {TeivazAnomalyGunslingerRepoUrl}
            TeivazAnomalyGunslingerRepoBranch: {TeivazAnomalyGunslingerRepoBranch}
            ModListUrl: {ModListUrl}
            Active: {Active}
            """;

    public async Task SetActiveAsync()
    {
        Active = true;
        var modOrganizerIniPath = Path.Join(Gamma, "ModOrganizer.ini");
        if (File.Exists(modOrganizerIniPath))
        {
            var profilePath = ProfileUtility.ValidateProfileExists(Gamma);
            var mo2ProfilePath = Path.Join(profilePath, Mo2Profile);
            if (!Directory.Exists(mo2ProfilePath))
            {
                Directory.CreateDirectory(mo2ProfilePath);
                var mo2ProfileModListPath = Path.Join(mo2ProfilePath, "modlist.txt");
                await File.WriteAllTextAsync(
                    mo2ProfileModListPath,
                    await new HttpClient().GetStringAsync(ModListUrl)
                );
            }
            var profiles = new DirectoryInfo(profilePath)
                .GetDirectories()
                .Select(x => x.Name)
                .ToList();
            if (profiles.Contains(Mo2Profile))
            {
                var mo2Ini = await File.ReadAllTextAsync(modOrganizerIniPath);
                mo2Ini = SelectedProfileRx()
                    .Replace(mo2Ini, $"selected_profile=@ByteArray({Mo2Profile})");
                await File.WriteAllTextAsync(modOrganizerIniPath, mo2Ini);
            }
        }
    }

    public bool TrySet(string setting, string value, out string? error)
    {
        switch (setting.ToLowerInvariant())
        {
            case "profilename":
                ProfileName = value;
                error = null;
                return true;

            case "anomaly":
                Anomaly = value;
                error = null;
                return true;

            case "gamma":
                Gamma = value;
                error = null;
                return true;

            case "cache":
                Cache = value;
                error = null;
                return true;

            case "mo2profile":
                Mo2Profile = value;
                error = null;
                return true;

            case "modpackmakerurl":
                ModPackMakerUrl = value;
                error = null;
                return true;

            case "modlisturl":
                ModListUrl = value;
                error = null;
                return true;

            case "gammasetuprepourl":
                GammaSetupRepoUrl = value;
                error = null;
                return true;

            case "gammasetuprepobranch":
                GammaSetupRepoBranch = value;
                error = null;
                return true;

            case "stalkergammarepourl":
                StalkerGammaRepoUrl = value;
                error = null;
                return true;

            case "stalkergammarepobranch":
                StalkerGammaRepoBranch = value;
                error = null;
                return true;

            case "gammalargefilesrepourl":
                GammaLargeFilesRepoUrl = value;
                error = null;
                return true;

            case "gammalargefilesrepobranch":
                GammaLargeFilesRepoBranch = value;
                error = null;
                return true;

            case "teivazanomalygunslingerrepourl":
                TeivazAnomalyGunslingerRepoUrl = value;
                error = null;
                return true;

            case "teivazanomalygunslingerrepobranch":
                TeivazAnomalyGunslingerRepoBranch = value;
                error = null;
                return true;

            case "downloadthreads":
                if (!int.TryParse(value, out var threads))
                {
                    error = "Value must be an integer.";
                    return false;
                }

                DownloadThreads = threads;
                error = null;
                return true;

            default:
                error = $"Unknown setting '{setting}'.";
                return false;
        }
    }

    [GeneratedRegex(@"selected_profile=@ByteArray\((?<profile>.+)\)")]
    private partial Regex SelectedProfileRx();
}

[JsonSerializable(typeof(CliProfile))]
public partial class CliProfileCtx : JsonSerializerContext;
