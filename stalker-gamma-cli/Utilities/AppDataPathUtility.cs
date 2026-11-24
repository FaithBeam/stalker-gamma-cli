namespace stalker_gamma_cli.Utilities;

/// <summary>
/// The folder the CLI keeps its settings and logs in. STALKER_GAMMA_APPDATA overrides it, so
/// tests and portable setups don't read or change the user's own settings.
/// </summary>
public static class AppDataPathUtility
{
    public const string Variable = "STALKER_GAMMA_APPDATA";

    public static string AppDataPath =>
        Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } configured
            ? Path.GetFullPath(configured)
            : Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "stalker-gamma"
            );

    public static string LogsPath => Path.Join(AppDataPath, "logs");
}
