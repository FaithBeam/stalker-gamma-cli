using Stalker.Gamma.Models;

namespace Stalker.Gamma.GammaInstallerServices.GammaInstaller;

public class GammaInstallerArgsBuilder(string anomaly, string gamma, string cache)
{
    private bool _downloadGithubArchives = true;
    private bool _skipExtractOnHashMatch;
    private IList<IDownloadableRecord> _groupedAddonRecords = [];
    private IDownloadableRecord? _anomalyRecord;
    private CancellationToken _cancellationToken = CancellationToken.None;
    private string _mo2Profile = "G.A.M.M.A";
    private bool _minimal;
    private bool _offline;
    private bool _preserveUserLtx;
    private bool _preserveMcmSettings;
    private string? _modPackMakerPath;
    private string? _modListPath;
    private bool _useExperimentalPythonServer;
    private ExperimentalPythonServerSettings? _experimentalPythonServerSettings;

    public GammaInstallerArgsBuilder WithCancellationToken(CancellationToken ct)
    {
        _cancellationToken = ct;
        return this;
    }

    public GammaInstallerArgsBuilder WithDownloadGithubArchives(bool value = true)
    {
        _downloadGithubArchives = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithSkipExtractOnHashMatch(bool value = true)
    {
        _skipExtractOnHashMatch = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithMo2Profile(string profile)
    {
        _mo2Profile = profile;
        return this;
    }

    public GammaInstallerArgsBuilder WithMinimal(bool value = true)
    {
        _minimal = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithOffline(bool value = true)
    {
        _offline = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithPreserveUserLtx(bool value = true)
    {
        _preserveUserLtx = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithPreserveMcmSettings(bool value = true)
    {
        _preserveMcmSettings = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithUseExperimentalPythonServer(bool value = false)
    {
        _useExperimentalPythonServer = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithExperimentalPythonServerSettings(
        ExperimentalPythonServerSettings? value
    )
    {
        _experimentalPythonServerSettings = value;
        return this;
    }

    public GammaInstallerArgsBuilder WithModPackMakerPath(string? path)
    {
        _modPackMakerPath = path;
        return this;
    }

    public GammaInstallerArgsBuilder WithModListPath(string? path)
    {
        _modListPath = path;
        return this;
    }

    public GammaInstallerArgsBuilder WithGroupedAddonRecords(IList<IDownloadableRecord> records)
    {
        _groupedAddonRecords = records;
        return this;
    }

    public GammaInstallerArgsBuilder WithAnomalyRecord(IDownloadableRecord? record)
    {
        _anomalyRecord = record;
        return this;
    }

    public GammaInstallerArgs Build() =>
        new()
        {
            Anomaly = anomaly,
            Gamma = gamma,
            Cache = cache,
            DownloadGithubArchives = _downloadGithubArchives,
            SkipExtractOnHashMatch = _skipExtractOnHashMatch,
            CancellationToken = _cancellationToken,
            Mo2Profile = _mo2Profile,
            Minimal = _minimal,
            Offline = _offline,
            PreserveUserLtx = _preserveUserLtx,
            PreserveMcmSettings = _preserveMcmSettings,
            ModPackMakerPath = _modPackMakerPath,
            ModListPath = _modListPath,
            GroupedAddonRecords = _groupedAddonRecords,
            AnomalyRecord = _anomalyRecord,
            UseExperimentalPythonServer = _useExperimentalPythonServer,
            ExperimentalPythonServerSettings = _experimentalPythonServerSettings,
        };
}
