using System.Diagnostics;

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
                $"New-Item -ItemType SymbolicLink -Path '{path}' -Value '{pathToTarget}'"
            );
        }
        return this;
    }

    public PowerShellCmd Build() => _powerShellCmd;
}

public class PowerShellCmd
{
    public readonly List<string> Cmds = [];

    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (Cmds.Count > 0)
        {
            var cmd = string.Join("; ", Cmds);
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"{cmd}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                Verb = "runas", // this runs as admin
            };
            process.Start();
            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0)
            {
                throw new PowerShellCmdException(
                    $"""
                    PowerShell exited with code {process.ExitCode}
                    Commands: {string.Join("\n", Cmds)}");"
                    """
                );
            }
        }
    }
}

public class PowerShellCmdException(string message) : Exception(message);
