using System.Text.RegularExpressions;
using ConsoleAppFramework;
using Serilog;
using stalker_gamma_cli.Models;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Models;

namespace stalker_gamma_cli.Commands;

[RegisterCommands("gog")]
public partial class Gog(
    ILogger logger,
    CliSettings cliSettings,
    StalkerGammaSettings stalkerGammaSettings
)
{
    /// <summary>
    /// Fix the GOG installation that creates the ModOrganizer.ini with bad paths.
    /// </summary>
    public async Task FixInstall()
    {
        ValidateActiveProfile.Validate(_logger, _cliSettings.ActiveProfile);
        var mo2IniPath = Path.Join(_cliSettings.ActiveProfile!.Gamma, "ModOrganizer.ini");
        var pathTxtFilePath = Path.Join(_cliSettings.ActiveProfile.Gamma, "..", "path.txt");
        var pathIniPath = Path.Join(_cliSettings.ActiveProfile.Gamma, "..", "path.ini");
        if (!File.Exists(mo2IniPath) || !File.Exists(pathTxtFilePath) || !File.Exists(pathIniPath))
        {
            _logger.Error("ModOrganizer.ini, path.txt, or path.ini not found");
            return;
        }

        var mo2IniTxt = await File.ReadAllTextAsync(mo2IniPath);
        var pathTxt = (await File.ReadAllTextAsync(pathTxtFilePath)).Replace("\\", @"\\");
        var pathIniTxt = await File.ReadAllTextAsync(pathIniPath);
        var badPath = PathIniPathRx().Match(pathIniTxt).Groups["path"].Value.Trim();
        mo2IniTxt = mo2IniTxt.Replace(badPath, pathTxt.Replace("/", @"\\"));
        _logger.Information(
            "Replacing {BadPath} with {GoodPath} in {FilePath}",
            badPath,
            pathTxt,
            mo2IniPath
        );
        await File.WriteAllTextAsync(mo2IniPath, mo2IniTxt);
        _logger.Information("ModOrganizer.ini updated");
    }

    [GeneratedRegex("^path=(?<path>.+)$", RegexOptions.Multiline)]
    private partial Regex PathIniPathRx();

    private readonly ILogger _logger = logger;
    private readonly CliSettings _cliSettings = cliSettings;
    private readonly StalkerGammaSettings _stalkerGammaSettings = stalkerGammaSettings;
}
