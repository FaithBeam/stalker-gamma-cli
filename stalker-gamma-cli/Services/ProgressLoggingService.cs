using Serilog;
using Stalker.Gamma.GammaInstallerServices;

namespace stalker_gamma_cli.Services;

public class LogFileRecord : IEquatable<LogFileRecord>
{
    public DateTimeOffset TimeStamp { get; set; } = DateTimeOffset.Now;
    public required string Operation { get; set; }
    public required string ArchiveName { get; set; }
    public required string DownloadPath { get; set; }
    public required string ExtractPath { get; set; }
    public required string Url { get; set; }

    public bool Equals(LogFileRecord? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Operation == other.Operation
            && ArchiveName == other.ArchiveName
            && DownloadPath == other.DownloadPath
            && ExtractPath == other.ExtractPath
            && Url == other.Url;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((LogFileRecord)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Operation, ArchiveName, DownloadPath, ExtractPath, Url);
    }
}

/// <summary>
/// Service responsible for collecting and writing progress events to a log file.
/// </summary>
public class ProgressLoggingService(ILogger logger)
{
    private readonly HashSet<LogFileRecord> _progressEventHashSet = [];
    private readonly Lock _progressEventHashSetLock = new();
    private bool _alreadyWrittenToLogFile;

    /// <summary>
    /// Writes all collected progress events to the log file if not already written.
    /// </summary>
    public void WriteToLogFile()
    {
        if (_alreadyWrittenToLogFile)
        {
            return;
        }

        lock (_progressEventHashSetLock)
        {
            var maxOperationLen = _progressEventHashSet.Max(x => x.Operation.Length);
            var maxArchiveNameLen = _progressEventHashSet.Max(x => x.ArchiveName.Length);
            var maxDownloadPathLen = _progressEventHashSet.Max(x => x.DownloadPath.Length);
            var maxExtractPathLen = _progressEventHashSet.Max(x => x.ExtractPath.Length);
            var maxUrlLen = _progressEventHashSet.Max(x => x.Url.Length);

            foreach (var e in _progressEventHashSet)
            {
                logger.Verbose(
                    "[{DateTime:yyyy-MM-dd HH:mm:ss}] Operation: {Operation} | Archive Name: {ArchiveName} | Download Path: {DownloadPath} | Extract Path: {ExtractPath} | Url: {Url}",
                    e.TimeStamp,
                    e.Operation.PadRight(maxOperationLen),
                    e.ArchiveName.PadRight(maxArchiveNameLen),
                    e.DownloadPath.PadRight(maxDownloadPathLen),
                    e.ExtractPath.PadRight(maxExtractPathLen),
                    e.Url.PadRight(maxUrlLen)
                );
            }
        }

        _alreadyWrittenToLogFile = true;
    }

    public void OnProgressChangedWriteToFile(GammaProgress.GammaInstallProgressEventArgs e) =>
        AddProgressEvent(
            new LogFileRecord
            {
                Operation = e.ProgressType,
                ArchiveName = e.Name,
                Url = e.Url,
                DownloadPath = e.DownloadPath,
                ExtractPath = e.ExtractPath,
            }
        );

    /// <summary>
    /// Adds a progress event to the collection.
    /// </summary>
    private void AddProgressEvent(LogFileRecord record)
    {
        lock (_progressEventHashSetLock)
        {
            _progressEventHashSet.Add(record);
        }
    }
}
