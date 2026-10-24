using System.Security.Cryptography;
using Stalker.Gamma.Models;
using Stalker.Gamma.Services;
using Stalker.Gamma.Utilities;
using ModDbService = Stalker.Gamma.ModDb.Services.ModDbService;

namespace Stalker.Gamma.GammaInstallerServices;

public interface IAnomalyInstaller : IDownloadableRecord;

public class AnomalyInstaller(
    GammaProgress progress,
    string downloadDirectory,
    string anomalyDir,
    ModDbService modDbService,
    ArchiveService archiveService,
    IHttpClientFactory hcf,
    string? mirrorUrl
) : IAnomalyInstaller
{
    public string Name { get; } = "Stalker Anomaly";
    public string ArchiveName { get; } = "Anomaly-1.5.3-Full.2.7z";
    public string MirrorArchiveName { get; } = "Anomaly-1.5.3-Full.mt-mx5.7z";
    protected string StalkerAnomalyUrl = "https://www.moddb.com/downloads/start/277404";
    private const string NiceUrl =
        "https://www.moddb.com/mods/stalker-anomaly/downloads/stalker-anomaly-153";

    public IGammaProgress Progress => _progress;
    protected string StalkerAnomalyMd5 = "d6bce51a4e6d98f9610ef0aa967ba964";

    // the mirror hosts a repack compressed with multithreaded lzma2 so 7zz extracts it on every core
    protected string StalkerAnomalyMirrorMd5 = "123a0566485a77f8d44a659630cce856";
    private readonly GammaProgress _progress = progress;
    private readonly string _downloadDirectory = downloadDirectory;
    private readonly string _anomalyDir = anomalyDir;
    private readonly ModDbService _modDbService = modDbService;
    private readonly ArchiveService _archiveService = archiveService;
    private readonly HttpClient _hc = hcf.CreateClient("dlAddon");
    private readonly string? _mirrorUrl = mirrorUrl;
    private string? _activeUrl;

    // the mirror archive takes priority when it exists because it's only created once its hash matches,
    // and DownloadAsync deletes it if it has since become invalid
    public string DownloadPath =>
        File.Exists(MirrorDownloadPath) ? MirrorDownloadPath : ModDbDownloadPath;
    private string ModDbDownloadPath => Path.Join(_downloadDirectory, ArchiveName);
    private string MirrorDownloadPath => Path.Join(_downloadDirectory, MirrorArchiveName);

    // the mirror downloads here and is only moved to MirrorDownloadPath once its hash matches
    private string MirrorPartialDownloadPath => $"{MirrorDownloadPath}.part";
    private const int MirrorDownloadAttempts = 3;
    private string ExtractPath => _anomalyDir;

    public virtual async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(MirrorDownloadPath))
            {
                if (
                    await HashMatchesAsync(
                        MirrorDownloadPath,
                        StalkerAnomalyMirrorMd5,
                        cancellationToken
                    )
                )
                {
                    return;
                }
                File.Delete(MirrorDownloadPath);
            }

            if (
                !File.Exists(ModDbDownloadPath)
                || !await HashMatchesAsync(ModDbDownloadPath, StalkerAnomalyMd5, cancellationToken)
            )
            {
                if (!await TryDownloadFromMirrorAsync(cancellationToken))
                {
                    _activeUrl = null;
                    await _modDbService.DownloadAddonAsync(
                        StalkerAnomalyUrl,
                        ModDbDownloadPath,
                        p => OnProgress(GammaProgressType.Download, p.Percent, p),
                        cancellationToken: cancellationToken
                    );
                }
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

    /// <summary>
    /// Downloads the repack from the mirror. Returns false so the caller falls back to ModDB when the mirror
    /// isn't configured, can't be reached, or serves a file that doesn't match its hash.
    /// </summary>
    private async Task<bool> TryDownloadFromMirrorAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_mirrorUrl))
        {
            return false;
        }

        try
        {
            _activeUrl = _mirrorUrl;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await DownloadFileFast.DownloadAsync(
                        _hc,
                        _mirrorUrl,
                        MirrorPartialDownloadPath,
                        p => OnProgress(GammaProgressType.Download, p.Percent, p),
                        cancellationToken,
                        resume: true
                    );
                    break;
                }
                // the partial file is kept so the next attempt continues where the connection dropped
                catch (DownloadFileFastException)
                    when (attempt < MirrorDownloadAttempts
                        && !cancellationToken.IsCancellationRequested
                    ) { }
            }

            if (
                await HashMatchesAsync(
                    MirrorPartialDownloadPath,
                    StalkerAnomalyMirrorMd5,
                    cancellationToken
                )
            )
            {
                File.Move(MirrorPartialDownloadPath, MirrorDownloadPath, overwrite: true);
                return true;
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }

        // a cancelled download skips this and keeps its partial file so the next run resumes it
        if (File.Exists(MirrorPartialDownloadPath))
        {
            File.Delete(MirrorPartialDownloadPath);
        }
        return false;
    }

    private async Task<bool> HashMatchesAsync(
        string path,
        string md5,
        CancellationToken cancellationToken
    ) =>
        await HashUtils.HashFile(
            path,
            HashAlgorithmName.MD5,
            pct => OnProgress(GammaProgressType.CheckMd5, pct),
            cancellationToken
        ) == md5;

    public virtual async Task ExtractAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _archiveService.ExtractAsync(
                DownloadPath,
                ExtractPath,
                pct => OnProgress(GammaProgressType.Extract, pct),
                ct: cancellationToken
            );
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

    private void OnProgress(
        GammaProgressType operation,
        double? pct,
        DownloadProgress? download = null
    ) => _progress.OnProgressChanged(ProgFunc(operation, pct, download));

    private GammaProgress.GammaInstallProgressEventArgs ProgFunc(
        GammaProgressType operation,
        double? pct,
        DownloadProgress? download
    ) =>
        new()
        {
            Name = Name,
            ProgressType = operation,
            Progress = pct,
            BytesDownloaded = download?.BytesRead,
            TotalBytes = download?.TotalBytes,
            Url = _activeUrl ?? StalkerAnomalyUrl,
            ArchiveName = ArchiveName,
            DownloadPath = DownloadPath,
            ExtractPath = ExtractPath,
        };
}

public class AnomalyInstallerException(string message, Exception innerException)
    : Exception(message, innerException);
