using System.Runtime.InteropServices;
using System.Text;

namespace LibSevenZip.Native;

internal static class HResult
{
    public const int S_OK = 0;
    public const int S_FALSE = 1;
    public const int E_NOTIMPL = unchecked((int)0x80004001);
    public const int E_NOINTERFACE = unchecked((int)0x80004002);
    public const int E_ABORT = unchecked((int)0x80004004);
    public const int E_FAIL = unchecked((int)0x80004005);
    public const int STG_E_INVALIDFUNCTION = unchecked((int)0x80030001);
    public const int E_OUTOFMEMORY = unchecked((int)0x8007000E);
    public const int E_INVALIDARG = unchecked((int)0x80070057);
}

/// <summary>
/// 7-Zip interface ids are {23170F69-40C1-278A-0000-00gg00ss0000}
/// where gg is the interface group and ss the interface within it.
/// </summary>
internal static class Iid
{
    public static readonly Guid IUnknown = new("00000000-0000-0000-C000-000000000046");

    public static readonly Guid IProgress = SevenZip(0, 0x05);

    public static readonly Guid ISequentialInStream = SevenZip(3, 0x01);
    public static readonly Guid ISequentialOutStream = SevenZip(3, 0x02);
    public static readonly Guid IInStream = SevenZip(3, 0x03);

    public static readonly Guid IArchiveOpenCallback = SevenZip(6, 0x10);
    public static readonly Guid IArchiveExtractCallback = SevenZip(6, 0x20);
    public static readonly Guid IInArchive = SevenZip(6, 0x60);

    private static Guid SevenZip(byte group, byte sub) =>
        new(0x23170F69, 0x40C1, 0x278A, 0, 0, 0, group, 0, sub, 0, 0);
}

/// <summary>NArchive::NHandlerPropID</summary>
internal enum HandlerPropId : uint
{
    Name = 0,
    ClassId = 1,
    Extension = 2,
    AddExtension = 3,
    Update = 4,
    KeepName = 5,
    Signature = 6,
    MultiSignature = 7,
    SignatureOffset = 8,
    AltStreams = 9,
    NtSecure = 10,
    Flags = 11,
    TimeFlags = 12,
}

/// <summary>Subset of kpid* from PropID.h</summary>
internal enum ItemPropId : uint
{
    Path = 3,
    IsDir = 6,
    Size = 7,
    PackSize = 8,
    Attrib = 9,
    MTime = 12,
    Encrypted = 15,
    PosixAttrib = 53,
    SymLink = 54,
    IsAltStream = 63,
}

/// <summary>NArchive::NExtract::NAskMode</summary>
internal enum AskMode
{
    Extract = 0,
    Test = 1,
    Skip = 2,
    ReadExternal = 3,
}

/// <summary>
/// PROPVARIANT. 7-Zip's own definition on Linux/macOS is 16 bytes, the Windows one is
/// 24 bytes on 64-bit, so this is sized for the larger of the two. 7-Zip only ever
/// writes the type tag and the first 8 bytes of the union.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 24)]
internal unsafe struct PropVariant
{
    public const ushort VT_EMPTY = 0;
    public const ushort VT_I2 = 2;
    public const ushort VT_I4 = 3;
    public const ushort VT_BSTR = 8;
    public const ushort VT_BOOL = 11;
    public const ushort VT_I1 = 16;
    public const ushort VT_UI1 = 17;
    public const ushort VT_UI2 = 18;
    public const ushort VT_UI4 = 19;
    public const ushort VT_I8 = 20;
    public const ushort VT_UI8 = 21;
    public const ushort VT_INT = 22;
    public const ushort VT_UINT = 23;
    public const ushort VT_FILETIME = 64;

    [FieldOffset(0)]
    public ushort Vt;

    [FieldOffset(8)]
    public long Int64;

    [FieldOffset(8)]
    public ulong UInt64;

    [FieldOffset(8)]
    public int Int32;

    [FieldOffset(8)]
    public uint UInt32;

    [FieldOffset(8)]
    public short Int16;

    [FieldOffset(8)]
    public ushort UInt16;

    [FieldOffset(8)]
    public byte Byte;

    [FieldOffset(8)]
    public void* Pointer;

    public readonly string? AsString() => Vt == VT_BSTR ? Bstr.ToString(Pointer) : null;

    public readonly byte[]? AsBytes() => Vt == VT_BSTR ? Bstr.ToBytes(Pointer) : null;

    public readonly bool AsBool() => Vt == VT_BOOL && Int16 != 0;

    public readonly ulong? AsUInt64() =>
        Vt switch
        {
            VT_UI1 => Byte,
            VT_UI2 => UInt16,
            VT_UI4 or VT_UINT => UInt32,
            VT_UI8 => UInt64,
            VT_I1 => (ulong)(sbyte)Byte,
            VT_I2 => (ulong)Int16,
            VT_I4 or VT_INT => (ulong)Int32,
            VT_I8 => (ulong)Int64,
            _ => null,
        };

    public readonly DateTime? AsDateTimeUtc()
    {
        if (Vt != VT_FILETIME || Int64 <= 0)
        {
            return null;
        }

        try
        {
            return DateTime.FromFileTimeUtc(Int64);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public void Clear()
    {
        if (Vt == VT_BSTR)
        {
            Bstr.Free(Pointer);
        }

        this = default;
    }
}

/// <summary>
/// BSTR: a length-prefixed (UInt32 byte length) string of OLECHAR. OLECHAR is wchar_t,
/// which is UTF-16 on Windows and UTF-32 on Linux/macOS. 7-Zip also stores binary data
/// (GUIDs, signatures) in BSTRs.
/// </summary>
internal static unsafe class Bstr
{
    private static readonly Encoding OleCharEncoding = OperatingSystem.IsWindows()
        ? Encoding.Unicode
        : Encoding.UTF32;

    public static uint ByteLength(void* bstr) => bstr is null ? 0 : ((uint*)bstr)[-1];

    public static string? ToString(void* bstr) =>
        bstr is null
            ? null
            : OleCharEncoding.GetString((byte*)bstr, checked((int)ByteLength(bstr)));

    public static byte[]? ToBytes(void* bstr) =>
        bstr is null ? null : new ReadOnlySpan<byte>(bstr, (int)ByteLength(bstr)).ToArray();

    public static void Free(void* bstr)
    {
        if (bstr is null)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Marshal.FreeBSTR((IntPtr)bstr);
        }
        else
        {
            SevenZipNative.SysFreeString(bstr);
        }
    }
}
