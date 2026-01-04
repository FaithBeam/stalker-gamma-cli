using System.Text.Json;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Services;
using Stalker.Gamma.Extensions;

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
                var settingsPath = Path.Join(
                    Path.Join(Path.GetDirectoryName(AppContext.BaseDirectory)!, "settings.json")
                );
                var settings = File.Exists(settingsPath)
                    ? JsonSerializer.Deserialize<CliSettings>(
                        File.ReadAllText(settingsPath),
                        jsonTypeInfo: CliSettingsCtx.Default.CliSettings
                    )
                        ?? throw new InvalidOperationException(
                            $"Unable to deserialize settings file {settingsPath}"
                        )
                    : new CliSettings();
                services.AddSingleton(settings);
                services
                    .AddSingleton<ILogger>(log)
                    .AddScoped<EnableLongPathsOnWindowsService>()
                    .AddScoped<AddFoldersToWinDefenderExclusionService>()
                    .RegisterCoreGammaServices();
            });

        await app.RunAsync(args);
    }
}
