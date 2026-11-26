using System.Runtime.InteropServices;
using System.Text;

namespace LibCurlImpersonate;

public static class CurlHttp
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
    ) => FetchResponse(url, onSpeed, http3, onHttpVersion, ct).Body;

    /// <summary>
    /// Fetches a url and returns its body with the HTTP status code. Error statuses aren't thrown
    /// so callers can inspect the body, e.g. for a Cloudflare challenge page served with 403.
    /// </summary>
    public static CurlResponse FetchResponse(
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

        // the body is collected as bytes and decoded once, since a chunk can end partway
        // through a multi-byte UTF-8 character
        using var body = new MemoryStream();
        var writePin = GCHandle.Alloc(body);
        var xferPin = InstallXferCallback(handle, ct, onSpeed: onSpeed);
        var errBuf = AllocErrorBuffer();
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
            ApplyTimeouts(handle);
            LibCurl.curl_easy_setopt_ptr(handle, LibCurl.CURLOPT_ERRORBUFFER, errBuf);
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
                throw CurlException.Create(code, url, Marshal.PtrToStringUTF8(errBuf));

            ReportHttpVersion(handle, onHttpVersion);
            LibCurl.curl_easy_getinfo_long(handle, LibCurl.CURLINFO_RESPONSE_CODE, out long status);
            return new CurlResponse(
                (int)status,
                Encoding.UTF8.GetString(body.GetBuffer(), 0, (int)body.Length)
            );
        }
        finally
        {
            writePin.Free();
            if (xferPin.IsAllocated)
                xferPin.Free();
            LibCurl.curl_easy_cleanup(handle);
            Marshal.FreeHGlobal(errBuf);
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
        var errBuf = AllocErrorBuffer();
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
            ApplyTimeouts(handle);
            LibCurl.curl_easy_setopt_ptr(handle, LibCurl.CURLOPT_ERRORBUFFER, errBuf);
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
                throw CurlException.Create(code, url, Marshal.PtrToStringUTF8(errBuf));

            ReportHttpVersion(handle, onHttpVersion);
            return headers;
        }
        finally
        {
            headerPin.Free();
            if (xferPin.IsAllocated)
                xferPin.Free();
            LibCurl.curl_easy_cleanup(handle);
            Marshal.FreeHGlobal(errBuf);
        }
    }

    /// <summary>
    /// Fail a request that can't connect, or that stalls, instead of waiting forever. libcurl has no
    /// total timeout by default, so one hung request would block everything waiting on it.
    /// </summary>
    private static void ApplyTimeouts(IntPtr handle)
    {
        LibCurl.curl_easy_setopt_long(
            handle,
            LibCurl.CURLOPT_CONNECTTIMEOUT,
            (long)ConnectTimeout.TotalSeconds
        );
        // abort when the transfer is slower than 1 byte/s for StallTimeout
        LibCurl.curl_easy_setopt_long(handle, LibCurl.CURLOPT_LOW_SPEED_LIMIT, 1L);
        LibCurl.curl_easy_setopt_long(
            handle,
            LibCurl.CURLOPT_LOW_SPEED_TIME,
            (long)StallTimeout.TotalSeconds
        );
    }

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

    // Buffer for CURLOPT_ERRORBUFFER; libcurl writes a detailed message here on failure.
    private static IntPtr AllocErrorBuffer()
    {
        var buf = Marshal.AllocHGlobal(LibCurl.CURL_ERROR_SIZE);
        Marshal.WriteByte(buf, 0);
        return buf;
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

    private static unsafe nuint OnWrite(IntPtr data, nuint size, nuint nmemb, IntPtr userdata)
    {
        nuint total = size * nmemb;
        var body = (MemoryStream)GCHandle.FromIntPtr(userdata).Target!;
        body.Write(new ReadOnlySpan<byte>((void*)data, (int)total));
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

/// <param name="StatusCode">The HTTP status code of the last response, after redirects</param>
/// <param name="Body">The response body</param>
public readonly record struct CurlResponse(int StatusCode, string Body);
