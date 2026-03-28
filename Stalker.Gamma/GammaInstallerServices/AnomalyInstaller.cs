using System.Security.Cryptography;
using Stalker.Gamma.Models;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices;

public interface IAnomalyInstaller : IDownloadableRecord;

public class AnomalyInstaller(
    GammaProgress progress,
    string downloadDirectory,
    string anomalyDir,
    ModDbUtility modDbUtility,
    ArchiveUtility archiveUtility
) : IAnomalyInstaller
{
    public string Name { get; } = "Stalker Anomaly";
    public string ArchiveName { get; } = "Anomaly-1.5.3-Full.2.7z";
    protected string StalkerAnomalyUrl = "https://www.moddb.com/downloads/start/277404";
    private const string NiceUrl =
        "https://www.moddb.com/mods/stalker-anomaly/downloads/stalker-anomaly-153";

    public IGammaProgress Progress => _progress;
    protected string StalkerAnomalyMd5 = "d6bce51a4e6d98f9610ef0aa967ba964";
    private readonly GammaProgress _progress = progress;
    private readonly string _downloadDirectory = downloadDirectory;
    private readonly string _anomalyDir = anomalyDir;
    private readonly ModDbUtility _modDbUtility = modDbUtility;
    private readonly ArchiveUtility _archiveUtility = archiveUtility;
    public string DownloadPath => Path.Join(_downloadDirectory, ArchiveName);
    private string ExtractPath => _anomalyDir;

    public virtual async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (
            !File.Exists(DownloadPath)
            || (
                File.Exists(DownloadPath)
                && await HashUtils.HashFile(
                    DownloadPath,
                    HashAlgorithmName.MD5,
                    pct => OnProgress("Check MD5", pct),
                    cancellationToken
                ) != StalkerAnomalyMd5
            )
        )
        {
            await _modDbUtility.GetModDbLinkCurl(
                StalkerAnomalyUrl,
                DownloadPath,
                pct => OnProgress("Download", pct),
                cancellationToken: cancellationToken
            );
            Downloaded = true;
        }
    }

    public virtual async Task ExtractAsync(CancellationToken cancellationToken = default)
    {
        await _archiveUtility.ExtractAsync(
            DownloadPath,
            ExtractPath,
            pct => OnProgress("Extract", pct),
            ct: cancellationToken
        );
        _progress.IncrementCompletedMods();
    }

    public bool Downloaded { get; set; }

    private void OnProgress(string operation, double pct) =>
        _progress.OnProgressChanged(ProgFunc(operation, pct));

    private GammaProgress.GammaInstallProgressEventArgs ProgFunc(string operation, double pct) =>
        new()
        {
            Name = Name,
            ProgressType = operation,
            Progress = pct,
            Url = StalkerAnomalyUrl,
            ArchiveName = ArchiveName,
            DownloadPath = DownloadPath,
            ExtractPath = ExtractPath,
        };
}

public class AnomalyInstallerException(string message, Exception innerException)
    : Exception(message, innerException);
