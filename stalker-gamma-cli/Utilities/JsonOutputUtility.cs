using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Parsing;
using stalker_gamma_cli.Models;
using Stalker.Gamma.Extensions;
using Stalker.Gamma.GammaInstallerServices;

namespace stalker_gamma_cli.Utilities;

/// <summary>
/// Writes newline-delimited JSON to stdout for commands run with --json so GUI wrappers can parse the output.
/// Every line is a single JSON object with a "type" of "log", "progress", "updates", "checksum", "prune", "profile", "profiles",
/// "mo2SelectedProfile", "mo2Profiles", "mods", "mod", "logsFolder",
/// "hashProgress", "hashArchive", or "result".
/// </summary>
public static class JsonOutputUtility
{
    public const string Flag = "--json";

    public static bool IsEnabled(string[] args) => args.Contains(Flag);

    /// <summary>
    /// Runs a command and writes a json result message when --json is passed.
    /// </summary>
    /// <param name="logger">Logger used to report exceptions</param>
    /// <param name="command">The command name reported in the result message</param>
    /// <param name="json">Whether --json was passed</param>
    /// <param name="action">The command logic. Returns an error message on failure, otherwise null.</param>
    public static async Task<int> RunAsync(
        ILogger logger,
        string command,
        bool json,
        Func<Task<string?>> action
    )
    {
        string? error;
        try
        {
            error = await action();
        }
        // Only catch in json mode so a GUI wrapper always gets a result message
        catch (Exception e) when (json)
        {
            logger.Error(e, "{Command} failed! {ExceptionMessage}", command, e.Message);
            error = e.Message;
        }

        var statusCode = error is null ? 0 : 1;
        if (json)
        {
            WriteResult(command, statusCode, error);
        }
        return statusCode;
    }

    /// <summary>
    /// Logs an error and returns it so command logic can pass it to <see cref="RunAsync"/>.
    /// </summary>
    public static string LogError(ILogger logger, string error)
    {
        logger.Error("{Error}", error);
        return error;
    }

    public static void WriteProgress(GammaProgress.GammaInstallProgressEventArgs e) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonProgressMessage
                {
                    Name = e.Name,
                    Operation = e.ProgressType.ToString(),
                    Progress = e.Progress,
                    BytesDownloaded = e.BytesDownloaded,
                    TotalBytes = e.TotalBytes,
                    Complete = e.Complete,
                    Total = e.Total,
                    Url = e.Url,
                    ArchiveName = e.ArchiveName,
                    DownloadPath = e.DownloadPath,
                    ExtractPath = e.ExtractPath,
                },
                JsonOutputCtx.Default.JsonProgressMessage
            )
        );

    public static void WriteUpdates(IList<ModPackMakerRecordDiff> diffs) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonUpdatesMessage
                {
                    Count = diffs.Count,
                    Updates = diffs
                        .Select(diff => new JsonUpdate
                        {
                            Status = diff.DiffType.ToString(),
                            AddonName =
                                diff.NewListRecord?.AddonName
                                ?? diff.OldListRecord?.AddonName
                                ?? diff.NewListRecord?.DlLink,
                            OldZipName = diff.OldListRecord?.ZipName,
                            NewZipName = diff.NewListRecord?.ZipName,
                        })
                        .ToList(),
                },
                JsonOutputCtx.Default.JsonUpdatesMessage
            )
        );

    public static void WriteChecksum(string path, ChecksumStatus status, int complete, int total) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonChecksumMessage
                {
                    Path = path,
                    Status = status.ToString(),
                    Complete = complete,
                    Total = total,
                },
                JsonOutputCtx.Default.JsonChecksumMessage
            )
        );

    public static void WritePrune(IList<FileInfo> archives) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonPruneMessage
                {
                    Count = archives.Count,
                    TotalBytes = archives.Sum(x => x.Length),
                    Archives = archives
                        .Select(x => new JsonPruneArchive
                        {
                            Path = x.FullName,
                            SizeBytes = x.Length,
                            LastWriteTime = x.LastWriteTime,
                        })
                        .ToList(),
                },
                JsonOutputCtx.Default.JsonPruneMessage
            )
        );

    public static void WriteProfile(string settingsPath, CliProfile profile) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonProfileMessage { SettingsPath = settingsPath, Profile = profile },
                JsonOutputCtx.Default.JsonProfileMessage
            )
        );

    public static void WriteProfiles(IList<CliProfile> profiles) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonProfilesMessage
                {
                    Profiles = profiles
                        .Select(x => new JsonProfileSummary
                        {
                            Name = x.ProfileName,
                            Active = x.Active,
                        })
                        .ToList(),
                },
                JsonOutputCtx.Default.JsonProfilesMessage
            )
        );

    public static void WriteMo2SelectedProfile(string profile) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonMo2SelectedProfileMessage { Profile = profile },
                JsonOutputCtx.Default.JsonMo2SelectedProfileMessage
            )
        );

    public static void WriteMo2Profiles(IList<string> profiles) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonMo2ProfilesMessage { Profiles = profiles.ToList() },
                JsonOutputCtx.Default.JsonMo2ProfilesMessage
            )
        );

    public static void WriteMods(string profile, IList<ModListRecord> mods) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonModsMessage
                {
                    Profile = profile,
                    Mods = mods.Select(x => new JsonMod
                        {
                            Name = x.Name,
                            Status = x.Status.ToString(),
                        })
                        .ToList(),
                },
                JsonOutputCtx.Default.JsonModsMessage
            )
        );

    public static void WriteMod(string profile, ModListRecord mod) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonModMessage
                {
                    Profile = profile,
                    Name = mod.Name,
                    Status = mod.Status.ToString(),
                },
                JsonOutputCtx.Default.JsonModMessage
            )
        );

    public static void WriteLogsFolder(string path) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonLogsFolderMessage { Path = path },
                JsonOutputCtx.Default.JsonLogsFolderMessage
            )
        );

    public static void WriteHashProgress(double progress) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonHashProgressMessage { Progress = progress },
                JsonOutputCtx.Default.JsonHashProgressMessage
            )
        );

    public static void WriteHashArchive(string path, string hashType) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonHashArchiveMessage { Path = path, HashType = hashType },
                JsonOutputCtx.Default.JsonHashArchiveMessage
            )
        );

    public static void WriteResult(string command, int exitCode, string? error) =>
        Console.Out.WriteLine(
            JsonSerializer.Serialize(
                new JsonResultMessage
                {
                    Command = command,
                    Success = exitCode == 0,
                    ExitCode = exitCode,
                    Error = error,
                },
                JsonOutputCtx.Default.JsonResultMessage
            )
        );
}

/// <summary>
/// Formats Serilog events as single-line JSON log messages.
/// </summary>
public class JsonLogFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output) =>
        output.WriteLine(
            JsonSerializer.Serialize(
                new JsonLogMessage
                {
                    Timestamp = logEvent.Timestamp,
                    Level = logEvent.Level.ToString(),
                    Message = RenderLiteral(logEvent),
                    Exception = logEvent.Exception?.ToString(),
                },
                JsonOutputCtx.Default.JsonLogMessage
            )
        );

    /// <summary>
    /// Render the message like the {Message:lj} output template so strings are not quoted.
    /// </summary>
    private static string RenderLiteral(LogEvent logEvent)
    {
        var writer = new StringWriter();
        foreach (var token in logEvent.MessageTemplate.Tokens)
        {
            if (
                token is PropertyToken pt
                && logEvent.Properties.TryGetValue(pt.PropertyName, out var value)
                && value is ScalarValue { Value: string s }
            )
            {
                writer.Write(s);
            }
            else
            {
                token.Render(logEvent.Properties, writer);
            }
        }
        return writer.ToString();
    }
}

public class JsonLogMessage
{
    public string Type => "log";
    public required DateTimeOffset Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
    public string? Exception { get; init; }
}

public class JsonProgressMessage
{
    public string Type => "progress";
    public required string Name { get; init; }
    public required string Operation { get; init; }

    /// <summary>
    /// null when the total size of a download is unknown; use BytesDownloaded instead.
    /// </summary>
    public required double? Progress { get; init; }
    public long? BytesDownloaded { get; init; }
    public long? TotalBytes { get; init; }
    public required int Complete { get; init; }
    public required int Total { get; init; }
    public required string Url { get; init; }
    public required string ArchiveName { get; init; }
    public required string DownloadPath { get; init; }
    public required string ExtractPath { get; init; }
}

public class JsonUpdatesMessage
{
    public string Type => "updates";
    public required int Count { get; init; }
    public required List<JsonUpdate> Updates { get; init; }
}

public class JsonUpdate
{
    public required string Status { get; init; }
    public string? AddonName { get; init; }
    public string? OldZipName { get; init; }
    public string? NewZipName { get; init; }
}

public enum ChecksumStatus
{
    Ok,
    Corrupt,
    NotFound,
}

public class JsonChecksumMessage
{
    public string Type => "checksum";
    public required string Path { get; init; }
    public required string Status { get; init; }
    public required int Complete { get; init; }
    public required int Total { get; init; }
}

public class JsonPruneMessage
{
    public string Type => "prune";
    public required int Count { get; init; }
    public required long TotalBytes { get; init; }
    public required List<JsonPruneArchive> Archives { get; init; }
}

public class JsonPruneArchive
{
    public required string Path { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTimeOffset LastWriteTime { get; init; }
}

public class JsonProfileMessage
{
    public string Type => "profile";
    public required string SettingsPath { get; init; }
    public required CliProfile Profile { get; init; }
}

public class JsonProfilesMessage
{
    public string Type => "profiles";
    public required List<JsonProfileSummary> Profiles { get; init; }
}

public class JsonProfileSummary
{
    public required string Name { get; init; }
    public required bool Active { get; init; }
}

public class JsonMo2SelectedProfileMessage
{
    public string Type => "mo2SelectedProfile";
    public required string Profile { get; init; }
}

public class JsonMo2ProfilesMessage
{
    public string Type => "mo2Profiles";
    public required List<string> Profiles { get; init; }
}

public class JsonModsMessage
{
    public string Type => "mods";
    public required string Profile { get; init; }
    public required List<JsonMod> Mods { get; init; }
}

public class JsonMod
{
    public required string Name { get; init; }
    public required string Status { get; init; }
}

public class JsonModMessage
{
    public string Type => "mod";
    public required string Profile { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
}

public class JsonLogsFolderMessage
{
    public string Type => "logsFolder";
    public required string Path { get; init; }
}

public class JsonHashProgressMessage
{
    public string Type => "hashProgress";
    public required double Progress { get; init; }
}

public class JsonHashArchiveMessage
{
    public string Type => "hashArchive";
    public required string Path { get; init; }
    public required string HashType { get; init; }
}

public class JsonResultMessage
{
    public string Type => "result";
    public required string Command { get; init; }
    public required bool Success { get; init; }
    public required int ExitCode { get; init; }
    public string? Error { get; init; }
}

[JsonSerializable(typeof(JsonLogMessage))]
[JsonSerializable(typeof(JsonProgressMessage))]
[JsonSerializable(typeof(JsonUpdatesMessage))]
[JsonSerializable(typeof(JsonChecksumMessage))]
[JsonSerializable(typeof(JsonPruneMessage))]
[JsonSerializable(typeof(JsonProfileMessage))]
[JsonSerializable(typeof(JsonProfilesMessage))]
[JsonSerializable(typeof(JsonMo2SelectedProfileMessage))]
[JsonSerializable(typeof(JsonMo2ProfilesMessage))]
[JsonSerializable(typeof(JsonModsMessage))]
[JsonSerializable(typeof(JsonModMessage))]
[JsonSerializable(typeof(JsonLogsFolderMessage))]
[JsonSerializable(typeof(JsonHashProgressMessage))]
[JsonSerializable(typeof(JsonHashArchiveMessage))]
[JsonSerializable(typeof(JsonResultMessage))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class JsonOutputCtx : JsonSerializerContext;
