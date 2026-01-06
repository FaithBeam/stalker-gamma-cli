using Stalker.Gamma.Utilities;

namespace stalker_gamma_cli.Utilities;

public class UtilitiesReady(
    CurlUtility curlUtility,
    GitUtility gitUtility,
    TarUtility tarUtility,
    UnzipUtility unzipUtility,
    SevenZipUtility sevenZipUtility
)
{
    public bool IsReady =>
        curlUtility.Ready
        && gitUtility.Ready
        && sevenZipUtility.Ready
        && tarUtility.Ready
        && unzipUtility.Ready;

    public string NotReadyReason =>
        IsReady
            ? ""
            : $"""
                Curl: {(curlUtility.Ready ? "Ready" : "Not Ready")}
                Git: {(gitUtility.Ready ? "Ready" : "Not Ready")}
                7z: {(sevenZipUtility.Ready ? "Ready" : "Not Ready")}
                Tar: {(tarUtility.Ready ? "Ready" : "Not Ready")}
                Unzip: {(unzipUtility.Ready ? "Ready" : "Not Ready")}
                """;
}
