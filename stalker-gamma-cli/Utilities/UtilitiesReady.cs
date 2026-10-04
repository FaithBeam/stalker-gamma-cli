using Stalker.Gamma.Factories;
using Stalker.Gamma.Services;

namespace stalker_gamma_cli.Utilities;

public class UtilitiesReady(
    NetworkServiceFactory networkServiceFactory,
    TarService tarService,
    UnzipService unzipService,
    SevenZipService sevenZipService
)
{
    public bool IsReady =>
        networkServiceFactory.Create().Ready
        && GitService.Ready
        && sevenZipService.Ready
        && (OperatingSystem.IsWindows() || tarService.Ready)
        && (OperatingSystem.IsWindows() || unzipService.Ready);

    public string NotReadyReason =>
        IsReady
            ? ""
            : $"""
                Curl: {(networkServiceFactory.Create().Ready ? "Ready" : "Not Ready")}
                Git: {(GitService.Ready ? "Ready" : "Not Ready")}
                7z: {(sevenZipService.Ready ? "Ready" : "Not Ready")}
                Tar: {(tarService.Ready ? "Ready" : "Not Ready")}
                Unzip: {(unzipService.Ready ? "Ready" : "Not Ready")}
                """;
}
