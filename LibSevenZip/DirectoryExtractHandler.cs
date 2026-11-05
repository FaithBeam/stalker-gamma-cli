using LibSevenZip.Native;

namespace LibSevenZip;

/// <summary>
/// Writes extracted items to the paths chosen by <paramref name="resolveDestination"/>.
/// Items it returns null for are skipped.
/// </summary>
internal sealed class DirectoryExtractHandler(
    SevenZipArchive archive,
    Func<ArchiveEntry, string?> resolveDestination,
    Action<double>? progress
) : IExtractHandler
{
    private ulong _total;
    private ArchiveEntry? _currentEntry;
    private string? _currentPath;
    private FileStream? _currentStream;

    public List<ExtractFailure> Failures { get; } = [];

    public void SetTotal(ulong total) => _total = total;

    public void SetCompleted(ulong completed)
    {
        if (_total > 0)
        {
            progress?.Invoke(Math.Min(1, completed / (double)_total));
        }
    }

    public Stream? GetStream(uint index, AskMode askMode)
    {
        CloseCurrent();
        if (askMode != AskMode.Extract)
        {
            return null;
        }

        var entry = archive.ReadEntry(index);
        if (entry.IsAltStream)
        {
            return null;
        }

        var destination = resolveDestination(entry);
        if (destination is null)
        {
            return null;
        }

        _currentEntry = entry;
        _currentPath = destination;

        if (entry.IsDirectory)
        {
            Directory.CreateDirectory(destination);
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        _currentStream = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1 << 16
        );
        return _currentStream;
    }

    public void PrepareOperation(AskMode askMode) { }

    public void SetOperationResult(OperationResult result)
    {
        var entry = _currentEntry;
        var path = _currentPath;
        CloseCurrent();

        if (entry is null || path is null)
        {
            return;
        }

        if (result != OperationResult.Ok)
        {
            Failures.Add(new ExtractFailure(entry.Path, result));
            return;
        }

        if (!entry.IsDirectory && entry.LastWriteTimeUtc is { } mtime)
        {
            File.SetLastWriteTimeUtc(path, mtime);
        }
    }

    public void CloseCurrent()
    {
        _currentStream?.Dispose();
        _currentStream = null;
        _currentEntry = null;
        _currentPath = null;
    }
}
