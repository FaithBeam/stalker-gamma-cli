using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stalker.Gamma.Models;

public class ModPackMakerRecord : IEquatable<ModPackMakerRecord>
{
    [JsonIgnore]
    public int Counter { get; set; }
    public required string DlLink { get; set; }

    [JsonConverter(typeof(ModPackMakerInstructionConverter))]
    public string? Instructions { get; set; }

    [JsonConverter(typeof(ModPackMakerPatchConverter))]
    public string? Patch { get; set; }
    public string? AddonName { get; set; }
    public string? ModDbUrl { get; set; }
    public string? ZipName { get; set; }
    public string? Md5ModDb { get; set; }

    public bool Equals(ModPackMakerRecord? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return DlLink == other.DlLink
            && Instructions == other.Instructions
            && Patch == other.Patch
            && AddonName == other.AddonName
            && ModDbUrl == other.ModDbUrl
            && ZipName == other.ZipName
            && Md5ModDb == other.Md5ModDb;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((ModPackMakerRecord)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            DlLink,
            Instructions,
            Patch,
            AddonName,
            ModDbUrl,
            ZipName,
            Md5ModDb
        );
    }
}

[JsonSerializable(typeof(ModPackMakerRecord))]
[JsonSerializable(typeof(List<ModPackMakerRecord>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
)]
public partial class ModPackMakerCtx : JsonSerializerContext;

public class ModPackMakerInstructionConverter : JsonConverter<string>
{
    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected StartArray token.");
        }

        List<string> instructions = [];

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return instructions.Count != 0 ? string.Join(':', instructions) : "0";
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                instructions.Add(reader.GetString() ?? throw new JsonException("Expected string"));
            }
            else
            {
                throw new JsonException("Expected string token.");
            }
        }
        throw new JsonException("Error reading instructions.");
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        if (value == "0")
        {
            writer.WriteEndArray();
            return;
        }
        var split = value.Split(':');
        foreach (var s in split)
        {
            writer.WriteStringValue(s);
        }
        writer.WriteEndArray();
    }
}

public class ModPackMakerPatchConverter : JsonConverter<string>
{
    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        return $"- {reader.GetString()}" ?? throw new JsonException("Expected string");
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.TrimStart("- "));
    }
}
