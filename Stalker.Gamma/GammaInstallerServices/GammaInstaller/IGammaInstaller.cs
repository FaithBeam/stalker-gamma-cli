using Stalker.Gamma.Models;

namespace Stalker.Gamma.GammaInstallerServices.GammaInstaller;

public interface IGammaInstaller
{
    IGammaProgress Progress { get; }
    Task<IList<IDownloadableRecord>> BuildGroupedAddonRecordsAsync(GammaInstallerArgs args);
    void BuildSpecialRepoRecords(GammaInstallerArgs args);
    Task InstallAsync(GammaInstallerArgs args);
    IDownloadableRecord BuildAnomalyRecord(GammaInstallerArgs args);

    Task<GammaInstaller.DiffedAddonRecords> DiffAddonRecordsAsync(GammaInstallerArgs args);

    Task<IList<IDownloadableRecord>> BuildUpdateGroupedAddonRecordsAsync(GammaInstallerArgs args);
}
