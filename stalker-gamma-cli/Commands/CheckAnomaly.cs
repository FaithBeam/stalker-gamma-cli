using System.IO.Enumeration;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Utilities;

namespace stalker_gamma_cli.Commands;

[RegisterCommands]
public class CheckAnomalyCmd(ILogger logger, CliSettings cliSettings)
{
    /// <summary>
    /// Verifies the integrity of Stalker Anomaly
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to abort the operation.
    /// </param>
    public async Task CheckAnomaly(CancellationToken cancellationToken)
    {
        ValidateActiveProfile.Validate(_logger, cliSettings.ActiveProfile);
        var anomaly = cliSettings.ActiveProfile!.Anomaly;
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

    private async Task ValidateChecksums(List<(string Md5, string Path)> checksums, Dictionary<string, Task<string>> actual, int longestPath)
    {
        foreach (var checksum in checksums)
        {
            if (actual.TryGetValue(checksum.Path, out var md5))
            {
                if (checksum.Md5 == await md5)
                {
                    _logger.Information(Informational, checksum.Path.PadRight(longestPath), "OK");
                }
                else
                {
                    _logger.Error(Informational, checksum.Path.PadRight(longestPath), "CORRUPT");
                }
            }
            else
            {
                _logger.Error(Informational, checksum.Path.PadRight(longestPath), "NOT FOUND");
            }
        }
    }

    private static async Task<Dictionary<string, Task<string>>> GetActualHashes(CancellationToken cancellationToken, string anomaly)
    {
        var actual = await new FileSystemEnumerable<FileSystemInfo>(anomaly,
            transform: (ref entry) => entry.ToFileSystemInfo(),
            new EnumerationOptions
            {
                RecurseSubdirectories = true,
            }).Where(x => !x.Attributes.HasFlag(FileAttributes.Directory)).ToAsyncEnumerable().Select(x => x.FullName).ToDictionaryAsync(x => x,
            async x => await HashUtility.Md5HashFile(x, cancellationToken), cancellationToken: cancellationToken);
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
            return
                (split[0],
                    Path.GetFullPath(
                        Path.Join(
                            split[1]
                                .Replace("*", $"{anomaly}{Path.DirectorySeparatorChar}")
                                .Replace('\\', Path.DirectorySeparatorChar)
                        )
                    ));
        }).ToList();

    private readonly ILogger _logger = logger;

    private const string Informational = "{File} | {Status}";
}