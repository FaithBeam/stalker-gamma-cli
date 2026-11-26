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
        var stalkerGammaLogsPath = AppDataPathUtility.LogsPath;
        var logPath = Path.Join(stalkerGammaLogsPath, "stalker-gamma-cli.log");
        Directory.CreateDirectory(stalkerGammaLogsPath);
        var jsonOutput = JsonOutputUtility.IsEnabled(args);
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
            .WriteTo.Logger(lc =>
            {
                if (jsonOutput)
                {
                    lc.WriteTo.Console(
                        new JsonLogFormatter(),
                        restrictedToMinimumLevel: LogEventLevel.Information
                    );
                }
                else
                {
                    lc.WriteTo.Console(
                        restrictedToMinimumLevel: LogEventLevel.Information,
                        outputTemplate: "{Message:lj}{NewLine}",
                        theme: ConsoleTheme.None
                    );
                }
            })
            .CreateLogger();

        CliSettings settings;
        try
        {
            settings = CliSettings.Load();
        }
        catch (InvalidSettingsFileException e)
        {
            // every command needs the settings, so report it like a command failure instead of
            // letting the framework print a stack trace, which isn't json or in the log file
            log.Error("{Error}", e.Message);
            if (jsonOutput)
            {
                JsonOutputUtility.WriteResult(GetCommandName(args), 1, e.Message);
            }
            Environment.ExitCode = 1;
            await log.DisposeAsync();
            return;
        }

        var app = ConsoleApp
            .Create()
            .ConfigureServices(services =>
            {
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
                    .AddScoped<InstallCommandSupport>()
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
            // the logger isn't Log.Logger, so Log.CloseAndFlushAsync wouldn't flush or close it
            await log.DisposeAsync();
        }
    }

    /// <summary>
    /// The command words before the first option, e.g. "update apply" for
    /// <c>update apply --json</c>, as commands name themselves in their json result
    /// </summary>
    private static string GetCommandName(string[] args) =>
        string.Join(" ", args.TakeWhile(arg => !arg.StartsWith('-')));
}
