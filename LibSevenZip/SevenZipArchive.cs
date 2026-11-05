using System.Runtime.ExceptionServices;
using LibSevenZip.Native;
using Microsoft.Win32.SafeHandles;

namespace LibSevenZip;

/// <summary>
/// An archive opened in-process through the 7-Zip library. Not thread-safe: use one
/// instance from one thread at a time.
/// </summary>
public sealed unsafe class SevenZipArchive : IDisposable
{
    private const int HeaderSize = 64 * 1024;

    private readonly string _path;
    private readonly SafeFileHandle _file;
    private readonly InStream _inStream;
    private readonly CallbackErrors _streamErrors;
    private NativeInArchive _archive;
    private IReadOnlyList<ArchiveEntry>? _entries;
    private bool _disposed;

    private SevenZipArchive(
        string path,
        SafeFileHandle file,
        InStream inStream,
        CallbackErrors streamErrors,
        NativeInArchive archive,
        ArchiveFormat format
    )
    {
        _path = path;
        _file = file;
        _inStream = inStream;
        _streamErrors = streamErrors;
        _archive = archive;
        Format = format;
    }

    public ArchiveFormat Format { get; }

    public IReadOnlyList<ArchiveEntry> Entries => _entries ??= ReadEntries();

    /// <summary>
    /// Opens an archive, detecting its format from its signature, then its extension.
    /// </summary>
    public static SevenZipArchive Open(string path, CancellationToken ct = default)
    {
        var fullPath = Path.GetFullPath(path);
        var file = File.OpenHandle(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var errors = new CallbackErrors();
        var inStream = new InStream(file, errors);
        try
        {
            var lastError = HResult.S_FALSE;
            var candidates = Candidates(file, fullPath);
            foreach (var format in candidates)
            {
                ct.ThrowIfCancellationRequested();

                var hr = TryOpen(format, inStream, errors, ct, out var archive);
                if (hr == HResult.S_OK)
                {
                    return new SevenZipArchive(fullPath, file, inStream, errors, archive, format);
                }

                if (errors.Exception is { } e)
                {
                    ExceptionDispatchInfo.Throw(e);
                }

                lastError = hr;
            }

            throw (candidates.Count, lastError) switch
            {
                (0, _) => new SevenZipException($"Unsupported archive type: {fullPath}"),
                (_, HResult.S_FALSE) => new SevenZipException(
                    $"Cannot open archive as {string.Join(" or ", candidates.Select(c => c.Name))}, it may be corrupt or truncated: {fullPath}"
                ),
                _ => new SevenZipException($"Cannot open archive: {fullPath}", lastError),
            };
        }
        catch
        {
            inStream.Release();
            file.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Extracts every item into <paramref name="destinationDir"/>, overwriting existing files.
    /// Backslashes in item paths are treated as directory separators on every platform,
    /// because zips made on Windows often use them. Items whose path would land outside
    /// the destination abort the extraction.
    /// </summary>
    /// <param name="progress">Fraction complete, 0 to 1.</param>
    public void ExtractTo(
        string destinationDir,
        Action<double>? progress = null,
        CancellationToken ct = default
    )
    {
        var root = Path.GetFullPath(destinationDir);
        Directory.CreateDirectory(root);
        ExtractTo(entry => ResolveUnder(root, entry.Path), progress, ct);
    }

    /// <summary>
    /// Extracts every item to the path <paramref name="resolveDestination"/> returns for it,
    /// overwriting existing files. Items it returns null for are skipped. The caller is
    /// responsible for keeping destinations where it wants them; <see cref="ResolveUnder"/>
    /// helps with that.
    /// </summary>
    /// <param name="resolveDestination">
    /// Returns the full path to extract an item to, or null to skip it. Called once per item,
    /// in archive order, on the extracting thread.
    /// </param>
    /// <param name="progress">Fraction complete, 0 to 1.</param>
    public void ExtractTo(
        Func<ArchiveEntry, string?> resolveDestination,
        Action<double>? progress = null,
        CancellationToken ct = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var errors = new CallbackErrors();
        var handler = new DirectoryExtractHandler(this, resolveDestination, progress);
        var callback = new ExtractCallback(handler, errors, ct);
        int hr;
        try
        {
            hr = _archive.Extract(null, uint.MaxValue, 0, callback.Pointer);
        }
        finally
        {
            callback.Release();
            handler.CloseCurrent();
        }

        if ((errors.Exception ?? _streamErrors.Exception) is { } e)
        {
            ExceptionDispatchInfo.Throw(e);
        }

        if (hr == HResult.E_ABORT)
        {
            ct.ThrowIfCancellationRequested();
        }

        SevenZipException.ThrowIfFailed(hr, $"extract of {_path}");

        if (handler.Failures.Count > 0)
        {
            throw new SevenZipException(
                $"{handler.Failures.Count} item(s) failed to extract from {_path}",
                handler.Failures
            );
        }

        progress?.Invoke(1);
    }

    /// <summary>
    /// Joins an archive item path onto <paramref name="root"/>, treating backslashes as
    /// directory separators. Throws if the result would land outside <paramref name="root"/>.
    /// </summary>
    /// <param name="root">A full path.</param>
    public static string ResolveUnder(string root, string itemPath)
    {
        var rootWithSeparator = Path.EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;
        var relative = itemPath.Replace('\\', '/').TrimStart('/');
        var full = Path.GetFullPath(Path.Join(root, relative));
        if (
            !full.StartsWith(rootWithSeparator, StringComparison.Ordinal)
            && !string.Equals(full, root, StringComparison.Ordinal)
        )
        {
            throw new SevenZipException(
                $"Archive item would extract outside of the destination: {itemPath}"
            );
        }

        return full;
    }

    /// <summary>Runs <see cref="ExtractTo"/> on a thread pool thread.</summary>
    public Task ExtractToAsync(
        string destinationDir,
        Action<double>? progress = null,
        CancellationToken ct = default
    ) => Task.Run(() => ExtractTo(destinationDir, progress, ct), ct);

    /// <summary>Opens <paramref name="archivePath"/> and extracts it.</summary>
    public static Task ExtractAsync(
        string archivePath,
        string destinationDir,
        Action<double>? progress = null,
        CancellationToken ct = default
    ) =>
        Task.Run(
            () =>
            {
                using var archive = Open(archivePath, ct);
                archive.ExtractTo(destinationDir, progress, ct);
            },
            ct
        );

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _archive.Close();
        _archive.Release();
        _archive = default;
        _inStream.Release();
        _file.Dispose();
    }

    internal ArchiveEntry ReadEntry(uint index)
    {
        var prop = default(PropVariant);

        string path = "";
        if (_archive.GetProperty(index, ItemPropId.Path, &prop) == HResult.S_OK)
        {
            path = prop.AsString() ?? "";
        }
        prop.Clear();

        var isDir = false;
        if (_archive.GetProperty(index, ItemPropId.IsDir, &prop) == HResult.S_OK)
        {
            isDir = prop.AsBool();
        }
        prop.Clear();

        ulong? size = null;
        if (_archive.GetProperty(index, ItemPropId.Size, &prop) == HResult.S_OK)
        {
            size = prop.AsUInt64();
        }
        prop.Clear();

        DateTime? mtime = null;
        if (_archive.GetProperty(index, ItemPropId.MTime, &prop) == HResult.S_OK)
        {
            mtime = prop.AsDateTimeUtc();
        }
        prop.Clear();

        var encrypted = false;
        if (_archive.GetProperty(index, ItemPropId.Encrypted, &prop) == HResult.S_OK)
        {
            encrypted = prop.AsBool();
        }
        prop.Clear();

        var altStream = false;
        if (_archive.GetProperty(index, ItemPropId.IsAltStream, &prop) == HResult.S_OK)
        {
            altStream = prop.AsBool();
        }
        prop.Clear();

        if (path.Length == 0 && !isDir)
        {
            // single-stream formats (gz, xz, zst, ...) may not store a name
            path = Path.GetFileNameWithoutExtension(_path);
        }

        return new ArchiveEntry(index, path, isDir, size, mtime, encrypted, altStream);
    }

    private IReadOnlyList<ArchiveEntry> ReadEntries()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        uint count;
        SevenZipException.ThrowIfFailed(_archive.GetNumberOfItems(&count), "GetNumberOfItems");
        var entries = new ArchiveEntry[count];
        for (uint i = 0; i < count; i++)
        {
            entries[i] = ReadEntry(i);
        }

        return entries;
    }

    private static int TryOpen(
        ArchiveFormat format,
        InStream inStream,
        CallbackErrors errors,
        CancellationToken ct,
        out NativeInArchive archive
    )
    {
        archive = default;

        var clsid = format.ClassId;
        var iid = Iid.IInArchive;
        void* obj;
        var hr = SevenZipNative.CreateObject(&clsid, &iid, &obj);
        if (hr != HResult.S_OK || obj is null)
        {
            return hr == HResult.S_OK ? HResult.E_FAIL : hr;
        }

        var candidate = new NativeInArchive(obj);
        var openCallback = new OpenCallback(errors, ct);
        try
        {
            inStream.Rewind();
            hr = candidate.Open(inStream.Pointer, null, openCallback.Pointer);
        }
        finally
        {
            openCallback.Release();
        }

        if (hr != HResult.S_OK)
        {
            candidate.Close();
            candidate.Release();
            return hr;
        }

        archive = candidate;
        return HResult.S_OK;
    }

    /// <summary>
    /// Formats whose signature matches the file header first, then formats that claim the
    /// file's extension. This mirrors (a simplified version of) 7-Zip's own detection.
    /// </summary>
    private static List<ArchiveFormat> Candidates(SafeFileHandle file, string path)
    {
        var header = new byte[HeaderSize];
        var headerLength = RandomAccess.Read(file, header, 0);

        var extension = Path.GetExtension(path).TrimStart('.');
        var candidates = new List<ArchiveFormat>();

        foreach (var format in SevenZipLibrary.Formats)
        {
            if (
                format.MatchesSignature(header.AsSpan(0, headerLength))
                && (
                    !format.Flags.HasFlag(ArchiveFormatFlags.ByExtOnlyOpen)
                    || format.MatchesExtension(extension)
                )
            )
            {
                candidates.Add(format);
            }
        }

        if (extension.Length > 0)
        {
            candidates.AddRange(
                SevenZipLibrary.Formats.Where(f =>
                    f.MatchesExtension(extension) && !candidates.Contains(f)
                )
            );
        }

        return candidates;
    }
}
