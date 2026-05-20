using System.Runtime.InteropServices;
using System.Text;

namespace LibCurlImpersonate;

public class CurlHttp : IDisposable
{
    static CurlHttp() => LibCurl.curl_global_init(LibCurl.CURL_GLOBAL_DEFAULT);

    private static readonly string CurDir = Path.Join(
        Path.GetDirectoryName(AppContext.BaseDirectory)
    );

    private static readonly string PathToCacert = Path.Join(CurDir, "cacert.pem");

    public static IEnumerable<string> FetchLines(
        string url,
        Action<double>? onSpeed = null,
        bool http3 = false,
        Action<string>? onHttpVersion = null,
        CancellationToken ct = default
    )
    {
        using var reader = new StringReader(Fetch(url, onSpeed, http3, onHttpVersion, ct));
        while (reader.ReadLine() is { } line)
            yield return line;
    }

    public static string Fetch(
        string url,
        Action<double>? onSpeed = null,
        bool http3 = false,
        Action<string>? onHttpVersion = null,
        CancellationToken ct = default
    )
    {
        IntPtr handle = LibCurl.curl_easy_init();
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("curl_easy_init failed");

        var sb = new StringBuilder();
        var writePin = GCHandle.Alloc(sb);
        var xferPin = InstallXferCallback(handle, ct, onSpeed: onSpeed);
        try
        {
            LibCurl.curl_easy_impersonate(handle, Impersonation, 1);
            LibCurl.curl_easy_setopt_str(handle, LibCurl.CURLOPT_ACCEPT_ENCODING, "");
            LibCurl.curl_easy_setopt_str(handle, LibCurl.CURLOPT_URL, url);
            LibCurl.curl_easy_setopt_cb(handle, LibCurl.CURLOPT_WRITEFUNCTION, OnWrite);
            LibCurl.curl_easy_setopt_ptr(
                handle,
                LibCurl.CURLOPT_WRITEDATA,
                GCHandle.ToIntPtr(writePin)
            );
            LibCurl.curl_easy_setopt_long(handle, LibCurl.CURLOPT_FOLLOWLOCATION, 1L);
            LibCurl.curl_easy_setopt_str(handle, LibCurl.CURLOPT_CAINFO, PathToCacert);
            if (http3)
                LibCurl.curl_easy_setopt_long(
                    handle,
                    LibCurl.CURLOPT_HTTP_VERSION,
                    LibCurl.CURL_HTTP_VERSION_3
                );

            int code = LibCurl.curl_easy_perform(handle);
            if (code == LibCurl.CURLE_ABORTED_BY_CALLBACK)
                ct.ThrowIfCancellationRequested();
            if (code != LibCurl.CURLE_OK)
                throw new InvalidOperationException(
                    $"curl_easy_perform returned error code {code}"
                );

            ReportHttpVersion(handle, onHttpVersion);
            return sb.ToString();
        }
        finally
        {
            writePin.Free();
            if (xferPin.IsAllocated)
                xferPin.Free();
            LibCurl.curl_easy_cleanup(handle);
        }
    }

    public static Dictionary<string, string> GetHeaders(
        string url,
        bool http3 = false,
        Action<string>? onHttpVersion = null,
        CancellationToken ct = default
    )
    {
        IntPtr handle = LibCurl.curl_easy_init();
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("curl_easy_init failed");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var headerPin = GCHandle.Alloc(headers);
        var xferPin = InstallXferCallback(handle, ct);
        try
        {
            LibCurl.curl_easy_impersonate(handle, Impersonation, 1);
            LibCurl.curl_easy_setopt_str(handle, LibCurl.CURLOPT_URL, url);
            LibCurl.curl_easy_setopt_cb(handle, LibCurl.CURLOPT_HEADERFUNCTION, OnHeader);
            LibCurl.curl_easy_setopt_ptr(
                handle,
                LibCurl.CURLOPT_HEADERDATA,
                GCHandle.ToIntPtr(headerPin)
            );
            LibCurl.curl_easy_setopt_long(handle, LibCurl.CURLOPT_NOBODY, 1L);
            LibCurl.curl_easy_setopt_str(handle, LibCurl.CURLOPT_CAINFO, PathToCacert);
            if (http3)
                LibCurl.curl_easy_setopt_long(
                    handle,
                    LibCurl.CURLOPT_HTTP_VERSION,
                    LibCurl.CURL_HTTP_VERSION_3
                );

            int code = LibCurl.curl_easy_perform(handle);
            if (code == LibCurl.CURLE_ABORTED_BY_CALLBACK)
                ct.ThrowIfCancellationRequested();
            if (code != LibCurl.CURLE_OK)
                throw new InvalidOperationException(
                    $"curl_easy_perform returned error code {code}"
                );

            ReportHttpVersion(handle, onHttpVersion);
            return headers;
        }
        finally
        {
            headerPin.Free();
            if (xferPin.IsAllocated)
                xferPin.Free();
            LibCurl.curl_easy_cleanup(handle);
        }
    }

    private static void ReportHttpVersion(IntPtr handle, Action<string>? cb)
    {
        if (cb is null)
            return;
        LibCurl.curl_easy_getinfo_long(handle, LibCurl.CURLINFO_HTTP_VERSION, out long v);
        cb(
            v switch
            {
                10 => "HTTP/1.0",
                11 => "HTTP/1.1",
                20 => "HTTP/2",
                30 => "HTTP/3",
                _ => $"HTTP/?",
            }
        );
    }

    // Installs CURLOPT_XFERINFOFUNCTION for cancellation and optional progress reporting.
    // Skipped entirely when neither is needed to avoid the NOPROGRESS overhead.
    private static GCHandle InstallXferCallback(
        IntPtr handle,
        CancellationToken ct,
        Action<double>? onProgress = null,
        Action<double>? onSpeed = null
    )
    {
        if (!ct.CanBeCanceled && onProgress is null && onSpeed is null)
            return default;

        LibCurl.XferInfoCallback cb = (_, total, now, _, _) =>
        {
            if (ct.IsCancellationRequested)
                return 1;
            if (onProgress is not null && total > 0)
                onProgress(now / (double)total);
            if (onSpeed is not null)
            {
                LibCurl.curl_easy_getinfo_long(
                    handle,
                    LibCurl.CURLINFO_SPEED_DOWNLOAD_T,
                    out long bps
                );
                onSpeed(bps);
            }
            return 0;
        };
        var pin = GCHandle.Alloc(cb);
        LibCurl.curl_easy_setopt_long(handle, LibCurl.CURLOPT_NOPROGRESS, 0L);
        LibCurl.curl_easy_setopt_xcb(handle, LibCurl.CURLOPT_XFERINFOFUNCTION, cb);
        return pin;
    }

    private static nuint OnWrite(IntPtr data, nuint size, nuint nmemb, IntPtr userdata)
    {
        nuint total = size * nmemb;
        var sb = (StringBuilder)GCHandle.FromIntPtr(userdata).Target!;
        sb.Append(Marshal.PtrToStringUTF8(data, (int)total));
        return total;
    }

    private static unsafe nuint OnWriteFile(IntPtr data, nuint size, nuint nmemb, IntPtr userdata)
    {
        nuint total = size * nmemb;
        var fs = (FileStream)GCHandle.FromIntPtr(userdata).Target!;
        fs.Write(new ReadOnlySpan<byte>((void*)data, (int)total));
        return total;
    }

    private static nuint OnHeader(IntPtr data, nuint size, nuint nmemb, IntPtr userdata)
    {
        nuint total = size * nmemb;
        var line = Marshal.PtrToStringUTF8(data, (int)total)?.Trim();
        if (string.IsNullOrEmpty(line) || line.StartsWith("HTTP/"))
            return total;

        int colon = line.IndexOf(':');
        if (colon > 0)
        {
            var headers = (Dictionary<string, string>)GCHandle.FromIntPtr(userdata).Target!;
            headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        return total;
    }

    public void Dispose()
    {
        LibCurl.curl_global_cleanup();
    }

    private static readonly HashSet<string> Impersonations =
    [
        "chrome145",
        "chrome142",
        // "firefox147",
        "safari2601",
    ];

    // choose an impersonation randomly to be used for this session's requests to moddb
    private static readonly string Impersonation = Impersonations.ElementAt(
        Random.Shared.Next(Impersonations.Count)
    );
}
