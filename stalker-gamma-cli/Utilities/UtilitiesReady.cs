using LibSevenZip;
using Stalker.Gamma.Factories;
using Stalker.Gamma.Services;

namespace stalker_gamma_cli.Utilities;

public class UtilitiesReady(NetworkServiceFactory networkServiceFactory)
{
    public bool IsReady =>
        networkServiceFactory.Create().Ready && GitService.Ready && SevenZipError is null;

    public string NotReadyReason =>
        IsReady
            ? ""
            : $"""
                Curl: {(networkServiceFactory.Create().Ready ? "Ready" : "Not Ready")}
                Git: {(GitService.Ready ? "Ready" : "Not Ready")}
                7z: {SevenZipError ?? "Ready"}
                """;

    private static string? SevenZipError
    {
        get
        {
            try
            {
                SevenZipLibrary.Load();
                return null;
            }
            catch (Exception e)
            {
                return $"Not Ready ({e.Message})";
            }
        }
    }
}
