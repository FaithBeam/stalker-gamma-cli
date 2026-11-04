namespace LibSevenZip;

public sealed record ArchiveFormat(
    uint Index,
    string Name,
    Guid ClassId,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<byte[]> Signatures,
    uint SignatureOffset,
    ArchiveFormatFlags Flags
)
{
    internal bool MatchesSignature(ReadOnlySpan<byte> header)
    {
        foreach (var signature in Signatures)
        {
            var end = (long)SignatureOffset + signature.Length;
            if (
                end <= header.Length
                && header.Slice((int)SignatureOffset, signature.Length).SequenceEqual(signature)
            )
            {
                return true;
            }
        }

        return false;
    }

    internal bool MatchesExtension(string extension) =>
        Extensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
}

/// <summary>NArcInfoFlags</summary>
[Flags]
public enum ArchiveFormatFlags : uint
{
    None = 0,
    KeepName = 1 << 0,
    AltStreams = 1 << 1,
    NtSecure = 1 << 2,
    FindSignature = 1 << 3,
    MultiSignature = 1 << 4,
    UseGlobalOffset = 1 << 5,
    StartOpen = 1 << 6,
    PureStartOpen = 1 << 7,
    BackwardOpen = 1 << 8,
    PreArc = 1 << 9,
    SymLinks = 1 << 10,
    HardLinks = 1 << 11,
    ByExtOnlyOpen = 1 << 12,
    HashHandler = 1 << 13,
}

public sealed record ArchiveEntry(
    uint Index,
    string Path,
    bool IsDirectory,
    ulong? Size,
    DateTime? LastWriteTimeUtc,
    bool IsEncrypted,
    bool IsAltStream
);

/// <summary>NArchive::NExtract::NOperationResult</summary>
public enum OperationResult
{
    Ok = 0,
    UnsupportedMethod,
    DataError,
    CrcError,
    Unavailable,
    UnexpectedEnd,
    DataAfterEnd,
    IsNotArchive,
    HeadersError,
    WrongPassword,
}

public sealed record ExtractFailure(string Path, OperationResult Result);

public class SevenZipException : Exception
{
    public SevenZipException(string message)
        : base(message) { }

    public SevenZipException(string message, int errorCode)
        : base($"{message} (0x{errorCode:X8})")
    {
        HResult = errorCode;
    }

    public SevenZipException(string message, IReadOnlyList<ExtractFailure> failures)
        : base(
            $"""
            {message}
            {string.Join(Environment.NewLine, failures.Select(f => $"  {f.Path}: {f.Result}"))}
            """
        )
    {
        Failures = failures;
    }

    public IReadOnlyList<ExtractFailure> Failures { get; } = [];

    internal static void ThrowIfFailed(int hr, string operation)
    {
        if (hr != Native.HResult.S_OK)
        {
            throw new SevenZipException($"7-Zip {operation} failed", hr);
        }
    }
}
