namespace stalker_gamma_cli.Utilities;

public static class TerminalColor
{
    /// <summary>
    /// Whether to write ANSI colors. They only go to a terminal, never to a file or pipe, and
    /// <see href="https://no-color.org">NO_COLOR</see> turns them off.
    /// </summary>
    public static bool Enabled { get; } =
        !Console.IsOutputRedirected
        && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
}
