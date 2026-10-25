using System.Collections.Concurrent;

namespace Stalker.Gamma.Models;

/// <summary>
/// The step a record is currently at.
/// Download: waiting on (or in the middle of) download.
/// Extract: downloaded, waiting on (or in the middle of) extraction.
/// Complete: downloaded and extracted.
/// </summary>
public enum ResumeStatus
{
    Download,
    Extract,
    Complete,
}

/// <summary>
/// Tracks the status of each record during an install so an interrupted install can be resumed.
/// </summary>
public class ResumeState
{
    public const string FileName = "resume.txt";
    private const string CommandPrefix = "# command: ";

    private readonly ConcurrentDictionary<string, ResumeStatus> _statuses = new();
    private readonly Lock _writeLock = new();

    public string? Command { get; init; }

    /// <summary>
    /// When set, the resume file is rewritten here every time a record changes status
    /// </summary>
    public string? FilePath { get; set; }

    public bool IsEmpty => _statuses.IsEmpty;

    public static string GetPath(string gammaDir) => Path.Join(gammaDir, FileName);

    public ResumeStatus? GetStatus(string key) =>
        _statuses.TryGetValue(key, out var status) ? status : null;

    public bool IsComplete(string key) => GetStatus(key) == ResumeStatus.Complete;

    /// <summary>
    /// Start tracking a record. Records already tracked (e.g. loaded from a resume file) keep their status.
    /// </summary>
    public void Register(string key) => _statuses.TryAdd(key, ResumeStatus.Download);

    public void SetStatus(string key, ResumeStatus status)
    {
        _statuses[key] = status;
        if (FilePath is null)
        {
            return;
        }
        try
        {
            Write(FilePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // don't fail the install over the resume file, it's written again if the install fails
        }
    }

    public void Write(string path)
    {
        lock (_writeLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            // write to a temp file first so the resume file isn't corrupted if the process is killed mid-write
            var tmpPath = $"{path}.tmp";
            File.WriteAllLines(tmpPath, BuildLines());
            File.Move(tmpPath, path, overwrite: true);
        }
    }

    private List<string> BuildLines()
    {
        var lines = new List<string>
        {
            "# stalker-gamma resume file. Run the same command with --resume to continue.",
            $"{CommandPrefix}{Command}",
            "# status\trecord",
        };
        lines.AddRange(
            _statuses
                .OrderBy(x => x.Value)
                .ThenBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Value}\t{x.Key}")
        );
        return lines;
    }

    public static async Task<ResumeState> ReadAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var command = lines
            .FirstOrDefault(x => x.StartsWith(CommandPrefix, StringComparison.Ordinal))
            ?[CommandPrefix.Length..]?.Trim();
        var state = new ResumeState { Command = command };
        foreach (var line in lines.Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith('#')))
        {
            var split = line.Split('\t', 2);
            if (split.Length != 2 || !Enum.TryParse<ResumeStatus>(split[0], out var status))
            {
                throw new ResumeStateException($"Invalid line in {path}: {line}");
            }
            state.SetStatus(split[1], status);
        }
        return state;
    }
}

public class ResumeStateException(string message) : Exception(message);
