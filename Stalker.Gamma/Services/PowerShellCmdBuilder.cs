namespace Stalker.Gamma.Services;

public class PowerShellCmdBuilder
{
    private readonly PowerShellCmd _powerShellCmd = new();

    public PowerShellCmdBuilder WithWindowsDefenderExclusions(params string[]? folders)
    {
        if (folders?.Length > 0)
        {
            _powerShellCmd.Cmds.Add(
                "Add-MpPreference -ExclusionPath " + string.Join(',', folders.Select(x => $"'{x}'"))
            );
        }
        return this;
    }

    public PowerShellCmdBuilder WithEnableLongPaths()
    {
        _powerShellCmd.Cmds.Add(
            """
            Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name "LongPathsEnabled" -Value "1"
            """
        );
        return this;
    }

    public PowerShellCmdBuilder WithCreateSymbolicLink(string? path, string? pathToTarget)
    {
        if (
            !string.IsNullOrWhiteSpace(path)
            && !string.IsNullOrWhiteSpace(pathToTarget)
            && !Directory.Exists(path)
        )
        {
            _powerShellCmd.Cmds.Add(
                $"New-Item -ItemType SymbolicLink -Path {path} -Value {pathToTarget}"
            );
        }
        return this;
    }

    public PowerShellCmd Build() => _powerShellCmd;
}
