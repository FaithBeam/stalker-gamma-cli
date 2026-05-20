using System.Diagnostics;

namespace Stalker.Gamma.Services;

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
