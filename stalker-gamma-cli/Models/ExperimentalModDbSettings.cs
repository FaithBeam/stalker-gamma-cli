using System.Text.Json.Serialization;
using Stalker.Gamma.Models;

namespace stalker_gamma_cli.Models;

[JsonSerializable(typeof(ExperimentalModDbSettings))]
public partial class ExperimentalModDbSettingsCtx : JsonSerializerContext;

public class ExperimentalModDbSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public ushort Port { get; set; } = 8000;

    public bool TrySet(string setting, string value, out string? error)
    {
        error = null;
        switch (setting.ToLowerInvariant())
        {
            case "host":
                Host = value;
                return true;

            case "port":
                if (!ushort.TryParse(value, out var port))
                {
                    error = "Port must be a number between 0 and 65535.";
                    return false;
                }

                Port = port;
                return true;

            default:
                error = $"Unknown setting '{setting}'.";
                return false;
        }
    }

    public ExperimentalPythonServerSettings ToServerSettings() =>
        new() { Host = Host, Port = Port };
}
