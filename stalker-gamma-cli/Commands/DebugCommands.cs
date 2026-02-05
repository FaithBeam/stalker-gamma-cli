using System.Reflection;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Utilities;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("debug")]
public class Debug(ILogger logger, CliSettings cliSettings, UtilitiesReady utilitiesReady)
{
    /// <summary>
    /// Hashes installation folders and creates a compressed archive containing the computed hashes.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns></returns>
    public async Task HashInstall(CancellationToken cancellationToken)
    {
        if (!utilitiesReady.IsReady)
        {
            _logger.Error(
                """
                Dependency not found:
                {Message}
                """,
                utilitiesReady.NotReadyReason
            );
            Environment.Exit(1);
        }

        ValidateActiveProfile.Validate(_logger, cliSettings.ActiveProfile);
        var anomaly = cliSettings.ActiveProfile!.Anomaly;
        var gamma = cliSettings.ActiveProfile!.Gamma;
        var cache = cliSettings.ActiveProfile!.Cache;

        const HashType hashType = HashType.Sha256;

        var entry = Assembly.GetEntryAssembly();
        var infoVersion =
            entry?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";
        infoVersion = infoVersion[..infoVersion.IndexOf('+')];
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
                _logger.Information("Hash Progress: {Percent:P2}", pct)
            ),
            cancellationToken
        );
        _logger.Information("Finished hashing install folders");
        _logger.Information("Archive created at {DestinationArchive}", destinationArchive);
    }

    private readonly ILogger _logger = logger;
}
