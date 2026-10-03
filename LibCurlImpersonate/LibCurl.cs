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

    [LibraryImport(Lib)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial IntPtr curl_easy_strerror(int code);

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
    internal const int CURLOPT_ERRORBUFFER = 10010;
    internal const int CURL_ERROR_SIZE = 256;
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
    internal const int CURLE_UNSUPPORTED_PROTOCOL = 1;
    internal const int CURLE_FAILED_INIT = 2;
    internal const int CURLE_URL_MALFORMAT = 3;
    internal const int CURLE_NOT_BUILT_IN = 4;
    internal const int CURLE_COULDNT_RESOLVE_PROXY = 5;
    internal const int CURLE_COULDNT_RESOLVE_HOST = 6;
    internal const int CURLE_COULDNT_CONNECT = 7;
    internal const int CURLE_WEIRD_SERVER_REPLY = 8;
    internal const int CURLE_REMOTE_ACCESS_DENIED = 9;
    internal const int CURLE_FTP_ACCEPT_FAILED = 10;
    internal const int CURLE_FTP_WEIRD_PASS_REPLY = 11;
    internal const int CURLE_FTP_ACCEPT_TIMEOUT = 12;
    internal const int CURLE_FTP_WEIRD_PASV_REPLY = 13;
    internal const int CURLE_FTP_WEIRD_227_FORMAT = 14;
    internal const int CURLE_FTP_CANT_GET_HOST = 15;
    internal const int CURLE_HTTP2 = 16;
    internal const int CURLE_FTP_COULDNT_SET_TYPE = 17;
    internal const int CURLE_PARTIAL_FILE = 18;
    internal const int CURLE_FTP_COULDNT_RETR_FILE = 19;
    internal const int CURLE_QUOTE_ERROR = 21;
    internal const int CURLE_HTTP_RETURNED_ERROR = 22;
    internal const int CURLE_WRITE_ERROR = 23;
    internal const int CURLE_UPLOAD_FAILED = 25;
    internal const int CURLE_READ_ERROR = 26;
    internal const int CURLE_OUT_OF_MEMORY = 27;
    internal const int CURLE_OPERATION_TIMEDOUT = 28;
    internal const int CURLE_FTP_PORT_FAILED = 30;
    internal const int CURLE_FTP_COULDNT_USE_REST = 31;
    internal const int CURLE_RANGE_ERROR = 33;
    internal const int CURLE_SSL_CONNECT_ERROR = 35;
    internal const int CURLE_BAD_DOWNLOAD_RESUME = 36;
    internal const int CURLE_FILE_COULDNT_READ_FILE = 37;
    internal const int CURLE_LDAP_CANNOT_BIND = 38;
    internal const int CURLE_LDAP_SEARCH_FAILED = 39;
    internal const int CURLE_ABORTED_BY_CALLBACK = 42;
    internal const int CURLE_BAD_FUNCTION_ARGUMENT = 43;
    internal const int CURLE_INTERFACE_FAILED = 45;
    internal const int CURLE_TOO_MANY_REDIRECTS = 47;
    internal const int CURLE_UNKNOWN_OPTION = 48;
    internal const int CURLE_SETOPT_OPTION_SYNTAX = 49;
    internal const int CURLE_GOT_NOTHING = 52;
    internal const int CURLE_SSL_ENGINE_NOTFOUND = 53;
    internal const int CURLE_SSL_ENGINE_SETFAILED = 54;
    internal const int CURLE_SEND_ERROR = 55;
    internal const int CURLE_RECV_ERROR = 56;
    internal const int CURLE_SSL_CERTPROBLEM = 58;
    internal const int CURLE_SSL_CIPHER = 59;
    internal const int CURLE_PEER_FAILED_VERIFICATION = 60;
    internal const int CURLE_BAD_CONTENT_ENCODING = 61;
    internal const int CURLE_FILESIZE_EXCEEDED = 63;
    internal const int CURLE_USE_SSL_FAILED = 64;
    internal const int CURLE_SEND_FAIL_REWIND = 65;
    internal const int CURLE_SSL_ENGINE_INITFAILED = 66;
    internal const int CURLE_LOGIN_DENIED = 67;
    internal const int CURLE_TFTP_NOTFOUND = 68;
    internal const int CURLE_TFTP_PERM = 69;
    internal const int CURLE_REMOTE_DISK_FULL = 70;
    internal const int CURLE_TFTP_ILLEGAL = 71;
    internal const int CURLE_TFTP_UNKNOWNID = 72;
    internal const int CURLE_REMOTE_FILE_EXISTS = 73;
    internal const int CURLE_TFTP_NOSUCHUSER = 74;
    internal const int CURLE_SSL_CACERT_BADFILE = 77;
    internal const int CURLE_REMOTE_FILE_NOT_FOUND = 78;
    internal const int CURLE_SSH = 79;
    internal const int CURLE_SSL_SHUTDOWN_FAILED = 80;
    internal const int CURLE_AGAIN = 81;
    internal const int CURLE_SSL_CRL_BADFILE = 82;
    internal const int CURLE_SSL_ISSUER_ERROR = 83;
    internal const int CURLE_FTP_PRET_FAILED = 84;
    internal const int CURLE_RTSP_CSEQ_ERROR = 85;
    internal const int CURLE_RTSP_SESSION_ERROR = 86;
    internal const int CURLE_FTP_BAD_FILE_LIST = 87;
    internal const int CURLE_CHUNK_FAILED = 88;
    internal const int CURLE_NO_CONNECTION_AVAILABLE = 89;
    internal const int CURLE_SSL_PINNEDPUBKEYNOTMATCH = 90;
    internal const int CURLE_SSL_INVALIDCERTSTATUS = 91;
    internal const int CURLE_HTTP2_STREAM = 92;
    internal const int CURLE_RECURSIVE_API_CALL = 93;
    internal const int CURLE_AUTH_ERROR = 94;
    internal const int CURLE_HTTP3 = 95;
    internal const int CURLE_QUIC_CONNECT_ERROR = 96;
    internal const int CURLE_PROXY = 97;
    internal const int CURLE_SSL_CLIENTCERT = 98;
    internal const int CURLE_UNRECOVERABLE_POLL = 99;
    internal const int CURLE_TOO_LARGE = 100;
    internal const int CURLE_ECH_REQUIRED = 101;
}
