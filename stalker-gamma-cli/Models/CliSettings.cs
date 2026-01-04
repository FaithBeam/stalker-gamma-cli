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
        await File.WriteAllTextAsync(
            _settingsPath,
            JsonSerializer.Serialize<CliSettings>(
                this,
                jsonTypeInfo: CliSettingsCtx.Default.CliSettings
            )
        );
        return ActiveProfile?.ProfileName;
    }

    private readonly string _settingsPath = Path.Join(
        Path.GetDirectoryName(AppContext.BaseDirectory)!,
        "settings.json"
    );
}

[JsonSerializable(typeof(CliSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class CliSettingsCtx : JsonSerializerContext;
