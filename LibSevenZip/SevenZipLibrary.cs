using LibSevenZip.Native;

namespace LibSevenZip;

public static unsafe class SevenZipLibrary
{
    private static readonly Lazy<IReadOnlyList<ArchiveFormat>> LazyFormats = new(LoadFormats);

    /// <summary>
    /// Path to 7z.dll / 7z.so. Must be set before the first use of the library.
    /// When null, the library is looked up next to the application, then on the
    /// system library search path.
    /// </summary>
    public static string? LibraryPath { get; set; }

    /// <summary>Loads the library now instead of on first use, surfacing load errors early.</summary>
    public static void Load() => SevenZipNative.EnsureLoaded();

    /// <summary>The loaded library's version, if it reports one (7-Zip 23+).</summary>
    public static Version? Version
    {
        get
        {
            SevenZipNative.EnsureLoaded();
            return SevenZipNative.LibraryVersion;
        }
    }

    /// <summary>Archive formats supported by the loaded library.</summary>
    public static IReadOnlyList<ArchiveFormat> Formats => LazyFormats.Value;

    private static IReadOnlyList<ArchiveFormat> LoadFormats()
    {
        uint count;
        SevenZipException.ThrowIfFailed(
            SevenZipNative.GetNumberOfFormats(&count),
            "GetNumberOfFormats"
        );

        var formats = new List<ArchiveFormat>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var classIdBytes = GetBytes(i, HandlerPropId.ClassId);
            if (classIdBytes is not { Length: 16 })
            {
                continue;
            }

            var signatures = new List<byte[]>();
            if (GetBytes(i, HandlerPropId.Signature) is { Length: > 0 } signature)
            {
                signatures.Add(signature);
            }

            if (GetBytes(i, HandlerPropId.MultiSignature) is { Length: > 0 } multi)
            {
                signatures.AddRange(ParseMultiSignature(multi));
            }

            formats.Add(
                new ArchiveFormat(
                    Index: i,
                    Name: GetString(i, HandlerPropId.Name) ?? $"#{i}",
                    ClassId: new Guid(classIdBytes),
                    Extensions: (GetString(i, HandlerPropId.Extension) ?? "").Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries
                    ),
                    Signatures: signatures,
                    SignatureOffset: (uint)(GetUInt64(i, HandlerPropId.SignatureOffset) ?? 0),
                    Flags: (ArchiveFormatFlags)(GetUInt64(i, HandlerPropId.Flags) ?? 0)
                )
            );
        }

        return formats;
    }

    /// <summary>Sequence of [length byte][signature bytes] (LoadCodecs.cpp ParseSignatures).</summary>
    private static IEnumerable<byte[]> ParseMultiSignature(byte[] data)
    {
        var pos = 0;
        while (pos < data.Length)
        {
            var len = data[pos++];
            if (len > data.Length - pos)
            {
                yield break;
            }

            yield return data[pos..(pos + len)];
            pos += len;
        }
    }

    private static string? GetString(uint index, HandlerPropId id)
    {
        var prop = default(PropVariant);
        try
        {
            return SevenZipNative.GetHandlerProperty2(index, id, &prop) == HResult.S_OK
                ? prop.AsString()
                : null;
        }
        finally
        {
            prop.Clear();
        }
    }

    private static byte[]? GetBytes(uint index, HandlerPropId id)
    {
        var prop = default(PropVariant);
        try
        {
            return SevenZipNative.GetHandlerProperty2(index, id, &prop) == HResult.S_OK
                ? prop.AsBytes()
                : null;
        }
        finally
        {
            prop.Clear();
        }
    }

    private static ulong? GetUInt64(uint index, HandlerPropId id)
    {
        var prop = default(PropVariant);
        try
        {
            return SevenZipNative.GetHandlerProperty2(index, id, &prop) == HResult.S_OK
                ? prop.AsUInt64()
                : null;
        }
        finally
        {
            prop.Clear();
        }
    }
}
