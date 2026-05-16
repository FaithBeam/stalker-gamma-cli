using System.Reflection;
using System.Text.Json;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Services;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Extensions;

namespace stalker_gamma_cli;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var stalkerGammaLogsPath = Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "stalker-gamma",
            "logs"
        );
        var logPath = Path.Join(stalkerGammaLogsPath, "stalker-gamma-cli.log");
        Directory.CreateDirectory(stalkerGammaLogsPath);
        var log = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Logger(lc =>
                lc.Filter.ByIncludingOnly(e => e.Level != LogEventLevel.Information)
                    .WriteTo.File(
                        logPath,
                        rollingInterval: RollingInterval.Day,
                        fileSizeLimitBytes: 10_000_000,
                        rollOnFileSizeLimit: true,
                        restrictedToMinimumLevel: LogEventLevel.Verbose,
                        retainedFileCountLimit: 5,
                        outputTemplate: "{Message:lj}{NewLine}{Exception}"
                    )
            )
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "{Message:lj}{NewLine}",
                theme: ConsoleTheme.None
            )
            .CreateLogger();
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
                log.Verbose(
                    "Settings: {Settings}",
                    JsonSerializer.Serialize(
                        settings,
                        jsonTypeInfo: CliSettingsCtx.Default.CliSettings
                    )
                );
                services.AddSingleton(settings);
                services
                    .AddSingleton<ILogger>(log)
                    .AddScoped<UtilitiesReady>()
                    .AddScoped<ProgressLoggingService>()
                    .AddScoped<GetRemoteGitRepoCommit>()
                    .AddScoped<SetupUtilitiesService>()
                    .RegisterCoreGammaServices();
            });

        app.PostConfigureServices(sp =>
        {
            var setup = sp.GetRequiredService<SetupUtilitiesService>();
            setup.Setup();
        });

        try
        {
            log.Verbose("Starting stalker-gamma-cli");
            log.Verbose("OS: {OS}", Environment.OSVersion);
            log.Verbose("CWD: {Cwd}", Directory.GetCurrentDirectory());
            log.Verbose("stalker-gamma-cli Path: {Exe}", Environment.ProcessPath);
            log.Verbose(
                "stalker-gamma-cli Version: {Version}",
                Assembly.GetExecutingAssembly().GetName().Version
            );
            log.Verbose("Args: {Args}", string.Join(" ", args));

            await app.RunAsync(args);
        }
        catch (Exception e)
        {
            log.Fatal(e, "Application terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
