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

    [GeneratedRegex(@"selected_profile=@ByteArray\((?<profile>.+)\)")]
    private partial Regex SelectedProfileRx();
}

[JsonSerializable(typeof(CliProfile))]
public partial class CliProfileCtx : JsonSerializerContext;
