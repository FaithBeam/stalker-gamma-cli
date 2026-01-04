using System.Reactive.Linq;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Models;

namespace stalker_gamma_cli.Commands;

[RegisterCommands]
public class AnomalyInstallCmd(
    ILogger logger,
    CliSettings cliSettings,
    StalkerGammaSettings stalkerGammaSettings,
    GammaProgress gammaProgress,
    IDownloadableRecordFactory downloadableRecordFactory
)
{
    /// <summary>
    /// Installs Stalker Anomaly.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests.
    /// </param>
    /// <param name="verbose">
    /// Indicates whether progress updates should be logged in verbose mode.
    /// </param>
    /// <param name="progressUpdateIntervalMs">
    /// The time interval, in milliseconds, at which progress updates are reported.
    /// </param>
    /// <returns>
    /// </returns>
    public async Task AnomalyInstall(
        CancellationToken cancellationToken,
        bool verbose = false,
        [Hidden] long progressUpdateIntervalMs = 250
    )
    {
        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
        var anomaly = _cliSettings.ActiveProfile!.Anomaly;
        var cache = _cliSettings.ActiveProfile!.Cache;
        var resourcesPath = Path.Join(Path.GetDirectoryName(AppContext.BaseDirectory), "resources");
        stalkerGammaSettings.PathToCurl = Path.Join(
            resourcesPath,
            OperatingSystem.IsWindows() ? "curl.exe" : "curl-impersonate"
        );
        stalkerGammaSettings.PathTo7Z = Path.Join(
            resourcesPath,
            OperatingSystem.IsWindows() ? "7zz.exe" : "7zz"
        );
        stalkerGammaSettings.PathToGit = OperatingSystem.IsWindows()
            ? Path.Join(resourcesPath, "git", "cmd", "git.exe")
            : "git";

        var anomalyInstaller = (AnomalyInstaller)
            downloadableRecordFactory.CreateAnomalyRecord(cache, anomaly);
        gammaProgress.TotalMods = 1;
        var gammaProgressObservable = Observable
            .FromEventPattern<GammaProgress.GammaInstallProgressEventArgs>(
                handler => anomalyInstaller.Progress.ProgressChanged += handler,
                handler => anomalyInstaller.Progress.ProgressChanged -= handler
            )
            .Select(x => x.EventArgs);
        var gammaProgressDisposable = gammaProgressObservable
            .Sample(TimeSpan.FromMilliseconds(progressUpdateIntervalMs))
            .Subscribe(verbose ? OnProgressChangedVerbose : OnProgressChangedInformational);
        try
        {
            await anomalyInstaller.DownloadAsync(cancellationToken);
            await anomalyInstaller.ExtractAsync(cancellationToken);
            _logger.Information("Anomaly install complete");
        }
        finally
        {
            gammaProgressDisposable.Dispose();
        }
    }

    private void OnProgressChangedInformational(GammaProgress.GammaInstallProgressEventArgs e) =>
        _logger.Information(
            Informational,
            e.Name[..Math.Min(e.Name.Length, 35)].PadRight(40),
            e.ProgressType.PadRight(10),
            $"{e.Progress:P2}".PadRight(8),
            $"[{e.Complete}/{e.Total}]"
        );

    private void OnProgressChangedVerbose(GammaProgress.GammaInstallProgressEventArgs e) =>
        _logger.Information(
            Verbose,
            e.Name[..Math.Min(e.Name.Length, 35)].PadRight(40),
            e.ProgressType.PadRight(10),
            $"{e.Progress:P2}".PadRight(8),
            $"[{e.Complete}/{e.Total}]",
            e.Url
        );

    private readonly ILogger _logger = logger;
    private readonly CliSettings _cliSettings = cliSettings;
    private const string Informational = "{AddonName} | {Operation} | {Percent} | {CompleteTotal}";
    private const string Verbose =
        "{AddonName} | {Operation} | {Percent} | {CompleteTotal} | {Url}";
}
