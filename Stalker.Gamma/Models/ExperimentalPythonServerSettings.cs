namespace Stalker.Gamma.Models;

public class ExperimentalPythonServerSettings
{
    public bool Enabled { get; set; }
    public required string Host { get; set; }
    public int Port { get; set; }
}
