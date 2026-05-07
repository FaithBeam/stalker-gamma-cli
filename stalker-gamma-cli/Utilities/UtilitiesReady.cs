using Stalker.Gamma.Proxies;
using Stalker.Gamma.Utilities;

namespace stalker_gamma_cli.Utilities;

public class UtilitiesReady(
    PythonApiProxy pythonApiProxy,
    TarUtility tarUtility,
    UnzipUtility unzipUtility,
    SevenZipUtility sevenZipUtility
)
{
    public Task<bool> IsReady() =>
        Task.FromResult(
            GitUtility.Ready
                && sevenZipUtility.Ready
                && (OperatingSystem.IsWindows() || tarUtility.Ready)
                && (OperatingSystem.IsWindows() || unzipUtility.Ready)
        );

    public async Task<string> NotReadyReason() =>
        await IsReady()
            ? ""
            : $"""
                Curl: {(await pythonApiProxy.Ready() ? "Ready" : "Not Ready")}
                Git: {(GitUtility.Ready ? "Ready" : "Not Ready")}
                7z: {(sevenZipUtility.Ready ? "Ready" : "Not Ready")}
                Tar: {(tarUtility.Ready ? "Ready" : "Not Ready")}
                Unzip: {(unzipUtility.Ready ? "Ready" : "Not Ready")}
                """;
}
