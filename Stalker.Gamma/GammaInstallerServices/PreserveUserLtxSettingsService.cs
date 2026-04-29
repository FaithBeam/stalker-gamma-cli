namespace Stalker.Gamma.GammaInstallerServices;

public class PreserveUserLtxSettingsService
{
    public async Task ReadUserLtxAsync(string anomalyPath, CancellationToken ct = default)
    {
        _anomalyPath = anomalyPath;
        _anomalyAppDataPath = Path.Join(_anomalyPath, "appdata");
        _userLtxPath = Path.Join(_anomalyAppDataPath, "user.ltx");

        if (File.Exists(_userLtxPath))
        {
            _userLtxContent = await File.ReadAllTextAsync(_userLtxPath, ct);
        }
    }

    public async Task WriteUserLtxAsync(CancellationToken ct = default)
    {
        if (
            Directory.Exists(_anomalyAppDataPath)
            && !string.IsNullOrWhiteSpace(_userLtxPath)
            && !string.IsNullOrWhiteSpace(_userLtxContent)
        )
        {
            await File.WriteAllTextAsync(_userLtxPath, _userLtxContent, ct);
        }
    }

    private string? _anomalyPath;
    private string? _anomalyAppDataPath;
    private string? _userLtxPath;
    private string? _userLtxContent;
}
