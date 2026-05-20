using Stalker.Gamma.Utilities;

namespace stalker_gamma_cli.Utilities;

public class UtilitiesReady(
    CurlUtility curlUtility,
    TarUtility tarUtility,
    UnzipUtility unzipUtility,
    SevenZipUtility sevenZipUtility
)
{
    public bool IsReady =>
        curlUtility.Ready
        && GitUtility.Ready
        && sevenZipUtility.Ready
        && (OperatingSystem.IsWindows() || tarUtility.Ready)
        && (OperatingSystem.IsWindows() || unzipUtility.Ready);

    public string NotReadyReason =>
        IsReady
            ? ""
            : $"""
                Curl: {(curlUtility.Ready ? "Ready" : "Not Ready")}
                Git: {(GitUtility.Ready ? "Ready" : "Not Ready")}
                7z: {(sevenZipUtility.Ready ? "Ready" : "Not Ready")}
                Tar: {(tarUtility.Ready ? "Ready" : "Not Ready")}
                Unzip: {(unzipUtility.Ready ? "Ready" : "Not Ready")}
                """;
}
