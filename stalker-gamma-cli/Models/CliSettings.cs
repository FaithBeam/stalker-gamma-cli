using System.Text.Json;
using System.Text.Json.Serialization;

namespace stalker_gamma_cli.Models;

public class CliSettings
{
    [JsonIgnore]
    public CliProfile? ActiveProfile => Profiles.FirstOrDefault(x => x.Active);

    public List<CliProfile> Profiles { get; set; } = [];

    public async Task<string?> SaveAsync()
    {
        if (!Directory.Exists(_appDataPath))
        {
            Directory.CreateDirectory(_appDataPath);
        }
        // write to a temp file first so the settings file isn't left truncated if the process is
        // killed mid-write, which would make every command fail to start
        var tmpPath = $"{SettingsPath}.tmp";
        await File.WriteAllTextAsync(
            tmpPath,
            JsonSerializer.Serialize<CliSettings>(
                this,
                jsonTypeInfo: CliSettingsCtx.Default.CliSettings
            )
        );
        File.Move(tmpPath, SettingsPath, overwrite: true);
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
