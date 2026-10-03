using System.Text.Json;
using System.Text.Json.Serialization;
using Stalker.Gamma.Models;

namespace stalker_gamma_cli.Models;

public class CliSettings
{
    [JsonIgnore]
    public CliProfile? ActiveProfile => Profiles.FirstOrDefault(x => x.Active);

    public List<CliProfile> Profiles { get; set; } = [];

    public ExperimentalModDbSettings ExperimentalModDbSettings { get; set; } = new();

    public bool MigrateObsoleteModPackMakerUrls()
    {
        var changed = false;
        foreach (var profile in Profiles)
        {
            if (!ModPackMakerUrls.IsObsoleteOfficialListUrl(profile.ModPackMakerUrl))
            {
                continue;
            }

            profile.ModPackMakerUrl = ModPackMakerUrls.Default;
            changed = true;
        }

        return changed;
    }

    public async Task<string?> SaveAsync()
    {
        if (!Directory.Exists(_appDataPath))
        {
            Directory.CreateDirectory(_appDataPath);
        }
        await File.WriteAllTextAsync(
            SettingsPath,
            JsonSerializer.Serialize<CliSettings>(
                this,
                jsonTypeInfo: CliSettingsCtx.Default.CliSettings
            )
        );
        return ActiveProfile?.ProfileName;
    }

    private static string _appDataPath = Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "stalker-gamma"
    );

    public static string SettingsPath => Path.Join(_appDataPath, "settings.json");
}

[JsonSerializable(typeof(CliSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class CliSettingsCtx : JsonSerializerContext;
