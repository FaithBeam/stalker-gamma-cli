using LibSevenZip.Native;

namespace LibSevenZip;

/// <summary>Writes extracted items under a root directory.</summary>
internal sealed class DirectoryExtractHandler(
    SevenZipArchive archive,
    string root,
    Action<double>? progress
) : IExtractHandler
{
    private readonly string _rootWithSeparator = Path.EndsInDirectorySeparator(root)
        ? root
        : root + Path.DirectorySeparatorChar;

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

        var destination = Resolve(entry.Path);
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

    private string Resolve(string itemPath)
    {
        var relative = itemPath.Replace('\\', '/').TrimStart('/');
        var full = Path.GetFullPath(Path.Join(root, relative));
        if (
            !full.StartsWith(_rootWithSeparator, StringComparison.Ordinal)
            && !string.Equals(full, root, StringComparison.Ordinal)
        )
        {
            throw new SevenZipException(
                $"Archive item would extract outside of the destination: {itemPath}"
            );
        }

        return full;
    }
}
