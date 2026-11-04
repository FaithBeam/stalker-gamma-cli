using System.Runtime.InteropServices;

namespace LibSevenZip.Native;

/// <summary>
/// Exports of the 7-Zip plugin library (7z.dll on Windows, 7z.so on Linux/macOS).
///
/// The exports are STDAPI: stdcall on win-x86, the platform default everywhere else,
/// which is what an unmanaged function pointer without an explicit convention uses.
/// </summary>
internal static unsafe class SevenZipNative
{
    private static readonly Lock InitLock = new();
    private static volatile bool _loaded;

    private static delegate* unmanaged<Guid*, Guid*, void**, int> _createObject;
    private static delegate* unmanaged<uint*, int> _getNumberOfFormats;
    private static delegate* unmanaged<uint, uint, PropVariant*, int> _getHandlerProperty2;
    private static delegate* unmanaged<void*, void> _sysFreeString;

    /// <summary>(major, minor) as reported by the library, if it reports it.</summary>
    public static Version? LibraryVersion { get; private set; }

    public static string? LoadedFrom { get; private set; }

    public static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        lock (InitLock)
        {
            if (_loaded)
            {
                return;
            }

            Load();
            _loaded = true;
        }
    }

    public static int CreateObject(Guid* clsid, Guid* iid, void** outObject)
    {
        EnsureLoaded();
        return _createObject(clsid, iid, outObject);
    }

    public static int GetNumberOfFormats(uint* numFormats)
    {
        EnsureLoaded();
        return _getNumberOfFormats(numFormats);
    }

    public static int GetHandlerProperty2(
        uint formatIndex,
        HandlerPropId propId,
        PropVariant* value
    )
    {
        EnsureLoaded();
        return _getHandlerProperty2(formatIndex, (uint)propId, value);
    }

    /// <summary>
    /// Only used on Linux/macOS, where 7-Zip implements BSTR itself.
    /// On Windows BSTRs come from oleaut32 and are freed with Marshal.FreeBSTR.
    /// </summary>
    public static void SysFreeString(void* bstr)
    {
        EnsureLoaded();
        _sysFreeString(bstr);
    }

    private static void Load()
    {
        var (lib, path) = OpenLibrary();
        try
        {
            CheckInterfaceType(lib, path);

            _createObject = (delegate* unmanaged<Guid*, Guid*, void**, int>)GetExport(
                lib,
                path,
                "CreateObject"
            );
            _getNumberOfFormats = (delegate* unmanaged<uint*, int>)GetExport(
                lib,
                path,
                "GetNumberOfFormats"
            );
            _getHandlerProperty2 = (delegate* unmanaged<uint, uint, PropVariant*, int>)GetExport(
                lib,
                path,
                "GetHandlerProperty2"
            );
            if (!OperatingSystem.IsWindows())
            {
                _sysFreeString = (delegate* unmanaged<void*, void>)GetExport(
                    lib,
                    path,
                    "SysFreeString"
                );
            }

            LoadedFrom = path;
        }
        catch
        {
            NativeLibrary.Free(lib);
            throw;
        }
    }

    private static (nint Handle, string Path) OpenLibrary()
    {
        if (SevenZipLibrary.LibraryPath is { } configured)
        {
            if (!NativeLibrary.TryLoad(configured, out var h))
            {
                throw new SevenZipException($"Could not load 7-Zip library: {configured}");
            }

            return (h, configured);
        }

        var fileName = OperatingSystem.IsWindows() ? "7z.dll" : "7z.so";
        var nextToApp = Path.Join(AppContext.BaseDirectory, fileName);
        if (NativeLibrary.TryLoad(nextToApp, out var handle))
        {
            return (handle, nextToApp);
        }

        if (NativeLibrary.TryLoad(fileName, out handle))
        {
            return (handle, fileName);
        }

        throw new SevenZipException(
            $"""
            Could not find the 7-Zip library ({fileName}).
            Set {nameof(SevenZipLibrary)}.{nameof(
                SevenZipLibrary.LibraryPath
            )} or place it next to the application.
            """
        );
    }

    /// <summary>
    /// p7zip and 7-Zip for Linux/macOS before 23.0 put a virtual destructor in IUnknown,
    /// which shifts every vtable slot after Release. Calling into such a library with the
    /// standard COM layout would crash, so refuse it, the same way 7-Zip itself does when
    /// loading plugins (LoadCodecs.cpp).
    /// </summary>
    private static void CheckInterfaceType(nint lib, string path)
    {
        const uint kInterfaceType = 0;
        const uint kVersion = 1;
        const uint virtDestructorNo = 0;

        if (!NativeLibrary.TryGetExport(lib, "GetModuleProp", out var addr))
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            throw new SevenZipException(
                $"7-Zip library is too old (p7zip or 7-Zip before 23.0 is not supported): {path}"
            );
        }

        var getModuleProp = (delegate* unmanaged<uint, PropVariant*, int>)addr;

        var prop = default(PropVariant);
        if (getModuleProp(kInterfaceType, &prop) == HResult.S_OK && prop.Vt == PropVariant.VT_UI4)
        {
            if (prop.UInt32 != virtDestructorNo)
            {
                throw new SevenZipException(
                    $"7-Zip library was built with a virtual destructor in IUnknown, which is not supported: {path}"
                );
            }
        }

        prop = default;
        if (getModuleProp(kVersion, &prop) == HResult.S_OK && prop.Vt == PropVariant.VT_UI4)
        {
            LibraryVersion = new Version((int)(prop.UInt32 >> 16), (int)(prop.UInt32 & 0xFFFF));
        }
    }

    private static nint GetExport(nint lib, string path, string name) =>
        NativeLibrary.TryGetExport(lib, name, out var addr)
            ? addr
            : throw new SevenZipException($"7-Zip library {path} does not export {name}");
}
