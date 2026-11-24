using System.IO.Enumeration;
using System.Security.Cryptography;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Services;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Factories;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Utilities;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("anomaly")]
public class AnomalyInstallCmd(
    ILogger logger,
    CliSettings cliSettings,
    GammaProgress gammaProgress,
    IDownloadableRecordFactory downloadableRecordFactory,
    UtilitiesReady utilitiesReady,
    InstallCommandSupport support
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
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    /// <param name="useAnomalyMirror">Download Anomaly from the faith-beam.com mirror first, which extracts faster, and fall back to ModDB if it fails</param>
    /// <param name="progressUpdateIntervalMs">
    /// The time interval, in milliseconds, at which progress updates are reported.
    /// </param>
    /// <returns>
    /// </returns>
    [Command("install")]
    public async Task<int> AnomalyInstall(
        CancellationToken cancellationToken,
        bool verbose = false,
        bool json = false,
        bool useAnomalyMirror = false,
        [Hidden] long progressUpdateIntervalMs = 1000
    )
    {
        gammaProgress.TotalMods = 1;
        return await support.RunAsync(
            InstallCommand,
            "Anomaly install failed!",
            json,
            progressReporting: support.ReportProgress(
                gammaProgress,
                verbose,
                json,
                progressUpdateIntervalMs
            ),
            action: async () =>
            {
                var profile = support.Validate(usesDownloadThreads: false);
                support.ApplySettings(profile, useAnomalyMirror: useAnomalyMirror);
                var anomalyInstaller = downloadableRecordFactory.CreateAnomalyRecord(
                    profile.Cache,
                    profile.Anomaly
                );
                await anomalyInstaller.DownloadAsync(cancellationToken);
                await anomalyInstaller.ExtractAsync(cancellationToken);
                logger.Information("Anomaly install complete");
            }
        );
    }

    /// <summary>
    /// Verifies the integrity of Stalker Anomaly
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to abort the operation.
    /// </param>
    /// <param name="json">Write output as newline-delimited JSON for GUI wrappers</param>
    [Command("check")]
    public Task<int> CheckAnomaly(CancellationToken cancellationToken, bool json = false) =>
        JsonOutputUtility.RunAsync(
            logger,
            CheckCommand,
            json,
            async () =>
            {
                ValidateDependencies.Validate(utilitiesReady);
                var anomaly = ValidateActiveProfile.Validate(cliSettings.ActiveProfile).Anomaly;
                if (!Directory.Exists(anomaly))
                {
                    throw new DirectoryNotFoundException($"Directory {anomaly} doesn't exist");
                }

                var anomalyToolsPath = Path.Join(anomaly, "tools");
                if (!Directory.Exists(anomalyToolsPath))
                {
                    throw new DirectoryNotFoundException(
                        $"Directory {anomalyToolsPath} doesn't exist"
                    );
                }

                var anomalyChecksumsPath = Path.Join(anomalyToolsPath, "checksums.md5");
                if (!File.Exists(anomalyChecksumsPath))
                {
                    throw new FileNotFoundException($"File {anomalyChecksumsPath} doesn't exist");
                }

                var checksums = await GetChecksums(anomaly, anomalyChecksumsPath);
                var actual = await GetActualHashes(cancellationToken, anomaly);
                var longestPath = checksums.MaxBy(x => x.Path.Length).Path.Length + 5;
                var failures = await ValidateChecksums(checksums, actual, longestPath, json);
                return failures > 0 ? $"{failures} file(s) corrupt or missing" : null;
            }
        );

    /// <summary>
    /// Deletes the shader cache directory for the active Anomaly profile
    /// </summary>
    public Task<int> PurgeShaderCache() =>
        JsonOutputUtility.RunAsync(
            logger,
            "anomaly purge-shader-cache",
            json: false,
            () =>
            {
                PurgeShaderCache(ValidateActiveProfile.Validate(cliSettings.ActiveProfile));
                return Task.FromResult<string?>(null);
            }
        );

    private void PurgeShaderCache(CliProfile profile)
    {
        var anomalyAppDataShadersDir = Path.Join(profile.Anomaly, "appdata", "shaders_cache");
        if (Directory.Exists(anomalyAppDataShadersDir))
        {
            DirUtils.NormalizePermissions(anomalyAppDataShadersDir);
            Directory.Delete(anomalyAppDataShadersDir, true);
            logger.Information("Shader cache deleted");
        }
        else
        {
            logger.Information("Shader cache not found");
        }
    }

    /// <summary>
    /// Deletes all ReShade-related files from the Anomaly bin directory
    /// </summary>
    public Task<int> DeleteReshade() =>
        JsonOutputUtility.RunAsync(
            logger,
            "anomaly delete-reshade",
            json: false,
            () =>
                Task.FromResult(
                    DeleteReshade(ValidateActiveProfile.Validate(cliSettings.ActiveProfile))
                )
        );

    /// <returns>An error, or null if it succeeded</returns>
    private string? DeleteReshade(CliProfile profile)
    {
        var anomalyBinDir = Path.Join(profile.Anomaly, "bin");
        List<string> reshadeFiles =
        [
            "d3d9.dll",
            "dxgi.dll",
            "dxgi.log",
            "G.A.M.M.A.Reshade.ini",
            "ReShade.ini",
            "ReShade.log",
        ];
        if (Directory.Exists(anomalyBinDir))
        {
            var files = new DirectoryInfo(anomalyBinDir)
                .EnumerateFiles()
                .Where(x => reshadeFiles.Contains(x.Name, StringComparer.OrdinalIgnoreCase));
            foreach (var fi in files)
            {
                fi.Delete();
                logger.Information("Deleted {File}", fi.FullName);
            }

            var reshadeShadersDir = Path.Join(anomalyBinDir, "reshade-shaders");
            if (Directory.Exists(reshadeShadersDir))
            {
                Directory.Delete(reshadeShadersDir, true);
                logger.Information("Reshade shaders directory deleted");
            }
            logger.Information("Reshade files deleted");
            return null;
        }

        return JsonOutputUtility.LogError(logger, "Anomaly bin directory not found");
    }

    /// <returns>The number of files that are corrupt or missing</returns>
    private async Task<int> ValidateChecksums(
        List<(string Md5, string Path)> checksums,
        Dictionary<string, Task<string>> actual,
        int longestPath,
        bool json
    )
    {
        var failures = 0;
        for (var i = 0; i < checksums.Count; i++)
        {
            var checksum = checksums[i];
            var status =
                !actual.TryGetValue(checksum.Path, out var md5) ? ChecksumStatus.NotFound
                : checksum.Md5 == await md5 ? ChecksumStatus.Ok
                : ChecksumStatus.Corrupt;
            if (status != ChecksumStatus.Ok)
            {
                failures++;
            }

            if (json)
            {
                JsonOutputUtility.WriteChecksum(checksum.Path, status, i + 1, checksums.Count);
            }
            else if (status == ChecksumStatus.Ok)
            {
                logger.Information(InformationalCheck, checksum.Path.PadRight(longestPath), "OK");
            }
            else
            {
                logger.Error(
                    InformationalCheck,
                    checksum.Path.PadRight(longestPath),
                    status == ChecksumStatus.Corrupt ? "CORRUPT" : "NOT FOUND"
                );
            }
        }
        return failures;
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
                async x =>
                    await HashUtils.HashFile(
                        x,
                        HashAlgorithmName.MD5,
                        cancellationToken: cancellationToken
                    ),
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

    private const string InstallCommand = "anomaly install";
    private const string CheckCommand = "anomaly check";
    private const string InformationalCheck = "{File} | {Status}";
}
