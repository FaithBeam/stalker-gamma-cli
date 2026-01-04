using System.IO.Enumeration;
using System.Reactive.Linq;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Models;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("anomaly")]
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
    [Command("install")]
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
    
    /// <summary>
    /// Verifies the integrity of Stalker Anomaly
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to abort the operation.
    /// </param>
    [Command("check")]
    public async Task CheckAnomaly(CancellationToken cancellationToken)
    {
        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
        var anomaly = _cliSettings.ActiveProfile!.Anomaly;
        if (!Directory.Exists(anomaly))
        {
            throw new DirectoryNotFoundException($"Directory {anomaly} doesn't exist");
        }

        var anomalyToolsPath = Path.Join(anomaly, "tools");
        if (!Directory.Exists(anomalyToolsPath))
        {
            throw new DirectoryNotFoundException($"Directory {anomalyToolsPath} doesn't exist");
        }

        var anomalyChecksumsPath = Path.Join(anomalyToolsPath, "checksums.md5");
        if (!File.Exists(anomalyChecksumsPath))
        {
            throw new FileNotFoundException($"File {anomalyChecksumsPath} doesn't exist");
        }

        var checksums = await GetChecksums(anomaly, anomalyChecksumsPath);
        var actual = await GetActualHashes(cancellationToken, anomaly);
        var longestPath = checksums.MaxBy(x => x.Path.Length).Path.Length + 5;
        await ValidateChecksums(checksums, actual, longestPath);
    }

    private async Task ValidateChecksums(
        List<(string Md5, string Path)> checksums,
        Dictionary<string, Task<string>> actual,
        int longestPath
    )
    {
        foreach (var checksum in checksums)
        {
            if (actual.TryGetValue(checksum.Path, out var md5))
            {
                if (checksum.Md5 == await md5)
                {
                    _logger.Information(InformationalCheck, checksum.Path.PadRight(longestPath), "OK");
                }
                else
                {
                    _logger.Error(InformationalCheck, checksum.Path.PadRight(longestPath), "CORRUPT");
                }
            }
            else
            {
                _logger.Error(InformationalCheck, checksum.Path.PadRight(longestPath), "NOT FOUND");
            }
        }
    }

    private static async Task<Dictionary<string, Task<string>>> GetActualHashes(
        CancellationToken cancellationToken,
        string anomaly
    )
    {
        var actual = await new FileSystemEnumerable<FileSystemInfo>(
            anomaly,
            transform: (ref entry) => entry.ToFileSystemInfo(),
            new EnumerationOptions { RecurseSubdirectories = true }
        )
            .Where(x => !x.Attributes.HasFlag(FileAttributes.Directory))
            .ToAsyncEnumerable()
            .Select(x => x.FullName)
            .ToDictionaryAsync(
                x => x,
                async x => await HashUtility.Md5HashFile(x, cancellationToken),
                cancellationToken: cancellationToken
            );
        return actual;
    }

    private static async Task<List<(string Md5, string Path)>> GetChecksums(
        string anomaly,
        string anomalyChecksumsPath
    ) =>
        (await File.ReadAllTextAsync(anomalyChecksumsPath))
            .Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .Select(line =>
            {
                var split = line.Split(
                    ' ',
                    2,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                );
                return (
                    split[0],
                    Path.GetFullPath(
                        Path.Join(
                            split[1]
                                .Replace("*", $"{anomaly}{Path.DirectorySeparatorChar}")
                                .Replace('\\', Path.DirectorySeparatorChar)
                        )
                    )
                );
            })
            .ToList();

    private const string InformationalCheck = "{File} | {Status}";

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
