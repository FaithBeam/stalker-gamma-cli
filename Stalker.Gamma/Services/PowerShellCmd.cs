using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Stalker.Gamma.Services;

public partial class PowerShellCmdBuilder
{
    private readonly PowerShellCmd _powerShellCmd = new();

    public PowerShellCmdBuilder WithWindowsDefenderExclusions(params string[]? folders)
    {
        if (folders?.Length > 0)
        {
            _powerShellCmd.Cmds.Add(
                "Add-MpPreference -ExclusionPath " + string.Join(',', folders.Select(Quote))
            );
        }
        return this;
    }

    public PowerShellCmdBuilder WithEnableLongPaths()
    {
        _powerShellCmd.Cmds.Add(
            """
            Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' -Name 'LongPathsEnabled' -Value 1
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
                $"New-Item -ItemType SymbolicLink -Path {Quote(path)} -Value {Quote(pathToTarget)}"
            );
        }
        return this;
    }

    public PowerShellCmd Build() => _powerShellCmd;

    /// <summary>
    /// Wraps a value in a PowerShell single-quoted string. PowerShell treats the curly single
    /// quotes as quote characters too, so all of them are escaped by doubling.
    /// </summary>
    internal static string Quote(string value) => $"'{SingleQuoteChars().Replace(value, "$0$0")}'";

    [GeneratedRegex("['‘’‚‛]")]
    private static partial Regex SingleQuoteChars();
}

public class PowerShellCmd
{
    public readonly List<string> Cmds = [];

    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (Cmds.Count > 0)
        {
            var cmd = string.Join("; ", Cmds);
            // -EncodedCommand takes the script as base64 UTF-16LE, so nothing in it has to
            // survive command-line quoting
            var encodedCmd = Convert.ToBase64String(Encoding.Unicode.GetBytes(cmd));
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -EncodedCommand {encodedCmd}",
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
                    Commands: {string.Join("\n", Cmds)}
                    """
                );
            }
        }
    }
}

public class PowerShellCmdException(string message) : Exception(message);
