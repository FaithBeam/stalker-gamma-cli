using Serilog;

namespace stalker_gamma_cli.Utilities;

public static class LogAndExitOnDependencyError
{
    public static async Task Check(UtilitiesReady utilitiesReady, ILogger logger)
    {
        if (!await utilitiesReady.IsReady())
        {
            logger.Error(
                """
                Dependency not found:
                {Message}
                """,
                utilitiesReady.NotReadyReason
            );
            Environment.Exit(1);
        }
    }
}
