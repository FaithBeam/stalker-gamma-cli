namespace Stalker.Gamma.Models;

public class StalkerGammaSettings
{
    public string ModpackMakerList { get; set; } = "https://stalker-gamma.com/api/list";
    public string? ModListUrl { get; set; }
    public string GammaLargeFilesRepo { get; set; } =
        "https://github.com/Grokitach/gamma_large_files_v2";
    public string GammaLargeFilesRepoBranch { get; set; } = "main";
    public string GammaSetupRepo { get; set; } = "https://github.com/Grokitach/gamma_setup";
    public string GammaSetupRepoBranch { get; set; } = "main";
    public string StalkerGammaRepo { get; set; } = "https://github.com/Grokitach/Stalker_GAMMA";
    public string StalkerGammaRepoBranch { get; set; } = "main";
    public string TeivazAnomalyGunslingerRepo { get; set; } =
        "https://github.com/Grokitach/teivaz_anomaly_gunslinger";
    public string TeivazAnomalyGunslingerRepoBranch { get; set; } = "main";

    public string ModOrganizer244Md5 { get; set; } = "e2bb7233cdab78f56912ebf4a0091768";
    public string ModOrganizer252Md5 { get; set; } = "b223ce1297107adbabb3fbaa3769eb2b";
    public int DownloadThreads { get; set; } = 2;
    public string PathToUnzip = "unzip";
    public string PathTo7Z = OperatingSystem.IsWindows() ? "7zz.exe" : "7zz";
    public string PathToTar = "tar";
    public string PythonApiUrl { get; set; } = "http://localhost:8000";
}
