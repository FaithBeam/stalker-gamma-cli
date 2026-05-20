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
    public string ArchiveNameZstd => Path.ChangeExtension(ArchiveName, "tar.zst");
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
    public string DownloadPathZstd => Path.Join(_downloadDirectory, ArchiveNameZstd);
    private string ExtractPath => _anomalyDir;

    public virtual async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (
                (!File.Exists(DownloadPathZstd) && !File.Exists(DownloadPath))
                || (
                    File.Exists(DownloadPath)
                    && await HashUtils.HashFile(
                        DownloadPath,
                        HashAlgorithmName.MD5,
                        pct => OnProgress(GammaProgressType.CheckMd5, pct),
                        cancellationToken
                    ) != StalkerAnomalyMd5
                )
            )
            {
                await _modDbUtility.GetModDbLinkCurl(
                    StalkerAnomalyUrl,
                    DownloadPath,
                    pct => OnProgress(GammaProgressType.Download, pct),
                    cancellationToken: cancellationToken
                );
                Downloaded = true;
            }
        }
        catch (Exception e)
        {
            throw new AnomalyInstallerException(
                $"""
                Error downloading Stalker Anomaly
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    public virtual async Task ExtractAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: This likely needs an extra extract on Windows
            if (File.Exists(DownloadPathZstd))
            {
                await _archiveUtility.ExtractAsync(
                    DownloadPathZstd,
                    ExtractPath,
                    pct => OnProgress(GammaProgressType.Extract, pct),
                    ct: cancellationToken
                );
            }
            else
            {
                await _archiveUtility.ExtractAsync(
                    DownloadPath,
                    ExtractPath,
                    pct => OnProgress(GammaProgressType.Extract, pct),
                    ct: cancellationToken
                );
            }
            _progress.IncrementCompletedMods();
        }
        catch (Exception e)
        {
            throw new AnomalyInstallerException(
                $"""
                Error extracting Stalker Anomaly
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    public bool Downloaded { get; set; }

    private void OnProgress(GammaProgressType operation, double pct) =>
        _progress.OnProgressChanged(ProgFunc(operation, pct));

    private GammaProgress.GammaInstallProgressEventArgs ProgFunc(
        GammaProgressType operation,
        double pct
    ) =>
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
