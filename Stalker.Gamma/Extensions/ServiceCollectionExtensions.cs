using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.GammaInstallerServices.SpecialRepos;
using Stalker.Gamma.Models;
using Stalker.Gamma.ModOrganizer.DownloadModOrganizer;
using Stalker.Gamma.Services;
using Stalker.Gamma.Utilities;
using CurlService = Stalker.Gamma.Services.CurlService;
using ModDbGetAddonMetadataService = Stalker.Gamma.ModDb.Services.ModDbGetAddonMetadataService;
using ModDbGetCdnLinkService = Stalker.Gamma.ModDb.Services.ModDbGetCdnLinkService;
using ModDbMirrorService = Stalker.Gamma.ModDb.Services.ModDbMirrorService;
using ModDbService = Stalker.Gamma.ModDb.Services.ModDbService;
using SevenZipService = Stalker.Gamma.Services.SevenZipService;

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
            .AddScoped<ArchiveService>()
            .AddScoped<SevenZipService>()
            .AddScoped<TarService>()
            .AddScoped<UnzipService>()
            .AddScoped<GitService>()
            .AddScoped<ModDbService>()
            .AddScoped<ModDbMirrorService>()
            .AddScoped<CurlService>()
            .AddScoped<ModDbGetCdnLinkService>()
            .AddScoped<PreserveMcmSettings>()
            .AddScoped<PreserveUserLtxSettingsService>()
            .AddScoped<GetCanonicalLinkFromModDbStartLink>()
            .AddScoped<ModDbGetAddonMetadataService>()
            .AddScoped<IGetStalkerModsFromLocal, GetStalkerModsFromLocal>()
            .AddScoped<ISeparatorsFactory, SeparatorsFactory>()
            .AddScoped<IGetStalkerModsFromApi, GetStalkerModsFromApi>()
            .AddScoped<IModListRecordFactory, ModPackMakerRecordFactory>()
            .AddScoped<IDownloadableRecordFactory, DownloadableRecordFactory>()
            .AddScoped<IGammaLargeFilesRepo, GammaLargeFilesRepo>()
            .AddScoped<IGammaSetupRepo, GammaSetupRepo>()
            .AddScoped<IStalkerGammaRepo, StalkerGammaRepo>()
            .AddScoped<ITeivazAnomalyGunslingerRepo, TeivazAnomalyGunslingerRepo>()
            .AddScoped<IGammaInstaller, GammaInstaller>()
            .AddScoped<OfflineGammaInstaller>()
            .AddScoped<IAnomalyInstaller, AnomalyInstaller>()
            .AddScoped<PowerShellCmdBuilder>();
    }
}
