using System.Reflection;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Utilities;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("debug")]
public class Debug(ILogger logger, CliSettings cliSettings)
{
    /// <summary>
    /// Open the logs folder.
    /// </summary>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    public Task<int> Logs(bool json = false) =>
        Run(
            "debug logs",
            json,
            () =>
            {
                ValidateActiveProfile.Validate(_logger, cliSettings.ActiveProfile);

                var stalkerGammaLogsPath = Path.Join(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "stalker-gamma",
                    "logs"
                );
                Directory.CreateDirectory(stalkerGammaLogsPath);
                if (json)
                {
                    JsonOutputUtility.WriteLogsFolder(Path.GetFullPath(stalkerGammaLogsPath));
                }
                FileExplorerHelper.OpenFolderInExplorer(stalkerGammaLogsPath);
                return Task.FromResult<string?>(null);
            }
        );

    /// <summary>
    /// Hashes installation folders and creates a compressed archive containing the computed hashes.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    /// <returns></returns>
    public Task<int> HashInstall(CancellationToken cancellationToken, bool json = false) =>
        Run(
            "debug hash-install",
            json,
            async () =>
            {
                ValidateActiveProfile.Validate(_logger, cliSettings.ActiveProfile);
                var anomaly = cliSettings.ActiveProfile!.Anomaly;
                var gamma = cliSettings.ActiveProfile!.Gamma;
                var cache = cliSettings.ActiveProfile!.Cache;

                const HashType hashType = HashType.Sha256;

                var entry = Assembly.GetEntryAssembly();
                var infoVersion =
                    entry
                        ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                        ?.InformationalVersion
                    ?? "unknown";
                // Strip the +commit suffix if the SDK added one
                var plusIndex = infoVersion.IndexOf('+');
                if (plusIndex >= 0)
                {
                    infoVersion = infoVersion[..plusIndex];
                }
                var destinationArchive =
                    $"stalker-gamma-cli-hashes-{Environment.UserName}.{infoVersion}.zip";
                _logger.Information("Hashing install folders, this will take a while...");
                _logger.Information("Hash Type: {HashType}", hashType);
                await HashUtility.Hash(
                    destinationArchive,
                    anomaly,
                    gamma,
                    cache,
                    hashType,
                    ProgressThrottleUtility.Throttle<double>(pct =>
                    {
                        if (json)
                        {
                            JsonOutputUtility.WriteHashProgress(pct);
                        }
                        else
                        {
                            _logger.Information("Hash Progress: {Percent:P2}", pct);
                        }
                    }),
                    cancellationToken
                );
                _logger.Information("Finished hashing install folders");
                _logger.Information("Archive created at {DestinationArchive}", destinationArchive);
                if (json)
                {
                    JsonOutputUtility.WriteHashArchive(
                        Path.GetFullPath(destinationArchive),
                        hashType.ToString()
                    );
                }
                return null;
            }
        );

    private Task<int> Run(string command, bool json, Func<Task<string?>> action) =>
        JsonOutputUtility.RunAsync(_logger, command, json, action);

    private readonly ILogger _logger = logger;
}
