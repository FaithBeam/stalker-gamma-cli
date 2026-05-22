using Stalker.Gamma.Models;

namespace Stalker.Gamma.GammaInstallerServices.GammaInstaller;

public class GammaInstallerArgs
{
    public required string Anomaly { get; set; }
    public required string Gamma { get; set; }
    public required string Cache { get; set; }
    public string? Mo2Version { get; set; }
    public bool DownloadGithubArchives { get; set; } = true;
    public bool SkipExtractOnHashMatch { get; set; }
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
    public string Mo2Profile { get; set; } = "G.A.M.M.A";
    public bool Minimal { get; set; }
    public bool Offline { get; set; }
    public bool PreserveUserLtx { get; set; }
    public bool PreserveMcmSettings { get; set; }
    public bool UseExperimentalPythonServer { get; set; }
    public ExperimentalPythonServerSettings? ExperimentalPythonServerSettings { get; set; }
    public string? ModPackMakerPath { get; set; }
    public string? ModListPath { get; set; }
    public IList<IDownloadableRecord> GroupedAddonRecords { get; set; } = [];
    public IDownloadableRecord? AnomalyRecord { get; set; }
    public IDownloadableRecord? GammaLargeFilesRecord { get; set; }
    public IDownloadableRecord? TeivazAnomalyGunslingerRecord { get; set; }
    public IDownloadableRecord? GammaSetupRecord { get; set; }
    public IDownloadableRecord? StalkerGammaRecord { get; set; }

    public static GammaInstallerArgsBuilder Create(string anomaly, string gamma, string cache) =>
        new(anomaly, gamma, cache);
}
