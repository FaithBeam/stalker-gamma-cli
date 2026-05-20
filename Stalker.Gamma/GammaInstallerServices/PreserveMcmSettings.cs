namespace Stalker.Gamma.GammaInstallerServices;

public class PreserveMcmSettings
{
    public async Task ReadAxrOptionsAsync(string gammaPath, CancellationToken ct = default)
    {
        _mcmModSettingsPath = Path.Join(
            gammaPath,
            "mods",
            "G.A.M.M.A. MCM values - Rename to keep your personal changes",
            "gamedata",
            "configs"
        );
        _axrOptionsPath = Path.Join(_mcmModSettingsPath, "axr_options.ltx");
        if (File.Exists(_axrOptionsPath))
        {
            _axrOptionsContent = await File.ReadAllTextAsync(_axrOptionsPath, ct);
            _lastWriteTime = File.GetLastWriteTimeUtc(_axrOptionsPath);
        }
    }

    public async Task WriteAxrOptionsAsync(CancellationToken ct = default)
    {
        if (
            Directory.Exists(_mcmModSettingsPath)
            && !string.IsNullOrWhiteSpace(_axrOptionsPath)
            && !string.IsNullOrWhiteSpace(_axrOptionsContent)
            && _lastWriteTime != null
        )
        {
            await File.WriteAllTextAsync(_axrOptionsPath, _axrOptionsContent, ct);
            File.SetLastAccessTimeUtc(_axrOptionsPath, _lastWriteTime.Value.UtcDateTime);
            File.SetLastWriteTimeUtc(_axrOptionsPath, _lastWriteTime.Value.UtcDateTime);
        }
    }

    private string? _mcmModSettingsPath;
    private string? _axrOptionsPath;
    private string? _axrOptionsContent;
    private DateTimeOffset? _lastWriteTime;
}
