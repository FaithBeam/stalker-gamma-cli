using Stalker.Gamma.Utilities;

namespace stalker_gamma_cli.Services;

public class AddFoldersToWinDefenderExclusionService
{
    public void Execute(params string[] folders)
    {
        var command =
            "Add-MpPreference -ExclusionPath " + string.Join(',', folders.Select(x => $"'{x}'"));
        PowerShellUtility.Execute(command);
    }
}
