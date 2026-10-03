using System.Runtime.InteropServices;

namespace LibCurlImpersonate;

/// <summary>
/// A curl_easy_perform failure. Derives from InvalidOperationException so existing catch sites keep working.
/// </summary>
public class CurlException(int code, string url, string message)
    : InvalidOperationException(message)
{
    /// <summary>The CURLcode returned by libcurl.</summary>
    public int Code { get; } = code;

    public string Url { get; } = url;

    internal static CurlException Create(int code, string url, string? detail)
    {
        var summary = Marshal.PtrToStringUTF8(LibCurl.curl_easy_strerror(code)) ?? "Unknown error";
        var msg = $"Request to {url} failed: {summary} (curl error {code})";
        if (!string.IsNullOrWhiteSpace(detail) && detail != summary)
            msg += $": {detail}";
        if (Hint(code) is { } hint)
            msg += $". {hint}";
        return new CurlException(code, url, msg);
    }

    private static string? Hint(int code) =>
        code switch
        {
            LibCurl.CURLE_COULDNT_RESOLVE_HOST or LibCurl.CURLE_COULDNT_RESOLVE_PROXY =>
                "Check your internet connection and DNS settings",
            LibCurl.CURLE_COULDNT_CONNECT =>
                "The server could not be reached; check your connection, firewall, or VPN/proxy",
            LibCurl.CURLE_OPERATION_TIMEDOUT => "The server took too long to respond; try again later",
            LibCurl.CURLE_SSL_CONNECT_ERROR =>
                "The TLS handshake failed; antivirus or a proxy intercepting HTTPS can cause this",
            LibCurl.CURLE_PEER_FAILED_VERIFICATION =>
                "The server's certificate could not be verified; check your system clock and any HTTPS-intercepting software",
            LibCurl.CURLE_SSL_CACERT_BADFILE => "cacert.pem is missing or unreadable next to the executable",
            LibCurl.CURLE_GOT_NOTHING or LibCurl.CURLE_RECV_ERROR or LibCurl.CURLE_SEND_ERROR =>
                "The connection was dropped; try again",
            LibCurl.CURLE_HTTP3 or LibCurl.CURLE_QUIC_CONNECT_ERROR =>
                "HTTP/3 failed; your network may be blocking UDP/QUIC",
            LibCurl.CURLE_TOO_MANY_REDIRECTS => "The server is stuck in a redirect loop",
            _ => null,
        };
}
