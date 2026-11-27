using System.Text.Json;
using System.Text.Json.Serialization;
using stalker_gamma_cli.Utilities;

namespace stalker_gamma_cli.Models;

public class CliSettings
{
    [JsonIgnore]
    public CliProfile? ActiveProfile => Profiles.FirstOrDefault(x => x.Active);

    public List<CliProfile> Profiles { get; set; } = [];

    public async Task<string?> SaveAsync()
    {
        if (!Directory.Exists(AppDataPathUtility.AppDataPath))
        {
            Directory.CreateDirectory(AppDataPathUtility.AppDataPath);
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

    public static string SettingsPath => Path.Join(AppDataPathUtility.AppDataPath, "settings.json");

    /// <summary>
    /// Reads the settings file, or returns empty settings when there isn't one
    /// </summary>
    /// <exception cref="InvalidSettingsFileException">The file isn't valid settings</exception>
    public static CliSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new CliSettings();
        }

        try
        {
            return JsonSerializer.Deserialize(
                    File.ReadAllText(SettingsPath),
                    jsonTypeInfo: CliSettingsCtx.Default.CliSettings
                ) ?? throw new JsonException("It contains null instead of settings.");
        }
        catch (JsonException e)
        {
            throw new InvalidSettingsFileException(
                $"The settings file {SettingsPath} is invalid: {e.Message} Fix it, or delete it and create your profile again with `stalker-gamma config create`.",
                e
            );
        }
    }
}

public class InvalidSettingsFileException(string message, Exception innerException)
    : Exception(message, innerException);

[JsonSerializable(typeof(CliSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class CliSettingsCtx : JsonSerializerContext;
