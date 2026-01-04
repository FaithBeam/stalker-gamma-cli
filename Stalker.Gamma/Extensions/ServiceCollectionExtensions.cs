using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.GammaInstallerServices.SpecialRepos;
using Stalker.Gamma.Models;
using Stalker.Gamma.ModOrganizer.DownloadModOrganizer;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterCoreGammaServices(this IServiceCollection s)
    {
        s.AddHttpClient()
            .AddHttpClient(
                "githubDlArchive",
                client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "stalker-gamma-clone/1.0");
                }
            )
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true,
                    AutomaticDecompression = DecompressionMethods.None,
                }
            );
        s.AddSingleton<StalkerGammaSettings>().AddSingleton<GammaProgress, GammaProgress>();
        return s.AddScoped<IDownloadModOrganizerService, DownloadModOrganizerService>()
            .AddScoped<ArchiveUtility>()
            .AddScoped<SevenZipUtility>()
            .AddScoped<TarUtility>()
            .AddScoped<UnzipUtility>()
            .AddScoped<GitUtility>()
            .AddScoped<ModDbUtility>()
            .AddScoped<MirrorUtility>()
            .AddScoped<CurlUtility>()
            .AddScoped<ISeparatorsFactory, SeparatorsFactory>()
            .AddScoped<IGetStalkerModsFromApi, GetStalkerModsFromApi>()
            .AddScoped<IModListRecordFactory, ModPackMakerRecordFactory>()
            .AddScoped<IDownloadableRecordFactory, DownloadableRecordFactory>()
            .AddScoped<IGammaLargeFilesRepo, GammaLargeFilesRepo>()
            .AddScoped<IGammaSetupRepo, GammaSetupRepo>()
            .AddScoped<IStalkerGammaRepo, StalkerGammaRepo>()
            .AddScoped<ITeivazAnomalyGunslingerRepo, TeivazAnomalyGunslingerRepo>()
            .AddScoped<GammaInstaller>()
            .AddScoped<IAnomalyInstaller, AnomalyInstaller>()
            .AddScoped<DownloadAndInstallCustomModlist>();
    }
}
