using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibCurlImpersonate;

internal static partial class LibCurl
{
    private const string Lib = "libcurl-impersonate";

    static LibCurl()
    {
        NativeLibrary.SetDllImportResolver(
            typeof(LibCurl).Assembly,
            static (name, assembly, searchPath) =>
            {
                if (name != Lib)
                    return IntPtr.Zero;
                var fileName =
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "libcurl-impersonate.dll"
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libcurl-impersonate.dylib"
                    : "libcurl-impersonate.so";
                return NativeLibrary.Load(fileName, assembly, searchPath);
            }
        );
    }

    [LibraryImport(Lib)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int curl_global_init(long flags);

    [LibraryImport(Lib)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void curl_global_cleanup();

    [LibraryImport(Lib)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial IntPtr curl_easy_init();

    [LibraryImport(Lib)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void curl_easy_cleanup(IntPtr handle);

    [LibraryImport(Lib)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int curl_easy_perform(IntPtr handle);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int curl_easy_impersonate(
        IntPtr handle,
        string target,
        int defaultHeaders
    );

    private static readonly bool _isMacOsArm64 =
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

    // curl_easy_setopt is variadic in C. On both x64 (R8) and ARM64/AAPCS64 (x2),
    // the 3rd argument is passed in the first available register — no stack padding needed.
    [LibraryImport(
        Lib,
        EntryPoint = "curl_easy_setopt",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int setopt_str_impl(IntPtr h, int opt, string value);

    [LibraryImport(Lib, EntryPoint = "curl_easy_setopt")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int setopt_long_impl(IntPtr h, int opt, long value);

    [LibraryImport(Lib, EntryPoint = "curl_easy_setopt")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int setopt_ptr_impl(IntPtr h, int opt, IntPtr value);

    [LibraryImport(Lib, EntryPoint = "curl_easy_setopt")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int setopt_cb_impl(
        IntPtr h,
        int opt,
        [MarshalAs(UnmanagedType.FunctionPtr)] WriteCallback value
    );

    [LibraryImport(Lib, EntryPoint = "curl_easy_setopt")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int setopt_xcb_impl(
        IntPtr h,
        int opt,
        [MarshalAs(UnmanagedType.FunctionPtr)] XferInfoCallback value
    );

    [LibraryImport(Lib, EntryPoint = "curl_easy_getinfo")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int getinfo_long_impl(IntPtr h, int info, out long value);

    // ARM64 calling convention fix: fill x2-x7 with dummy zeros so the real value
    // lands on the stack at [old_sp], which is where Curl_vsetopt reads it from.
    [LibraryImport(
        Lib,
        EntryPoint = "curl_easy_setopt",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int setopt_str_macos_arm64_impl(
        IntPtr h,
        int opt,
        nint _2,
        nint _3,
        nint _4,
        nint _5,
        nint _6,
        nint _7,
        string value
    );

    [LibraryImport(Lib, EntryPoint = "curl_easy_setopt")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int setopt_long_macos_arm64_impl(
        IntPtr h,
        int opt,
        nint _2,
        nint _3,
        nint _4,
        nint _5,
        nint _6,
        nint _7,
        long value
    );

    [LibraryImport(Lib, EntryPoint = "curl_easy_setopt")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int setopt_ptr_macos_arm64_impl(
        IntPtr h,
        int opt,
        nint _2,
        nint _3,
        nint _4,
        nint _5,
        nint _6,
        nint _7,
        IntPtr value
    );

    [LibraryImport(Lib, EntryPoint = "curl_easy_setopt")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int setopt_cb_macos_arm64_impl(
        IntPtr h,
        int opt,
        nint _2,
        nint _3,
        nint _4,
        nint _5,
        nint _6,
        nint _7,
        [MarshalAs(UnmanagedType.FunctionPtr)] WriteCallback value
    );

    [LibraryImport(Lib, EntryPoint = "curl_easy_setopt")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int setopt_xcb_macos_arm64_impl(
        IntPtr h,
        int opt,
        nint _2,
        nint _3,
        nint _4,
        nint _5,
        nint _6,
        nint _7,
        [MarshalAs(UnmanagedType.FunctionPtr)] XferInfoCallback value
    );

    [LibraryImport(Lib, EntryPoint = "curl_easy_getinfo")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int getinfo_long_macos_arm64_impl(
        IntPtr h,
        int info,
        nint _2,
        nint _3,
        nint _4,
        nint _5,
        nint _6,
        nint _7,
        out long value
    );

    internal static int curl_easy_getinfo_long(IntPtr h, int info, out long value) =>
        _isMacOsArm64
            ? getinfo_long_macos_arm64_impl(h, info, 0, 0, 0, 0, 0, 0, out value)
            : getinfo_long_impl(h, info, out value);

    internal static int curl_easy_setopt_str(IntPtr h, int opt, string v) =>
        _isMacOsArm64
            ? setopt_str_macos_arm64_impl(h, opt, 0, 0, 0, 0, 0, 0, v)
            : setopt_str_impl(h, opt, v);

    internal static int curl_easy_setopt_long(IntPtr h, int opt, long v) =>
        _isMacOsArm64
            ? setopt_long_macos_arm64_impl(h, opt, 0, 0, 0, 0, 0, 0, v)
            : setopt_long_impl(h, opt, v);

    internal static int curl_easy_setopt_ptr(IntPtr h, int opt, IntPtr v) =>
        _isMacOsArm64
            ? setopt_ptr_macos_arm64_impl(h, opt, 0, 0, 0, 0, 0, 0, v)
            : setopt_ptr_impl(h, opt, v);

    internal static int curl_easy_setopt_cb(IntPtr h, int opt, WriteCallback v) =>
        _isMacOsArm64
            ? setopt_cb_macos_arm64_impl(h, opt, 0, 0, 0, 0, 0, 0, v)
            : setopt_cb_impl(h, opt, v);

    internal static int curl_easy_setopt_xcb(IntPtr h, int opt, XferInfoCallback v) =>
        _isMacOsArm64
            ? setopt_xcb_macos_arm64_impl(h, opt, 0, 0, 0, 0, 0, 0, v)
            : setopt_xcb_impl(h, opt, v);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nuint WriteCallback(IntPtr data, nuint size, nuint nmemb, IntPtr userdata);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int XferInfoCallback(
        IntPtr clientp,
        long dltotal,
        long dlnow,
        long ultotal,
        long ulnow
    );

    internal const long CURL_GLOBAL_DEFAULT = 3;
    internal const int CURLOPT_URL = 10002;
    internal const int CURLOPT_WRITEFUNCTION = 20011;
    internal const int CURLOPT_WRITEDATA = 10001;
    internal const int CURLOPT_FOLLOWLOCATION = 52;
    internal const int CURLOPT_CAINFO = 10065;
    internal const int CURLOPT_ACCEPT_ENCODING = 10102;
    internal const int CURLOPT_HEADERFUNCTION = 20079;
    internal const int CURLOPT_HEADERDATA = 10029;
    internal const int CURLOPT_NOBODY = 44;
    internal const int CURLOPT_NOPROGRESS = 43;
    internal const int CURLOPT_XFERINFOFUNCTION = 20219;
    internal const int CURLOPT_XFERINFODATA = 10057;
    internal const int CURLOPT_HTTP_VERSION = 84;
    internal const long CURL_HTTP_VERSION_3 = 30;
    internal const int CURLINFO_HTTP_VERSION = 0x200000 + 46;
    internal const int CURLINFO_SPEED_DOWNLOAD_T = 0x600000 + 9;
    internal const int CURLE_OK = 0;
    internal const int CURLE_ABORTED_BY_CALLBACK = 42;
}
