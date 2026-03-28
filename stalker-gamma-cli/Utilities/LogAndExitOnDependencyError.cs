using Serilog;

namespace stalker_gamma_cli.Utilities;

public static class LogAndExitOnDependencyError
{
    public static void Check(UtilitiesReady utilitiesReady, ILogger logger)
    {
        if (!utilitiesReady.IsReady)
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
