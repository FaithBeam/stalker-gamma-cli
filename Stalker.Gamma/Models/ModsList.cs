using System.Text.Json.Serialization;

namespace Stalker.Gamma.Models;

[JsonSerializable(typeof(ModsList))]
public partial class ModsListCtx : JsonSerializerContext;

public class ModsList
{
    [JsonPropertyName("separators")]
    public Separators[] Separators { get; set; } = [];

    [JsonPropertyName("modDb")]
    public ModDb[] ModDb { get; set; } = [];

    [JsonPropertyName("github")]
    public Github[] Github { get; set; } = [];

    [JsonPropertyName("gitRepos")]
    public GitRepos[] GitRepos { get; set; } = [];
}

public class Separators
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

public class ModDb
{
    [JsonPropertyName("lastUpdated")]
    public required string LastUpdated { get; set; }

    [JsonPropertyName("archiveName")]
    public string? ArchiveName { get; set; }

    [JsonPropertyName("downloadUrl")]
    public required string DownloadUrl { get; set; }

    [JsonPropertyName("niceUrl")]
    public string? NiceUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("md5")]
    public string? Md5 { get; set; }

    [JsonPropertyName("instructions")]
    public string[] Instructions { get; set; } = [];

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

public class Github
{
    [JsonPropertyName("archiveName")]
    public string? ArchiveName { get; set; }

    [JsonPropertyName("downloadUrl")]
    public required string DownloadUrl { get; set; }

    [JsonPropertyName("niceUrl")]
    public string? NiceUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("md5")]
    public string? Md5 { get; set; }

    [JsonPropertyName("instructions")]
    public string[] Instructions { get; set; } = [];

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

public class GitRepos
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("url")]
    public required string Url { get; set; }

    [JsonPropertyName("owner")]
    public required string Owner { get; set; }

    [JsonPropertyName("repository")]
    public required string Repository { get; set; }

    [JsonPropertyName("ref")]
    public required string Ref { get; set; }
}
