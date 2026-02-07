using System.Text.Json;
using CliWrap;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Services;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Extensions;
using Stalker.Gamma.Models;

namespace stalker_gamma_cli;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var log = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        var app = ConsoleApp
            .Create()
            .ConfigureServices(services =>
            {
                var settings = File.Exists(CliSettings.SettingsPath)
                    ? JsonSerializer.Deserialize<CliSettings>(
                        File.ReadAllText(CliSettings.SettingsPath),
                        jsonTypeInfo: CliSettingsCtx.Default.CliSettings
                    )
                        ?? throw new InvalidOperationException(
                            $"Unable to deserialize settings file {CliSettings.SettingsPath}"
                        )
                    : new CliSettings();
                services.AddSingleton(settings);
                services
                    .AddSingleton<ILogger>(log)
                    .AddScoped<UtilitiesReady>()
                    .AddScoped<GetRemoteGitRepoCommit>()
                    .AddScoped<SetupUtilitiesService>()
                    .RegisterCoreGammaServices();
            });

        app.PostConfigureServices(sp =>
        {
            var setup = sp.GetRequiredService<SetupUtilitiesService>();
            setup.Setup();
        });

        await app.RunAsync(args);
    }
}
