using System.Diagnostics;
using System.Text.Json;

namespace Stalker.Gamma.IntegrationTests.Cli;

/// <summary>
/// Runs the stalker-gamma CLI in its own process with its settings and logs in
/// <paramref name="appData"/>, so it never reads or changes the user's own
/// </summary>
public sealed class CliRunner(string appData, string workingDirectory)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    // the CLI project is referenced, so it's built next to the tests
    private static readonly string CliDll = Path.Join(
        AppContext.BaseDirectory,
        "stalker-gamma.dll"
    );

    private static readonly Lazy<bool> ResourcesReady = new(() =>
    {
        // the CLI loads 7-Zip from resources/ next to itself
        var source =
            NativeSevenZip.Path
            ?? throw new InvalidOperationException("The native 7-Zip library wasn't found");
        var resources = Path.Join(AppContext.BaseDirectory, "resources");
        Directory.CreateDirectory(resources);
        File.Copy(source, Path.Join(resources, Path.GetFileName(source)), overwrite: true);
        return true;
    });

    public string AppData { get; } = appData;

    public async Task<CliResult> RunAsync(params string[] args)
    {
        _ = ResourcesReady.Value;
        var startInfo = new ProcessStartInfo
        {
            // set by dotnet test to the dotnet that's running the tests
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
                is { Length: > 0 } host
                ? host
                : "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(CliDll);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
        startInfo.Environment["STALKER_GAMMA_APPDATA"] = AppData;

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(Timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"stalker-gamma {string.Join(' ', args)} didn't exit within {Timeout}"
            );
        }
        return new CliResult(process.ExitCode, await stdout, await stderr);
    }
}

public record CliResult(int ExitCode, string StdOut, string StdErr)
{
    /// <summary>
    /// The lines --json writes, one JSON object each
    /// </summary>
    public List<JsonElement> Messages =>
        StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith('{'))
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToList();

    public IEnumerable<JsonElement> MessagesOfType(string type) =>
        Messages.Where(x => x.GetProperty("type").GetString() == type);

    /// <summary>
    /// The result message --json ends every command with
    /// </summary>
    public JsonElement Result => MessagesOfType("result").Single();

    public override string ToString() =>
        $"""
            exit code {ExitCode}
            stdout:
            {StdOut}
            stderr:
            {StdErr}
            """;
}
